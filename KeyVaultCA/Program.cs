using CommandLine;
using System.IO;

namespace KeyVaultCA
{
    class Program
    {
        public class Options
        {
            [Option("appId", Required = true, HelpText = "AppId of the AAD service principal that can access KeyVault.")]
            public string AppId { get; set; }

            [Option("secret", Required = true, HelpText = "Password of the AAD service principal that can access KeyVault.")]
            public string Secret { get; set; }

            [Option("issuer", Required = true, HelpText = "Name of the issuing certificate in KeyVault.")]
            public string IssuerCertName { get; set; }

            [Option("csrPath", Required = true, HelpText = "Path to the CSR file in .der format")]
            public string PathToCsr { get; set; }

            [Option("output", Required = true, HelpText = "Output file name for the certificate")]
            public string OutputFileName { get; set; }

            [Option("kvName", Required = true, HelpText = "KeyVault name")]
            public string KeyVaultName { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       var keyVaultServiceClient = new KeyVaultServiceClient($"https://{o.KeyVaultName}.vault.azure.net/");

                       keyVaultServiceClient.SetAuthenticationClientCredential(o.AppId, o.Secret);

                       var kvCertProvider = new KeyVaultCertificateProvider(o.IssuerCertName, keyVaultServiceClient);
                       var csr = File.ReadAllBytes(o.PathToCsr);
                       var cert = kvCertProvider.SigningRequestAsync(csr).Result;

                       File.WriteAllBytes(o.OutputFileName, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
                   });
        }
    }
}
