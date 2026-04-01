namespace SmartFactoryWebApi.Options
{
    public class AppUpdateOptions
    {
        public bool Enabled { get; set; } = true;
        public string DefaultChannel { get; set; } = "prod";
        public List<UpdateReleaseOptions> Releases { get; set; } = new();
    }

    public class UpdateReleaseOptions
    {
        public string AppId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Channel { get; set; } = "prod";
        public string VersionName { get; set; } = string.Empty;
        public int VersionCode { get; set; }
        public int MinSupportedVersionCode { get; set; }
        public bool IsMandatory { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string? ReleaseNotes { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
    }
}
