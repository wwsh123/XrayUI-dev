using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using XrayUI.Models;

namespace XrayUI.Services
{
    public sealed class CloudflareMonitorService
    {
        private const string Endpoint = "https://api.cloudflare.com/client/v4/graphql";
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

        private const string Query = """
            query Viewer($act: string, $start: String, $end: String) {
              viewer {
                accounts(filter: { accountTag: $act }) {
                  workers: workersInvocationsAdaptive(limit: 100, filter: { datetime_geq: $start, datetime_leq: $end }) {
                    sum { requests }
                  }
                  pages: pagesFunctionsInvocationsAdaptiveGroups(limit: 100, filter: { datetime_geq: $start, datetime_leq: $end }) {
                    sum { requests }
                  }
                }
              }
            }
            """;

        public async Task<CloudflareAccountSnapshot> QueryAsync(
            CloudflareAccount account,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(account.AccountTag) || string.IsNullOrWhiteSpace(account.ApiToken))
                return Failure("请填写 Cloudflare Account ID 和 API Token");

            var now = DateTimeOffset.UtcNow;
            var body = new JsonObject
            {
                ["query"] = Query,
                ["variables"] = new JsonObject
                {
                    ["act"] = account.AccountTag.Trim(),
                    ["start"] = now.UtcDateTime.Date.ToString("O"),
                    ["end"] = now.UtcDateTime.Date.AddDays(1).ToString("O")
                }
            }.ToJsonString();

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", account.ApiToken.Trim());
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            try
            {
                using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return Failure($"Cloudflare API 返回 {(int)response.StatusCode}");

                using var document = JsonDocument.Parse(content);
                if (document.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                    return Failure(errors[0].GetProperty("message").GetString() ?? "GraphQL 查询失败");

                var accounts = document.RootElement.GetProperty("data").GetProperty("viewer").GetProperty("accounts");
                if (accounts.GetArrayLength() == 0)
                    return Failure("API 未返回该账号，请检查 Account ID 和 Token 权限");

                var accountData = accounts[0];
                return new CloudflareAccountSnapshot
                {
                    IsSuccess = true,
                    WorkersRequests = SumRequests(accountData, "workers"),
                    PagesRequests = SumRequests(accountData, "pages"),
                    QueriedAt = now
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Failure(ex.Message);
            }
        }

        private static long SumRequests(JsonElement account, string propertyName)
        {
            long total = 0;
            if (!account.TryGetProperty(propertyName, out var groups)) return 0;
            foreach (var group in groups.EnumerateArray())
            {
                if (group.TryGetProperty("sum", out var sum)
                    && sum.TryGetProperty("requests", out var requests)
                    && requests.ValueKind == JsonValueKind.Number)
                    total += requests.GetInt64();
            }
            return total;
        }

        private static CloudflareAccountSnapshot Failure(string message) => new()
        {
            Error = message,
            QueriedAt = DateTimeOffset.UtcNow
        };
    }
}