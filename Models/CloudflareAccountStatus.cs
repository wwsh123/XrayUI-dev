namespace XrayUI.Models
{
    public sealed class CloudflareAccountStatus
    {
        public string Name { get; init; } = string.Empty;
        public string AccountTag { get; init; } = string.Empty;
        public string Requests { get; set; } = "-";
        public string Workers { get; set; } = "-";
        public string Pages { get; set; } = "-";
        public string Usage { get; set; } = "-";
        public string Status { get; set; } = "未查询";
    }
}