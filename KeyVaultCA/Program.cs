using Azure.Identity;
using KeyVaultCa.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KeyVaultCA
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddCommandLine(args)
                .AddEnvironmentVariables()
                .Build();

            await CreateCertificate(config);
        }

        private static async Task CreateCertificate(IConfiguration config)
        {
            var estConfig = config.GetSection("KeyVault").Get<EstConfiguration>();
            var csrConfig = config.GetSection("Csr").Get<CsrConfiguration>();

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

            var keyVaultServiceClient = new KeyVaultServiceClient(estConfig, new DefaultAzureCredential(), loggerFactory.CreateLogger<KeyVaultServiceClient>());
            var kvCertProvider = new KeyVaultCertificateProvider(keyVaultServiceClient, loggerFactory.CreateLogger<KeyVaultCertificateProvider>());

            if (csrConfig.IsRootCA)
            {
                if (string.IsNullOrEmpty(csrConfig.Subject))
                {
                    logger.LogError("Certificate subject is not provided.");
                    Environment.Exit(1);
                }

                // Generate issuing certificate in KeyVault
                await kvCertProvider.CreateCACertificateAsync(estConfig.IssuingCA, csrConfig.Subject, estConfig.CertPathLength);
                logger.LogInformation("CA certificate was either created successfully or it already existed in the Key Vault {kvUrl}.", estConfig.KeyVaultUrl);
            }
            else
            {
                if (string.IsNullOrEmpty(csrConfig.PathToCsr) || string.IsNullOrEmpty(csrConfig.OutputFileName))
                {
                    logger.LogError("Path to CSR or the Output Filename is not provided.");
                    Environment.Exit(1);
                }

                if (estConfig.CertValidityInDays <= 0 || estConfig.CertValidityInDays > 365)
                {
                    logger.LogError("Number of days specified as the certificate validity period should be between 1 and 365.");
                    Environment.Exit(1);
                }

                // Issue device certificate
                var csr = File.ReadAllBytes(csrConfig.PathToCsr);
                var cert = await kvCertProvider.SignRequestAsync(csr, estConfig.IssuingCA, estConfig.CertValidityInDays);

                File.WriteAllBytes(csrConfig.OutputFileName, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
                logger.LogInformation("Device certificate was created successfully.");
            }
        }
    }
}
