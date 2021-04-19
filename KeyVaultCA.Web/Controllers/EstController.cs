using KeyVaultCa.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace KeyVaultCA.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EstController : ControllerBase
    {
        private readonly ILogger<EstController> _logger;
        private readonly IKeyVaultCertificateProvider _keyVaultCertProvider;
        private readonly CAConfuguration _confuguration;

        public EstController(ILogger<EstController> logger, IKeyVaultCertificateProvider keyVaultCertProvider, CAConfuguration confuguration)
        {
            _logger = logger;
            _keyVaultCertProvider = keyVaultCertProvider;
            _confuguration = confuguration;
        }

        [HttpGet]
        [Route("cacerts")]
        public async Task<IEnumerable<string>> GetCACertsAsync()
        {
            var caCerts = new List<string>();

            foreach (var issuerName in _confuguration.Issuers)
            {
                var cert = await _keyVaultCertProvider.GetCertificateAsync(issuerName).ConfigureAwait(false);

                if (cert != null)
                {
                    caCerts.Add(EncodeCertificateAsPem(cert));
                }
            }

            return caCerts;
        }

        [HttpPost]
        [Route("simpleenroll")]
        [Consumes("application/pkcs10")]
        public async Task<string> EnrollAsync()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var cleanedUpBody = CleanUpAsn1Structure(body);
            var cert = await _keyVaultCertProvider.SigningRequestAsync(Convert.FromBase64String(cleanedUpBody), "ContosoRootCA");
            return EncodeCertificateAsPem(cert);
        }

        private string EncodeCertificateAsPem(X509Certificate2 cert)
        {
            var builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");

            return builder.ToString();
        }

        private string CleanUpAsn1Structure(string raw)
        {
            var tokens = raw.Split(Environment.NewLine);
            return string.Join("", tokens.Skip(1).Take(tokens.Length - 3));
        }
    }
}
