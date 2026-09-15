using System;

namespace XrayUI.Models
{
    public sealed class CloudflareAccountSnapshot
    {
        public bool IsSuccess { get; init; }
        public string? Error { get; init; }
        public long WorkersRequests { get; init; }
        public long PagesRequests { get; init; }
        public long TotalRequests => WorkersRequests + PagesRequests;
        public DateTimeOffset QueriedAt { get; init; }
    }
}