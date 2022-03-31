using System;

namespace KeyVaultCA.Web.Auth
{
    public enum AuthMode
    {
        Basic = 0,
        x509 = 1
    }

    public class AuthConfiguration
    {
        public string EstUsername { get; set; }

        public string EstPassword { get; set; }

        public string Auth { get; set; }

        public AuthMode AuthMode => (AuthMode)Enum.Parse(typeof(AuthMode), Auth);
    }
}
