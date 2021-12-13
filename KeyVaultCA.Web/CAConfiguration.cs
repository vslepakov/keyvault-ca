using System;
using System.Collections.Generic;

namespace KeyVaultCA.Web
{
    public enum AuthMode
    {
        Basic = 0,
        x509 = 1
    }

    public class CAConfiguration
    {
        public string KeyVaultName => Environment.GetEnvironmentVariable("KeyVaultName");

        public string AppId => Environment.GetEnvironmentVariable("AppId");

        public string Secret => Environment.GetEnvironmentVariable("Secret");

        public string IssuingCA => Environment.GetEnvironmentVariable("IssuingCA");

        public string EstUsername => Environment.GetEnvironmentVariable("EstUser");

        public string EstPassword => Environment.GetEnvironmentVariable("EstPassword");

        public int CertValidityInDays => int.Parse(Environment.GetEnvironmentVariable("CertValidityInDays"));

        public AuthMode AuthMode => (AuthMode)Enum.Parse(typeof(AuthMode), Environment.GetEnvironmentVariable("AuthMode"));
    }
}
