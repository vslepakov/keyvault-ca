using Org.BouncyCastle.Pkcs;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVaultCA
{
    internal class KeyVaultCertificateProvider
    {
        private readonly KeyVaultServiceClient _keyVaultServiceClient;
        private readonly string _certificateName;

        internal KeyVaultCertificateProvider(string certificateName, KeyVaultServiceClient keyVaultServiceClient)
        {
            _keyVaultServiceClient = keyVaultServiceClient;
            _certificateName = certificateName;
        }

        /// <summary>
        /// Creates a KeyVault signed certficate from signing request.
        /// </summary>
        internal async Task<X509Certificate2> SigningRequestAsync(byte[] certificateRequest)
        {
            var pkcs10CertificationRequest = new Pkcs10CertificationRequest(certificateRequest);
            if (!pkcs10CertificationRequest.Verify())
            {
                throw new ArgumentException("CSR signature invalid.");
            }

            var info = pkcs10CertificationRequest.GetCertificationRequestInfo();
            var notBefore = DateTime.UtcNow.AddDays(-1);

            var certBundle = await _keyVaultServiceClient.GetCertificateAsync(_certificateName).ConfigureAwait(false);

            var signingCert = new X509Certificate2(certBundle.Cer);
            var publicKey = KeyVaultCertFactory.GetRSAPublicKey(info.SubjectPublicKeyInfo);

            return await KeyVaultCertFactory.CreateSignedCertificate(
                info.Subject.ToString(),
                2048,
                notBefore,
                notBefore.AddMonths(12),
                256,
                signingCert,
                publicKey,
                new KeyVaultSignatureGenerator(_keyVaultServiceClient, certBundle.KeyIdentifier.Identifier, signingCert)
                );
        }
    }
}
