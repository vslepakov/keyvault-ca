using CommandLine;
using KeyVaultCa.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KeyVaultCA
{
    class Program
    {
        public class Options
        {
            // General options for the KeyVault access

            [Option("appId", Required = true, HelpText = "AppId of the AAD service principal that can access KeyVault.")]
            public string AppId { get; set; }

            [Option("secret", Required = true, HelpText = "Password of the AAD service principal that can access KeyVault.")]
            public string Secret { get; set; }

            [Option("kvName", Required = true, HelpText = "KeyVault name")]
            public string KeyVaultName { get; set; }

            // Certificates

            [Option("issuercert", Required = true, HelpText = "Name of the issuing certificate in KeyVault.")]
            public string IssuerCertName { get; set; }

            // Options for the end entity certificate

            [Option("csrPath", Required = false, HelpText = "Path to the CSR file in .der format")]
            public string PathToCsr { get; set; }

            [Option("output", Required = false, HelpText = "Output file name for the certificate")]
            public string OutputFileName { get; set; }

            // Options for Root CA creation

            [Option("ca", Required = false, HelpText = "Should register Root CA")]
            public bool IsRootCA { get; set; }

            [Option("subject", Required = false, HelpText = "Subject in the format 'C=US, ST=WA, L=Redmond, O=Contoso, OU=Contoso HR, CN=Contoso Inc'")]
            public string Subject { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(o =>
                   {
                       StartAsync(o).Wait();
                   });
        }

        private static async Task StartAsync(Options o)
        {
            var keyVaultServiceClient = new KeyVaultServiceClient($"https://{o.KeyVaultName}.vault.azure.net/");
            keyVaultServiceClient.SetAuthenticationClientCredential(o.AppId, o.Secret);
            var kvCertProvider = new KeyVaultCertificateProvider(keyVaultServiceClient);

            if (o.IsRootCA)
            {
                if (string.IsNullOrEmpty(o.Subject))
                {
                    throw new ArgumentException("Subject is not provided.");
                }
                    
                // Generate issuing certificate in KeyVault
                await kvCertProvider.CreateCACertificateAsync(o.IssuerCertName, o.Subject);
            }
            else
            {
                if(string.IsNullOrEmpty(o.PathToCsr) || string.IsNullOrEmpty(o.OutputFileName))
                {
                    throw new ArgumentException("Path to CSR or the Output Filename is not provided.");
                }

                // Issue device certificate
                var csr = File.ReadAllBytes(o.PathToCsr);
                var cert = await kvCertProvider.SigningRequestAsync(csr, o.IssuerCertName, 365);

                File.WriteAllBytes(o.OutputFileName, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
            }
        }
    }
}
