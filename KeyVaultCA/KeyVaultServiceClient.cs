// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------


using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.KeyVault.WebKey;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KeyVaultCA
{
    public struct CertificateKeyInfo
    {
        public X509Certificate2 Certificate { get; set; }
        public string KeyIdentifier { get; set; }
    }

    /// <summary>
    /// The KeyVault service client.
    /// </summary>
    public class KeyVaultServiceClient
    {
        public const int MaxResults = 5;
        public const string ContentTypeJson = "application/json";
        // see RFC 2585
        public const string ContentTypeCert = "application/pkix-cert";
        public const string ContentTypeCrl = "application/pkix-crl";
        // see CertificateContentType.Pfx and
        public const string ContentTypePfx = "application/x-pkcs12";
        // see CertificateContentType.Pem
        public const string ContentTypePem = "application/x-pem-file";

        // trust list tags
        public const string TagIssuerList = "Issuer";
        public const string TagTrustedList = "Trusted";

        /// <summary>
        /// Create the service client for KeyVault, with user or service credentials.
        /// </summary>
        /// <param name="groupSecret">The name of the secret for group configuration</param>
        /// <param name="vaultBaseUrl">The Url of the Key Vault.</param>
        /// <param name="keyStoreHSM">The KeyVault is HSM backed.</param>
        /// <param name="logger">The logger.</param>
        public KeyVaultServiceClient(
            string groupSecret,
            string vaultBaseUrl,
            bool keyStoreHSM
            )
        {
            _groupSecret = groupSecret;
            _vaultBaseUrl = vaultBaseUrl;
            _keyStoreHSM = keyStoreHSM;
        }

        /// <summary>
        /// Set appID and app secret for keyVault authentication.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="appSecret"></param>
        public void SetAuthenticationClientCredential(
            string appId,
            string appSecret)
        {
            _assertionCert = null;
            _clientCredential = new ClientCredential(appId, appSecret);
            _keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(GetAccessTokenAsync));
        }

        /// <summary>
        /// Set appID and client certificate for keyVault authentication.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="clientAssertionCertPfx"></param>
        public void SetAuthenticationAssertionCertificate(
            string appId,
            X509Certificate2 clientAssertionCertPfx)
        {
            _clientCredential = null;
            _assertionCert = new ClientAssertionCertificate(appId, clientAssertionCertPfx);
            _keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(GetAccessTokenAsync));
        }

        /// <summary>
        /// Authentication for MSI or dev user callback.
        /// </summary>
        public void SetAuthenticationTokenProvider()
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
            _keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
        }


        /// <summary>
        /// Service client credentials.
        /// </summary>
        public void SetServiceClientCredentials(ServiceClientCredentials credentials)
        {
            _keyVaultClient = new KeyVaultClient(credentials);
        }

        /// <summary>
        /// Private callback for keyvault authentication.
        /// </summary>
        private async Task<string> GetAccessTokenAsync(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            AuthenticationResult result;
            if (_clientCredential != null)
            {
                result = await context.AcquireTokenAsync(resource, _clientCredential);
            }
            else
            {
                result = await context.AcquireTokenAsync(resource, _assertionCert);
            }
            return result.AccessToken;
        }

        /// <summary>
        /// Read the OpcVault CertificateConfigurationGroups as Json.
        /// </summary>
        public async Task<string> GetCertificateConfigurationGroupsAsync(CancellationToken ct = default)
        {
            SecretBundle secret = await _keyVaultClient.GetSecretAsync(_vaultBaseUrl, _groupSecret, ct).ConfigureAwait(false);
            return secret.Value;
        }

        /// <summary>
        /// Write the OpcVault CertificateConfigurationGroups as Json.
        /// </summary>
        public async Task<string> PutCertificateConfigurationGroupsAsync(string json, CancellationToken ct = default)
        {
            SecretBundle secret = await _keyVaultClient.SetSecretAsync(_vaultBaseUrl, _groupSecret, json, null, ContentTypeJson, null, ct).ConfigureAwait(false);
            return secret.Value;
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

        /// <summary>
        /// Read all certificate versions of a CA certificate group.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="thumbprint">filter for thumbprint</param>
        /// <param name="nextPageLink"></param>
        /// <param name="pageSize"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<(X509Certificate2Collection, string)> GetCertificateVersionsAsync(string id, string thumbprint = null, string nextPageLink = null, int? pageSize = null, CancellationToken ct = default)
        {
            var certificates = new X509Certificate2Collection();
            pageSize = pageSize ?? MaxResults;
            try
            {
                IPage<CertificateItem> certItems = null;
                if (nextPageLink != null)
                {
                    certItems = await _keyVaultClient.GetCertificateVersionsNextAsync(nextPageLink, ct).ConfigureAwait(false);
                }
                else
                {
                    certItems = await _keyVaultClient.GetCertificateVersionsAsync(_vaultBaseUrl, id, pageSize, ct).ConfigureAwait(false);
                }
                while (certItems != null)
                {
                    foreach (var certItem in certItems)
                    {
                        if (certItem.Attributes.Enabled ?? false)
                        {
                            var certBundle = await _keyVaultClient.GetCertificateAsync(certItem.Id, ct).ConfigureAwait(false);
                            var cert = new X509Certificate2(certBundle.Cer);
                            if (thumbprint == null ||
                                cert.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase))
                            {
                                certificates.Add(cert);
                            }
                        }
                    }
                    if (certItems.NextPageLink != null)
                    {
                        nextPageLink = certItems.NextPageLink;
                        certItems = null;
                        if (certificates.Count < pageSize)
                        {
                            certItems = await _keyVaultClient.GetCertificateVersionsNextAsync(nextPageLink, ct).ConfigureAwait(false);
                            nextPageLink = null;
                        }
                    }
                    else
                    {
                        certItems = null;
                        nextPageLink = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while loading the certificate versions for {id}. {ex}");
            }
            return (certificates, nextPageLink);
        }

        /// <summary>
        /// Read all certificate versions of a CA certificate group.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<IList<CertificateKeyInfo>> GetCertificateVersionsKeyInfoAsync(string id, CancellationToken ct = default)
        {
            var result = new List<CertificateKeyInfo>();
            try
            {
                var certItems = await _keyVaultClient.GetCertificateVersionsAsync(_vaultBaseUrl, id, MaxResults, ct).ConfigureAwait(false);
                while (certItems != null)
                {
                    foreach (var certItem in certItems)
                    {
                        var certBundle = await _keyVaultClient.GetCertificateAsync(certItem.Id, ct).ConfigureAwait(false);
                        var cert = new X509Certificate2(certBundle.Cer);
                        var certKeyInfo = new CertificateKeyInfo
                        {
                            Certificate = new X509Certificate2(certBundle.Cer),
                            KeyIdentifier = certBundle.KeyIdentifier.Identifier
                        };
                        result.Add(certKeyInfo);
                    }
                    if (certItems.NextPageLink != null)
                    {
                        certItems = await _keyVaultClient.GetCertificateVersionsNextAsync(certItems.NextPageLink, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        certItems = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while loading the certificate versions for {id}. {ex}");
            }
            return result;
        }

        /// <summary>
        /// Load the signing CA certificate for signing operations.
        /// </summary>
        internal Task<X509Certificate2> LoadSigningCertificateAsync(string signingCertificateKey, X509Certificate2 publicCert, CancellationToken ct = default)
        {
#if LOADPRIVATEKEY
            var secret = await _keyVaultClient.GetSecretAsync(signingCertificateKey, ct);
            if (secret.ContentType == CertificateContentType.Pfx)
            {
                var certBlob = Convert.FromBase64String(secret.Value);
                return CertificateFactory.CreateCertificateFromPKCS12(certBlob, string.Empty);
            }
            else if (secret.ContentType == CertificateContentType.Pem)
            {
                Encoding encoder = Encoding.UTF8;
                var privateKey = encoder.GetBytes(secret.Value.ToCharArray());
                return CertificateFactory.CreateCertificateWithPEMPrivateKey(publicCert, privateKey, string.Empty);
            }
            throw new NotImplementedException("Unknown content type: " + secret.ContentType);
#else
            Console.WriteLine("Error in LoadSigningCertificateAsync " + signingCertificateKey + "." +
                "Loading the private key is not permitted.", signingCertificateKey);
            throw new NotSupportedException("Loading the private key from key Vault is not permitted.");
#endif
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
                    Console.WriteLine("Error in SignDigestAsync " + signingKey + "." +
                        "Unsupported hash algorithm used.", signingKey );
                    throw new ArgumentOutOfRangeException(nameof(hashAlgorithm));
                }
            }
#if FUTURE
            else if (padding == RSASignaturePadding.Pss)
            {
                if (hashAlgorithm == HashAlgorithmName.SHA256)
                {
                    algorithm = JsonWebKeySignatureAlgorithm.PS256;
                }
                else if (hashAlgorithm == HashAlgorithmName.SHA384)
                {
                    algorithm = JsonWebKeySignatureAlgorithm.PS384;
                }
                else if (hashAlgorithm == HashAlgorithmName.SHA512)
                {
                    algorithm = JsonWebKeySignatureAlgorithm.PS512;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(hashAlgorithm));
                }
            }
#endif
            else
            {
                Console.WriteLine("Error in SignDigestAsync " + padding + "." +
                    "Unsupported padding algorithm used.", new { padding });
                throw new ArgumentOutOfRangeException(nameof(padding));
            }

            var result = await _keyVaultClient.SignAsync(signingKey, algorithm, digest, ct).ConfigureAwait(false);
            return result.Result;
        }

        private CertificateAttributes CreateCertificateAttributes(
            X509Certificate2 certificate
            )
        {
            return CreateCertificateAttributes(certificate.NotBefore, certificate.NotAfter);
        }

        private CertificateAttributes CreateCertificateAttributes(
            DateTime notBefore,
            DateTime notAfter
            )
        {
            var attributes = new CertificateAttributes
            {
                Enabled = true,
                NotBefore = notBefore,
                Expires = notAfter
            };
            return attributes;
        }


        private CertificatePolicy CreateCertificatePolicy(
            X509Certificate2 certificate,
            bool selfSigned)
        {
            int keySize;
            using (RSA rsa = certificate.GetRSAPublicKey())
            {
                keySize = rsa.KeySize;
                return CreateCertificatePolicy(certificate.Subject, rsa.KeySize, selfSigned);
            }
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
                    KeyType = (_keyStoreHSM && !exportable) ? "RSA-HSM" : "RSA",
                    ReuseKey = reuseKey
                },
                SecretProperties = new SecretProperties
                {
                    ContentType = CertificateContentType.Pfx
                },
                X509CertificateProperties = new X509CertificateProperties
                {
                    Subject = subject
                }
            };
            return policy;
        }

        private string KeyStoreName(string id, string requestId)
        {
            return id + "Key" + requestId;
        }
        private string KeySecretName(string id, string requestId)
        {
            return id + "Key" + requestId;
        }
        private string CrlSecretName(string id, string thumbprint)
        {
            return id + "Crl" + thumbprint;
        }

        private string PrivateKeyFormatToContentType(string privateKeyFormat)
        {
            if (privateKeyFormat.Equals("PFX", StringComparison.OrdinalIgnoreCase))
            {
                return ContentTypePfx;
            }
            else if (privateKeyFormat.Equals("PEM", StringComparison.OrdinalIgnoreCase))
            {
                return ContentTypePem;
            }
            throw new Exception("Unknown Private Key format.");
        }

        private readonly string _groupSecret;
        private readonly string _vaultBaseUrl;
        private readonly bool _keyStoreHSM;
        private IKeyVaultClient _keyVaultClient;
        private ClientAssertionCertificate _assertionCert;
        private ClientCredential _clientCredential;
    }
}

