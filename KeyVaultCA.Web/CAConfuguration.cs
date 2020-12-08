using System;
using System.Collections.Generic;

namespace KeyVaultCA.Web
{
    public class CAConfuguration
    {
        public string KeyVaultName => Environment.GetEnvironmentVariable("KeyVaultName");

        public string AppId => Environment.GetEnvironmentVariable("AppId");

        public string Secret => Environment.GetEnvironmentVariable("Secret");

        public IList<string> Issuers 
        { 
            get 
            {
                return Environment.GetEnvironmentVariable("Issuers").Split(',');
            } 
        }
    }
}
