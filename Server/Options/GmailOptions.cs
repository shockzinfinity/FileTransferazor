namespace FileTransferazor.Server.Options
{
    public class GmailOptions
    {
        public const string SectionName = "Gmail";

        public string GmailUser { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string CredentialFilePath { get; set; } = string.Empty;
        public string ServiceAccountCredentialFilePath { get; set; } = string.Empty;
        public string ServiceAccountEmail { get; set; } = string.Empty;
    }
}
