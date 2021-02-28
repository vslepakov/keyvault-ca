﻿using Org.BouncyCastle.Pkcs;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVaultCa.Core
{
    public class KeyVaultCertificateProvider : IKeyVaultCertificateProvider
    {
        private readonly KeyVaultServiceClient _keyVaultServiceClient;

        public KeyVaultCertificateProvider(KeyVaultServiceClient keyVaultServiceClient)
        {
            _keyVaultServiceClient = keyVaultServiceClient;
        }

        public async Task CreateCACertificateAsync(string issuerCertificateName, string subject, int pathLengthConstraint, TimeSpan validity)
        {
            var certVersions = await _keyVaultServiceClient.GetCertificateVersionsAsync(issuerCertificateName).ConfigureAwait(false);

            if (!certVersions.Any())
            {
                var notBefore = DateTime.UtcNow.AddDays(-1);
                await _keyVaultServiceClient.CreateCACertificateAsync(
                        issuerCertificateName,
                        subject,
                        notBefore,
                        notBefore.Add(validity), 
                        4096, 
                        256,
                        pathLengthConstraint);
            }
        }

        public async Task<X509Certificate2> GetCertificateAsync(string issuerCertificateName)
        {
            var certBundle = await _keyVaultServiceClient.GetCertificateAsync(issuerCertificateName).ConfigureAwait(false);
            return new X509Certificate2(certBundle.Cer);
        }

        /// <summary>
        /// Creates a KeyVault signed certficate from signing request.
        /// </summary>
        public async Task<X509Certificate2> SigningRequestAsync(byte[] certificateRequest, string issuerCertificateName,
            bool isIntermediateCA, int pathLengthConstraint, TimeSpan validity)
        {
            var pkcs10CertificationRequest = new Pkcs10CertificationRequest(certificateRequest);
            if (!pkcs10CertificationRequest.Verify())
            {
                throw new ArgumentException("CSR signature invalid.");
            }

            var info = pkcs10CertificationRequest.GetCertificationRequestInfo();
            var notBefore = DateTime.UtcNow.AddDays(-1);

            var certBundle = await _keyVaultServiceClient.GetCertificateAsync(issuerCertificateName).ConfigureAwait(false);

            var signingCert = new X509Certificate2(certBundle.Cer);
            var publicKey = KeyVaultCertFactory.GetRSAPublicKey(info.SubjectPublicKeyInfo);

            return await KeyVaultCertFactory.CreateSignedCertificate(
                info.Subject.ToString(),
                2048,
                notBefore,
                notBefore.Add(validity),
                256,
                signingCert,
                publicKey,
                new KeyVaultSignatureGenerator(_keyVaultServiceClient, certBundle.KeyIdentifier.Identifier, signingCert),
                caCert: false,
                intermediateCACert: isIntermediateCA,
                pathLengthConstraint: pathLengthConstraint
                );
        }
    }
}
