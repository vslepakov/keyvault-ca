﻿using CommandLine;
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

            [Option("secret", Required = false, HelpText = "Password of the AAD service principal that can access KeyVault.")]
            public string Secret { get; set; }

            [Option("kvName", Required = true, HelpText = "KeyVault name")]
            public string KeyVaultName { get; set; }

            [Option("deviceAuth", Required = false, HelpText = "Use device authentication instead of client secret")]
            public bool UseDeviceAuth { get; set; }

            // Certificates

            [Option("issuercert", Required = true, HelpText = "Name of the issuing certificate in KeyVault.")]
            public string IssuerCertName { get; set; }

            [Option("validity", Required = false, HelpText = "Validity of the issued certificate in months (default 12)")]
            public int ValidityMonths { get; set; } = 12;

            // Options for the end entity certificate

            [Option("csrPath", Required = false, HelpText = "Path to the CSR file in .der format")]
            public string PathToCsr { get; set; }

            [Option("output", Required = false, HelpText = "Output file name for the certificate")]
            public string OutputFileName { get; set; }

            [Option("intermediate", Required = false, HelpText = "Is the certificate allowed to act as an intermediate certificate authority")]
            public bool IsIntermediateCA { get; set; }

            [Option("maxPathLength", Required = false, HelpText = "Maximum number of intermediate certificates that can follow this certificate")]
            public int PathLengthConstraint { get; set; }

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
            if (!o.UseDeviceAuth && string.IsNullOrEmpty(o.Secret))
                throw new ArgumentException("If device authentication is not used, a client secret must be provided.");

            var now = DateTime.Now;
            var validity = now.AddMonths(o.ValidityMonths).Subtract(now);

            var keyVaultServiceClient = o.UseDeviceAuth
                ? new KeyVaultServiceClient($"https://{o.KeyVaultName}.vault.azure.net/", o.AppId)
                : new KeyVaultServiceClient($"https://{o.KeyVaultName}.vault.azure.net/", o.AppId, o.Secret);
            var kvCertProvider = new KeyVaultCertificateProvider(keyVaultServiceClient);

            if (o.IsRootCA)
            {
                if (string.IsNullOrEmpty(o.Subject))
                {
                    throw new ArgumentException("Subject is not provided.");
                }
                    
                // Generate issuing certificate in KeyVault
                await kvCertProvider.CreateCACertificateAsync(o.IssuerCertName, o.Subject, o.PathLengthConstraint, validity);
            }
            else
            {
                if(string.IsNullOrEmpty(o.PathToCsr) || string.IsNullOrEmpty(o.OutputFileName))
                {
                    throw new ArgumentException("Path to CSR or the Output Filename is not provided.");
                }

                // Issue device certificate or intermediate certificate
                var csr = File.ReadAllBytes(o.PathToCsr);
                var cert = await kvCertProvider.SigningRequestAsync(csr, o.IssuerCertName, o.IsIntermediateCA, o.PathLengthConstraint, validity);

                File.WriteAllBytes(o.OutputFileName, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
            }
        }
    }
}
