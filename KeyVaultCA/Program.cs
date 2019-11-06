using System;

namespace KeyVaultCA
{
    class Program
    {
        static void Main(string[] args)
        {
            var keyVaultServiceClient = new KeyVaultServiceClient("todo");
            keyVaultServiceClient.SetAuthenticationClientCredential("todo", "todo");

            var kvCertProvider = new KeyVaultCertificateProvider("todo", keyVaultServiceClient);
            var cert = kvCertProvider.SigningRequestAsync(null).Result;

            //TODO
        }
    }
}
