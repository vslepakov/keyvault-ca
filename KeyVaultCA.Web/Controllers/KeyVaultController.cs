using KeyVaultCa.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVaultCA.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class KeyVaultController : ControllerBase
    {
        private readonly ILogger<KeyVaultController> _logger;
        private readonly IKeyVaultCertificateProvider _keyVaultCertProvider;
        private readonly CAConfuguration _confuguration;

        public KeyVaultController(ILogger<KeyVaultController> logger, IKeyVaultCertificateProvider keyVaultCertProvider, CAConfuguration confuguration)
        {
            _logger = logger;
            _keyVaultCertProvider = keyVaultCertProvider;
            _confuguration = confuguration;
        }

        [HttpGet]
        [Route("cacerts")]
        public async Task<IEnumerable<byte[]>> CACerts()
        {
            var caCerts = new List<byte[]>();

            foreach (var issuerName in _confuguration.Issuers) 
            {
                var cert = await _keyVaultCertProvider.GetCertificateAsync(issuerName).ConfigureAwait(false);

                if(cert != null)
                {
                    caCerts.Add(cert.Export(X509ContentType.Cert));
                } 
            }

            return caCerts;
        }

        [HttpPost]
        [Route("enroll")]
        public async Task<byte[]> Enroll(byte[] certificateRequest, string issuerCertificateName)
        {
            var cert = await _keyVaultCertProvider.SigningRequestAsync(certificateRequest, issuerCertificateName,
                isIntermediateCA: false, pathLengthConstraint: 0);
            return cert.Export(X509ContentType.Cert);
        }

        [HttpPost]
        [Route("reenroll")]
        public async Task<byte[]> Reenroll(byte[] certificateRequest, string issuerCertificateName)
        {
            return await Enroll(certificateRequest, issuerCertificateName);
        }
    }
}
