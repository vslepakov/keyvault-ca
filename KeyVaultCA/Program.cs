using System;
using System.IO;

namespace KeyVaultCA
{
    class Program
    {
        static void Main(string[] args)
        {
            var keyVaultServiceClient = new KeyVaultServiceClient("https://my-kv.vault.azure.net/");

            var appId = Environment.GetEnvironmentVariable("APP_ID");
            var appSecret = Environment.GetEnvironmentVariable("APP_SECRET");
            keyVaultServiceClient.SetAuthenticationClientCredential(appId, appSecret);

            var kvCertProvider = new KeyVaultCertificateProvider("DeviceRootCA", keyVaultServiceClient);
            var csr = File.ReadAllBytes("my-test-device-csr.der");
            var cert = kvCertProvider.SigningRequestAsync(csr).Result;

            File.WriteAllBytes("cert.cer", cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));
        }
    }
}
