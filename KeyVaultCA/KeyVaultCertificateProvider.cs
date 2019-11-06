using Org.BouncyCastle.Pkcs;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace KeyVaultCA
{
    internal class KeyVaultCertificateProvider
    {
        /// <summary>
        /// Creates a KeyVault signed certficate from signing request.
        /// </summary>
        internal async Task<X509Certificate2> SigningRequestAsync(string[] domainNames, byte[] certificateRequest)
        {
            var pkcs10CertificationRequest = new Pkcs10CertificationRequest(certificateRequest);
            if (!pkcs10CertificationRequest.Verify())
            {
                throw new ArgumentException("CSR signature invalid.");
            }

            var info = pkcs10CertificationRequest.GetCertificationRequestInfo();
            var notBefore = DateTime.UtcNow.AddDays(-1);

            var signingCert = new X509Certificate2();
            {
                var publicKey = KeyVaultCertFactory.GetRSAPublicKey(info.SubjectPublicKeyInfo);
                return await KeyVaultCertFactory.CreateSignedCertificate(
                    info.Subject.ToString(),
                    Configuration.DefaultCertificateKeySize,
                    notBefore,
                    notBefore.AddMonths(Configuration.DefaultCertificateLifetime),
                    Configuration.DefaultCertificateHashSize,
                    signingCert,
                    publicKey,
                    new KeyVaultSignatureGenerator(_keyVaultServiceClient, _caCertKeyIdentifier, signingCert),
                    extensionUrl: authorityInformationAccess
                    );
            }
        }
    }
}
