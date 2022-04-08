namespace KeyVaultCa.Core
{
    public class EstConfiguration
    {
        public string KeyVaultUrl { get; set; }

        public string IssuingCA { get; set; }

        public int CertValidityInDays { get; set; }

        public int CertPathLength { get; set; }
    }
}
