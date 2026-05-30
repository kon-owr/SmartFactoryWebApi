namespace SmartFactoryWebApi.DTO
{
    public class UpdateCheckResponse
    {
        public bool HasUpdate { get; set; }
        public bool ForceUpdate { get; set; }
        public string LatestVersionName { get; set; } = string.Empty;
        public int LatestVersionCode { get; set; }
        public int MinSupportedVersionCode { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string? ReleaseNotes { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
    }
}
