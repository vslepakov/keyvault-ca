using CommandLine;
using KeyVaultCa.Core;
using Microsoft.Extensions.Logging;
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

            [Option("kvUrl", Required = true, HelpText = "Key Vault URL")]
            public string KeyVaultUrl { get; set; }

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
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("KeyVaultCa.Program", LogLevel.Information)
                    .AddFilter("KeyVaultCa.Core", LogLevel.Information)
                    .AddConsole();
            });

            ILogger logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation("KeyVaultCA app started.");

            var keyVaultServiceClient = new KeyVaultServiceClient(o.KeyVaultUrl);
            keyVaultServiceClient.SetAuthenticationClientCredential(o.AppId, o.Secret);
            var kvCertProvider = new KeyVaultCertificateProvider(keyVaultServiceClient, loggerFactory.CreateLogger<KeyVaultCertificateProvider>());

            if (o.IsRootCA)
            {
                if (string.IsNullOrEmpty(o.Subject))
                {
                    logger.LogError("Certificate subject is not provided.");
                    throw new ArgumentException("Subject is not provided.");
                }
                    
                // Generate issuing certificate in KeyVault
                await kvCertProvider.CreateCACertificateAsync(o.IssuerCertName, o.Subject);
                logger.LogInformation("CA certificate was created successfully and can be found in the Key Vault {kvUrl}.", o.KeyVaultUrl);
            }
            else
            {
                if(string.IsNullOrEmpty(o.PathToCsr) || string.IsNullOrEmpty(o.OutputFileName))
                {
                    logger.LogError("Path to CSR or the Output Filename is not provided.");
                    throw new ArgumentException("Path to CSR or the Output Filename is not provided.");
                }

                // Issue device certificate
                var csr = File.ReadAllBytes(o.PathToCsr);
                var cert = await kvCertProvider.SigningRequestAsync(csr, o.IssuerCertName, 365);

                File.WriteAllBytes(o.OutputFileName, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
                logger.LogInformation("Device certificate was created successfully.");
            }
        }
    }
}
