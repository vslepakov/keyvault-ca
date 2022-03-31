namespace KeyVaultCa.Core
{
    public class EstConfiguration
    {
        public string KeyVaultUrl { get; set; }

        public string AppId { get; set; }

        public string Secret { get; set; }

        public string IssuingCA { get; set; }

        public int CertValidityInDays { get; set; } = 365;
    }
}
