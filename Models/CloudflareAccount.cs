using System;

namespace XrayUI.Models
{
    public class CloudflareAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string AccountTag { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
    }
}