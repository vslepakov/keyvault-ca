using KeyVaultCa.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        public async Task<IEnumerable<string>> CACerts()
        {
            var caCerts = new List<string>();

            foreach (var issuerName in _confuguration.Issuers) 
            {
                var cert = await _keyVaultCertProvider.GetCertificateAsync(issuerName).ConfigureAwait(false);

                if(cert != null)
                {
                    caCerts.Add(EncodeCertificateAsPem(cert));
                } 
            }

            return caCerts;
        }

        [HttpPost]
        [Route("enroll")]
        public async Task<string> Enroll(Csr csr)
        {
            var cert = await _keyVaultCertProvider.SigningRequestAsync(Convert.FromBase64String(csr.CertificateRequest), csr.IssuerCertificateName);
            return EncodeCertificateAsPem(cert);
        }

        [HttpPost]
        [Route("reenroll")]
        public async Task<string> Reenroll(Csr csr)
        {
            return await Enroll(csr);
        }

        private string EncodeCertificateAsPem(X509Certificate2 cert)
        {
            var builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");

            return builder.ToString();
        }
    }
}
