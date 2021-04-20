using System;
using System.Collections.Generic;

namespace KeyVaultCA.Web
{
    public class CAConfuguration
    {
        public string KeyVaultName => Environment.GetEnvironmentVariable("KeyVaultName");

        public string AppId => Environment.GetEnvironmentVariable("AppId");

        public string Secret => Environment.GetEnvironmentVariable("Secret");

        public string IssuingCA => Environment.GetEnvironmentVariable("IssuingCA");

        public IList<string> CACerts 
        { 
            get 
            {
                return Environment.GetEnvironmentVariable("CACerts").Split(',');
            } 
        }

        public string EstUsername => Environment.GetEnvironmentVariable("EstUser");

        public string EstPassword => Environment.GetEnvironmentVariable("EstPassword");
    }
}
