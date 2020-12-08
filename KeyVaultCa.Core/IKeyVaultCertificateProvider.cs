using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVaultCa.Core
{
    public interface IKeyVaultCertificateProvider
    {
        Task CreateCACertificateAsync(string issuerCertificateName, string subject);

        Task<X509Certificate2> GetCertificateAsync(string issuerCertificateName);

        Task<X509Certificate2> SigningRequestAsync(byte[] certificateRequest, string issuerCertificateName);
    }
}