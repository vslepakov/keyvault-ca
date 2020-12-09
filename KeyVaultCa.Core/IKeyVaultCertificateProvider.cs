using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVaultCa.Core
{
    public interface IKeyVaultCertificateProvider
    {
        Task CreateCACertificateAsync(string issuerCertificateName, string subject, int pathLengthConstraint);

        Task<X509Certificate2> GetCertificateAsync(string issuerCertificateName);

        Task<X509Certificate2> SigningRequestAsync(byte[] certificateRequest, string issuerCertificateName, bool isIntermediateCA, int pathLengthConstraint);
    }
}