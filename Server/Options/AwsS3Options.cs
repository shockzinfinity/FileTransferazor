namespace FileTransferazor.Server.Options
{
    public class AwsS3Options
    {
        public const string SectionName = "AwsS3";

        public string BucketName { get; set; } = string.Empty;
    }
}
