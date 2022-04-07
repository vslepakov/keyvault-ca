using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVaultCa.Core
{
    public interface IKeyVaultCertificateProvider
    {
        Task CreateCACertificateAsync(string issuerCertificateName, string subject, int certPathLength);

        Task<IList<X509Certificate2>> GetPublicCertificatesByName(IEnumerable<string> certNames);

        Task<X509Certificate2> GetCertificateAsync(string issuerCertificateName);

        Task<X509Certificate2> SignRequestAsync(byte[] certificateRequest, string issuerCertificateName, int validityInDays, bool caCert = false);
    }
}