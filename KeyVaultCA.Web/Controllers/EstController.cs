using KeyVaultCa.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace KeyVaultCA.Web.Controllers
{
    [ApiController]
    public class EstController : ControllerBase
    {
        private const string PKCS7_MIME_TYPE = "application/pkcs7-mime";
        private const string PKCS10_MIME_TYPE = "application/pkcs10";

        private readonly ILogger _logger;
        private readonly IKeyVaultCertificateProvider _keyVaultCertProvider;
        private readonly CAConfiguration _configuration;

        public EstController(ILogger<EstController> logger, IKeyVaultCertificateProvider keyVaultCertProvider, CAConfiguration configuration)
        {
            _logger = logger;
            _keyVaultCertProvider = keyVaultCertProvider;
            _configuration = configuration;
        }

        [HttpGet]
        [Authorize]
        [Route(".well-known/est/cacerts")]
        [Route("ca/.well-known/est/cacerts")]
        public async Task<IActionResult> GetCACertsAsync()
        {
            _logger.LogDebug("Call 'CA certs' endpoint.");
            var caCerts = await _keyVaultCertProvider.GetPublicCertificatesByName(new[] { _configuration.IssuingCA });
            var pkcs7 = EncodeCertificatesAsPkcs7(caCerts.ToArray());

            return Content(pkcs7, PKCS7_MIME_TYPE);
        }

        [HttpPost]
        [Authorize]
        [Route(".well-known/est/simpleenroll")]
        [Route("ca/.well-known/est/simpleenroll")]
        [Consumes(PKCS10_MIME_TYPE)]
        public async Task<IActionResult> EnrollAsync()
        {
            _logger.LogDebug("Call 'Simple Enroll' endpoint.");

            var cleanedUpBody = await GetAsn1StructureFromBody();

            _logger.LogDebug("Request body is: {body}.", cleanedUpBody);

            var caCert = Request.Path.StartsWithSegments("/ca");

            _logger.LogInformation("Is a CA certificate: {flag}.", caCert);

            var cert = await _keyVaultCertProvider.SigningRequestAsync(
                Convert.FromBase64String(cleanedUpBody), _configuration.IssuingCA, _configuration.CertValidityInDays, caCert);

            var pkcs7 = EncodeCertificatesAsPkcs7(new[] { cert });
            return Content(pkcs7, PKCS7_MIME_TYPE);
        }

        private string EncodeCertificatesAsPkcs7(X509Certificate2[] certs)
        {
            var collection = new X509Certificate2Collection(certs);
            var data = collection.Export(X509ContentType.Pkcs7);

            var builder = new StringBuilder();
            builder.AppendLine(Convert.ToBase64String(data));

            return builder.ToString();
        }

        private async Task<string> GetAsn1StructureFromBody()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            // Need to handle different types of Line Breaks
            var tokens = body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var token = tokens.Length > 1 ? string.Join(string.Empty, tokens) : tokens.FirstOrDefault();

            _logger.LogDebug("Returning token: {token} ", token);

            return token;
        }
    }
}
