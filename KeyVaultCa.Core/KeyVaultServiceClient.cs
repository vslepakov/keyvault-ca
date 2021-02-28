using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.KeyVault.WebKey;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace KeyVaultCa.Core
{
    /// <summary>
    /// The KeyVault service client.
    /// </summary>
    public class KeyVaultServiceClient
    {
        private readonly string _vaultBaseUrl;
        private IKeyVaultClient _keyVaultClient;
        private readonly string _clientId;
        private ClientCredential _clientCredential;

        /// <summary>
        /// Create the service client for KeyVault, with service (client) credentials.
        /// </summary>
        /// <param name="vaultBaseUrl">The Url of the Key Vault.</param>
        /// <param name="appId"></param>
        /// <param name="appSecret"></param>
        public KeyVaultServiceClient(string vaultBaseUrl, string appId, string appSecret)
        {
            _vaultBaseUrl = vaultBaseUrl;
            _clientId = appId;
            _clientCredential = new ClientCredential(appId, appSecret);
            _keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(GetAccessTokenUsingClientCredentialsAsync));
        }

        /// <summary>
        /// Create the service client for KeyVault, with device authentication.
        /// </summary>
        /// <param name="vaultBaseUrl">The Url of the Key Vault.</param>
        /// <param name="appId"></param>
        public KeyVaultServiceClient(string vaultBaseUrl, string appId)
        {
            _vaultBaseUrl = vaultBaseUrl;
            _clientId = appId;
            _keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(GetAccessTokenUsingDeviceAuthenticationAsync));
        }

        public async Task<X509Certificate2> CreateCACertificateAsync(
                string id,
                string subject,
                DateTime notBefore,
                DateTime notAfter,
                int keySize,
                int hashSize,
                int pathLengthConstraint,
                CancellationToken ct = default)
        {
            try
            {
                // delete pending operations
                await _keyVaultClient.DeleteCertificateOperationAsync(_vaultBaseUrl, id);
            }
            catch
            {
                // intentionally ignore errors
            }

            string caTempCertIdentifier = null;

            try
            {
                //// policy self signed, new key
                var policySelfSignedNewKey = CreateCertificatePolicy(subject, keySize, true, false);
                var tempAttributes = CreateCertificateAttributes(DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(10));
                var createKey = await _keyVaultClient.CreateCertificateAsync(
                    _vaultBaseUrl,
                    id,
                    policySelfSignedNewKey,
                    tempAttributes,
                    null,
                    ct)
                    .ConfigureAwait(false);

                CertificateOperation operation;

                do
                {
                    await Task.Delay(1000);
                    operation = await _keyVaultClient.GetCertificateOperationAsync(_vaultBaseUrl, id, ct);

                } while (operation.Status == "inProgress" && !ct.IsCancellationRequested);

                if (operation.Status != "completed")
                {
                    throw new Exception("Failed to create new key pair.");
                }

                var createdCertificateBundle = await _keyVaultClient.GetCertificateAsync(_vaultBaseUrl, id).ConfigureAwait(false);
                var caCertKeyIdentifier = createdCertificateBundle.KeyIdentifier.Identifier;
                caTempCertIdentifier = createdCertificateBundle.CertificateIdentifier.Identifier;

                // policy unknown issuer, reuse key
                var policyUnknownReuse = CreateCertificatePolicy(subject, keySize, false, true);
                var attributes = CreateCertificateAttributes(notBefore, notAfter);
                var tags = CreateCertificateTags(id, false);

                // create the CSR
                var createResult = await _keyVaultClient.CreateCertificateAsync(
                    _vaultBaseUrl,
                    id,
                    policyUnknownReuse,
                    attributes,
                    tags,
                    ct)
                    .ConfigureAwait(false);

                if (createResult.Csr == null)
                {
                    throw new Exception("Failed to read CSR from CreateCertificate.");
                }

                // decode the CSR and verify consistency
                var pkcs10CertificationRequest = new Org.BouncyCastle.Pkcs.Pkcs10CertificationRequest(createResult.Csr);
                var info = pkcs10CertificationRequest.GetCertificationRequestInfo();
                if (createResult.Csr == null ||
                    pkcs10CertificationRequest == null ||
                    !pkcs10CertificationRequest.Verify())
                {
                    throw new Exception("Invalid CSR.");
                }

                // create the self signed root CA cert
                var publicKey = KeyVaultCertFactory.GetRSAPublicKey(info.SubjectPublicKeyInfo);
                var signedcert = await KeyVaultCertFactory.CreateSignedCertificate(
                    subject,
                    (ushort)keySize,
                    notBefore,
                    notAfter,
                    (ushort)hashSize,
                    null,
                    publicKey,
                    new KeyVaultSignatureGenerator(this, createdCertificateBundle.KeyIdentifier.Identifier, null),
                    caCert: true,
                    pathLengthConstraint: pathLengthConstraint);

                // merge Root CA cert with
                var mergeResult = await _keyVaultClient.MergeCertificateAsync(
                    _vaultBaseUrl,
                    id,
                    new X509Certificate2Collection(signedcert));

                return signedcert;
            }
            catch (KeyVaultErrorException kex)
            {
                Console.WriteLine($"Failed to create new Root CA certificate: {kex}");
                throw;
            }
            finally
            {
                if (caTempCertIdentifier != null)
                {
                    try
                    {
                        // disable the temp cert for self signing operation
                        var attr = new CertificateAttributes()
                        {
                            Enabled = false
                        };
                        await _keyVaultClient.UpdateCertificateAsync(caTempCertIdentifier, null, attr);
                    }
                    catch
                    {
                        // intentionally ignore error
                    }
                }
            }
        }

        /// <summary>
        /// Sign a digest with the signing key.
        /// </summary>
        public async Task<byte[]> SignDigestAsync(
            string signingKey,
            byte[] digest,
            HashAlgorithmName hashAlgorithm,
            RSASignaturePadding padding,
            CancellationToken ct = default)
        {
            string algorithm;

            if (padding == RSASignaturePadding.Pkcs1)
            {
                if (hashAlgorithm == HashAlgorithmName.SHA256)
                {
                    algorithm = JsonWebKeySignatureAlgorithm.RS256;
                }
                else if (hashAlgorithm == HashAlgorithmName.SHA384)
                {
                    algorithm = JsonWebKeySignatureAlgorithm.RS384;
                }
                else if (hashAlgorithm == HashAlgorithmName.SHA512)
                {
                    algorithm = JsonWebKeySignatureAlgorithm.RS512;
                }
                else
                {
                    Console.WriteLine($"Error in SignDigestAsync {signingKey}. Unsupported hash algorithm used.");
                    throw new ArgumentOutOfRangeException(nameof(hashAlgorithm));
                }
            }
            else
            {
                Console.WriteLine($"Error in SignDigestAsync {padding}. Unsupported padding algorithm used.");
                throw new ArgumentOutOfRangeException(nameof(padding));
            }

            var result = await _keyVaultClient.SignAsync(signingKey, algorithm, digest, ct).ConfigureAwait(false);
            return result.Result;
        }

        private Dictionary<string, string> CreateCertificateTags(string id, bool trusted)
        {
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                [id] = trusted ? "Trusted" : "Issuer"
            };
            return tags;
        }

        private CertificatePolicy CreateCertificatePolicy(
            string subject,
            int keySize,
            bool selfSigned,
            bool reuseKey = false,
            bool exportable = false)
        {

            var policy = new CertificatePolicy
            {
                IssuerParameters = new IssuerParameters
                {
                    Name = selfSigned ? "Self" : "Unknown"
                },
                KeyProperties = new KeyProperties
                {
                    Exportable = exportable,
                    KeySize = keySize,
                    KeyType = "RSA",
                    ReuseKey = reuseKey
                },
                SecretProperties = new SecretProperties
                {
                    ContentType = CertificateContentType.Pfx
                },
                X509CertificateProperties = new X509CertificateProperties
                {
                    Subject = subject                
                },
            };
            return policy;
        }

        private CertificateAttributes CreateCertificateAttributes(DateTime notBefore, DateTime notAfter)
        {
            var attributes = new CertificateAttributes
            {
                Enabled = true,
                NotBefore = notBefore,
                Expires = notAfter
            };

            return attributes;
        }

        /// <summary>
        /// Private callback for keyvault authentication using client credentials.
        /// </summary>
        private async Task<string> GetAccessTokenUsingClientCredentialsAsync(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            AuthenticationResult result;
            try
            {
                result = await context.AcquireTokenSilentAsync(resource, _clientId);
            }
            catch (AdalException)
            {
                result = await context.AcquireTokenAsync(resource, _clientCredential);
            }
            return result.AccessToken;
        }

        /// <summary>
        /// Private callback for keyvault authentication using device authentication.
        /// </summary>
        private async Task<string> GetAccessTokenUsingDeviceAuthenticationAsync(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            AuthenticationResult result;
            try
            {
                result = await context.AcquireTokenSilentAsync(resource, _clientId);
            }
            catch (AdalException)
            {
                var deviceCode = await context.AcquireDeviceCodeAsync(resource, _clientId);
                Console.WriteLine(deviceCode.Message);
                result = await context.AcquireTokenByDeviceCodeAsync(deviceCode);
            }
            return result.AccessToken;
        }

        /// <summary>
        /// Get Certificate bundle from key Vault.
        /// </summary>
        /// <param name="name">Key Vault name</param>
        /// <param name="ct">CancellationToken</param>
        /// <returns></returns>
        internal async Task<CertificateBundle> GetCertificateAsync(string name, CancellationToken ct = default)
        {
            return await _keyVaultClient.GetCertificateAsync(_vaultBaseUrl, name, ct).ConfigureAwait(false);
        }

        internal async Task<IPage<CertificateItem>> GetCertificateVersionsAsync(string name)
        {
            return await _keyVaultClient.GetCertificateVersionsAsync(_vaultBaseUrl, name).ConfigureAwait(false);
        }
    }
}

