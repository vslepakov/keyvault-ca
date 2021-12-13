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

        private readonly ILogger<EstController> _logger;
        private readonly IKeyVaultCertificateProvider _keyVaultCertProvider;
        private readonly CAConfiguration _confuguration;

        public EstController(ILogger<EstController> logger, IKeyVaultCertificateProvider keyVaultCertProvider, CAConfiguration confuguration)
        {
            _logger = logger;
            _keyVaultCertProvider = keyVaultCertProvider;
            _confuguration = confuguration;
        }

        [HttpGet]
        [Authorize]
        [Route(".well-known/est/cacerts")]
        [Route("ca/.well-known/est/cacerts")]
        public async Task<IActionResult> GetCACertsAsync()
        {
            var caCerts = await _keyVaultCertProvider.GetPublicCertificatesByName(new [] { _confuguration.IssuingCA });
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
            var cleanedUpBody = await GetAsn1StructureFromBody();

            var caCert = Request.Path.StartsWithSegments("/ca");

            var cert = await _keyVaultCertProvider.SigningRequestAsync(
                Convert.FromBase64String(cleanedUpBody), _confuguration.IssuingCA, _confuguration.CertValidityInDays, caCert);

            var pkcs7 = EncodeCertificatesAsPkcs7(new[] { cert });
            return Content(pkcs7, PKCS7_MIME_TYPE);
        }

        private string EncodeCertificatesAsPkcs7(X509Certificate2 [] certs)
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

            // Need to handle different types of Line Breaks (e.g. Linux to this Api running on Windows
            var tokens = body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            return string.Join("", tokens.Skip(1).Take(tokens.Length - 3));
        }
    }
}
