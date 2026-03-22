namespace FileTransferazor.Server.Options;

public class TusOptions
{
    public const string SectionName = "Tus";
    public string StoragePath { get; set; } = Path.Combine(Path.GetTempPath(), "tusfiles");
    public long MaxFileSizeInBytes { get; set; } = 10L * 1024 * 1024 * 1024;
    public int ExpirationHours { get; set; } = 24;
}
