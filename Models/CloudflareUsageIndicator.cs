namespace XrayUI.Models
{
    public sealed class CloudflareUsageIndicator
    {
        public string Name { get; init; } = string.Empty;
        public string Usage { get; init; } = string.Empty;
        public bool IsVisible { get; init; }
        public bool IsGreen { get; init; }
        public bool IsYellow { get; init; }
        public bool IsRed { get; init; }
        public string ToolTip => $"{Name}：占限额 {Usage}";
    }
}