namespace KeyVaultCA
{
    public class CsrConfiguration
    {
        public bool IsRootCA { get; set; }

        public string Subject { get; set; }

        public string PathToCsr { get; set; }

        public string OutputFileName { get; set; }
    }
}
