// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Azure.Identity;
using System.Threading;
using Azure.Security.KeyVault.Keys.Cryptography;
using Polly;

namespace KeyVaultCa.Core
{
    /// <summary>
    /// The X509 signature generator to sign a digest with a KeyVault key.
    /// </summary>
    public class KeyVaultSignatureGenerator : X509SignatureGenerator
    {
        private X509Certificate2 _issuerCert;
        private DefaultAzureCredential _credential;
        private readonly Uri _signingKey;

        /// <summary>
        /// Create the KeyVault signature generator.
        /// </summary>
        /// <param name="keyVaultServiceClient">The KeyVault service client to use</param>
        /// <param name="signingKey">The KeyVault signing key</param>
        /// <param name="issuerCertificate">The issuer certificate used for signing</param>
        public KeyVaultSignatureGenerator(
            DefaultAzureCredential credential,
            Uri signingKey,
            X509Certificate2 issuerCertificate)
        {
            _issuerCert = issuerCertificate;
            _credential = credential;
            _signingKey = signingKey;
        }

        /// <summary>
        /// Callback to sign a digest with KeyVault key.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="hashAlgorithm"></param>
        /// <returns></returns>
        public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm)
        {
            HashAlgorithm hash;
            if (hashAlgorithm == HashAlgorithmName.SHA256)
            {
                hash = SHA256.Create();
            }
            else if (hashAlgorithm == HashAlgorithmName.SHA384)
            {
                hash = SHA384.Create();
            }
            else if (hashAlgorithm == HashAlgorithmName.SHA512)
            {
                hash = SHA512.Create();
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), "The hash algorithm " + hashAlgorithm.Name + " is not supported.");
            }
            var digest = hash.ComputeHash(data);
            var resultKeyVaultPkcs = SignDigestAsync(_signingKey, digest, hashAlgorithm, RSASignaturePadding.Pkcs1).GetAwaiter().GetResult();
            return resultKeyVaultPkcs;
        }

        protected override PublicKey BuildPublicKey()
        {
            return _issuerCert.PublicKey;
        }

        internal static PublicKey BuildPublicKey(RSA rsa)
        {
            if (rsa == null)
            {
                throw new ArgumentNullException(nameof(rsa));
            }
            // function is never called
            return null;
        }

        public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm)
        {
            byte[] oidSequence;

            if (hashAlgorithm == HashAlgorithmName.SHA256)
            {
                //const string RsaPkcs1Sha256 = "1.2.840.113549.1.1.11";
                oidSequence = new byte[] { 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 11, 5, 0 };
            }
            else if (hashAlgorithm == HashAlgorithmName.SHA384)
            {
                //const string RsaPkcs1Sha384 = "1.2.840.113549.1.1.12";
                oidSequence = new byte[] { 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 12, 5, 0 };
            }
            else if (hashAlgorithm == HashAlgorithmName.SHA512)
            {
                //const string RsaPkcs1Sha512 = "1.2.840.113549.1.1.13";
                oidSequence = new byte[] { 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 13, 5, 0 };
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), "The hash algorithm " + hashAlgorithm.Name + " is not supported.");
            }
            return oidSequence;
        }

        /// <summary>
        /// Sign a digest with the signing key.
        /// </summary>
        public async Task<byte[]> SignDigestAsync(
            Uri signingKey,
            byte[] digest,
            HashAlgorithmName hashAlgorithm,
            RSASignaturePadding padding,
            CancellationToken ct = default)
        {
            SignatureAlgorithm algorithm;

            if (padding == RSASignaturePadding.Pkcs1)
            {
                if (hashAlgorithm == HashAlgorithmName.SHA256)
                {
                    algorithm = SignatureAlgorithm.RS256;
                }
                else if (hashAlgorithm == HashAlgorithmName.SHA384)
                {
                    algorithm = SignatureAlgorithm.RS384;
                }
                else if (hashAlgorithm == HashAlgorithmName.SHA512)
                {
                    algorithm = SignatureAlgorithm.RS512;
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

            // create a client for performing cryptographic operations on Key Vault
            var cryptoClient = new CryptographyClient(signingKey, _credential);

            SignResult result = null;

            Random jitterer = new();

            var retryPolicy = await Policy
              .Handle<Exception>() // etc
              .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))  // exponential back-off: 2, 4, 8 etc
                               + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000))) // plus some jitter: up to 1 second                                                                                                  
              .ExecuteAndCaptureAsync(async () =>
              {
                  result = await cryptoClient.SignAsync(algorithm, digest, ct).ConfigureAwait(false);
              });

            return result.Signature;
        }
    }
}
