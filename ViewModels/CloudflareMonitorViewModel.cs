using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XrayUI.Models;
using XrayUI.Services;

namespace XrayUI.ViewModels
{
    public partial class CloudflareMonitorViewModel : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly CloudflareMonitorService _service = new();
        private readonly System.Threading.SemaphoreSlim _refreshLock = new(1, 1);
        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _timer;
        public ObservableCollection<CloudflareAccount> Accounts { get; } = new();
        public ObservableCollection<CloudflareAccountSnapshot?> Snapshots { get; } = new();
        public ObservableCollection<CloudflareAccountStatus> Rows { get; } = new();

        [ObservableProperty] public partial CloudflareAccount? SelectedAccount { get; set; }
        [ObservableProperty] public partial string AccountName { get; set; } = string.Empty;
        [ObservableProperty] public partial string AccountTag { get; set; } = string.Empty;
        [ObservableProperty] public partial string ApiToken { get; set; } = string.Empty;
        [ObservableProperty] public partial string StatusText { get; private set; } = "尚未查询";
        [ObservableProperty] public partial string AlertText { get; private set; } = string.Empty;
        public bool HasAlert => !string.IsNullOrEmpty(AlertText);
        [ObservableProperty] public partial bool IsBusy { get; private set; }
        [ObservableProperty] public partial int RefreshIntervalMinutes { get; set; } = 30;
        [ObservableProperty] public partial double DailyLimit { get; set; } = 100000;
        [ObservableProperty] public partial bool AlertsEnabled { get; set; } = true;

        public CloudflareMonitorViewModel(SettingsService settings) => _settings = settings;

        partial void OnSelectedAccountChanged(CloudflareAccount? value)
        {
            if (value is null) return;
            AccountName = value.Name;
            AccountTag = value.AccountTag;
            ApiToken = value.ApiToken;
        }

        public async Task LoadAsync()
        {
            var settings = await _settings.LoadSettingsAsync();
            Accounts.Clear();
            foreach (var account in settings.CloudflareAccounts ?? new()) Accounts.Add(account);
            RefreshIntervalMinutes = settings.CloudflareRefreshIntervalMinutes is 5 or 15 or 30 or 60
                ? settings.CloudflareRefreshIntervalMinutes : 30;
            DailyLimit = settings.CloudflareDailyLimit > 0 ? settings.CloudflareDailyLimit : 100000;
            AlertsEnabled = settings.CloudflareAlertsEnabled;
            RebuildSnapshots();
            if (Accounts.Count > 0) SelectedAccount = Accounts[0];
        }

        [RelayCommand]
        private async Task SaveAccountAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(AccountName) || string.IsNullOrWhiteSpace(AccountTag) || string.IsNullOrWhiteSpace(ApiToken))
                {
                    StatusText = "账号名称、Account ID 和 API Token 不能为空";
                    return;
                }

                var name = AccountName.Trim();
                var accountTag = AccountTag.Trim().Trim('\"', ',', ';').Trim();
                var apiToken = ApiToken.Trim();
                var account = SelectedAccount;
                if (account is null)
                {
                    account = new CloudflareAccount();
                }
                account.Name = name;
                account.AccountTag = accountTag;
                account.ApiToken = apiToken;
                if (!Accounts.Contains(account))
                    Accounts.Add(account);
                SelectedAccount = account;
                AccountTag = account.AccountTag;
                await PersistAsync();
                RebuildSnapshots();
                StatusText = "账号配置已保存";
            }
            catch (Exception ex)
            {
                StatusText = $"保存账号失败：{ex.Message}";
            }
        }

        [RelayCommand]
        private async Task NewAccountAsync()
        {
            SelectedAccount = null;
            AccountName = AccountTag = ApiToken = string.Empty;
            StatusText = "请输入新账号配置";
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task RemoveAccountAsync()
        {
            if (SelectedAccount is null) return;
            Accounts.Remove(SelectedAccount);
            SelectedAccount = Accounts.FirstOrDefault();
            await PersistAsync();
            RebuildSnapshots();
        }

        [RelayCommand]
        private Task RefreshAsync() => RefreshCoreAsync();

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            RefreshIntervalMinutes = RefreshIntervalMinutes is 5 or 15 or 30 or 60 ? RefreshIntervalMinutes : 30;
            DailyLimit = double.IsFinite(DailyLimit) && DailyLimit > 0 ? Math.Round(DailyLimit) : 100000;
            await PersistAsync();
            if (_timer is not null) _timer.Interval = TimeSpan.FromMinutes(RefreshIntervalMinutes);
            StatusText = "监控设置已保存";
        }

        private async Task RefreshCoreAsync()
        {
            if (!await _refreshLock.WaitAsync(0)) return;
            IsBusy = true;
            AlertText = string.Empty;
            OnPropertyChanged(nameof(HasAlert));
            try
            {
                RebuildSnapshots();
                var accounts = Accounts.ToArray();
                var results = await Task.WhenAll(accounts.Select(account => _service.QueryAsync(account)));
                for (var i = 0; i < results.Length; i++)
                    Snapshots[i] = results[i];
                UpdateRows();
                var failures = Snapshots.Count(x => x is { IsSuccess: false });
                var overLimit = Snapshots.Any(x => x is { IsSuccess: true } && x.TotalRequests >= DailyLimit);
                StatusText = $"已查询 {DateTime.Now:HH:mm:ss}，成功 {Accounts.Count - failures}/{Accounts.Count}";
                if (AlertsEnabled && (overLimit || failures > 0))
                    AlertText = overLimit ? "提醒：至少一个账号已达到每日请求限额。" : "提醒：至少一个 Cloudflare 账号查询失败，请检查配置或网络。";
                OnPropertyChanged(nameof(HasAlert));
            }
            finally
            {
                IsBusy = false;
                _refreshLock.Release();
            }
        }

        public void StartScheduler()
        {
            if (_timer is not null) return;
            _timer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.CreateTimer();
            if (_timer is null) return;
            _timer.Interval = TimeSpan.FromMinutes(RefreshIntervalMinutes);
            _timer.IsRepeating = true;
            _timer.Tick += (_, _) => _ = RefreshCoreAsync();
            _timer.Start();
            _ = RefreshCoreAsync();
        }

        public void StopScheduler()
        {
            _timer?.Stop();
            _timer = null;
        }

        private async Task PersistAsync()
        {
            var settings = await _settings.LoadSettingsAsync();
            settings.CloudflareAccounts = Accounts.ToList();
            settings.CloudflareRefreshIntervalMinutes = RefreshIntervalMinutes;
            settings.CloudflareDailyLimit = (long)DailyLimit;
            settings.CloudflareAlertsEnabled = AlertsEnabled;
            await _settings.SaveSettingsAsync(settings);
        }

        private void RebuildSnapshots()
        {
            while (Snapshots.Count < Accounts.Count) Snapshots.Add(null);
            while (Snapshots.Count > Accounts.Count) Snapshots.RemoveAt(Snapshots.Count - 1);
            UpdateRows();
        }

        private void UpdateRows()
        {
            Rows.Clear();
            for (var i = 0; i < Accounts.Count; i++)
            {
                var snapshot = i < Snapshots.Count ? Snapshots[i] : null;
                var row = new CloudflareAccountStatus
                {
                    Name = Accounts[i].Name,
                    AccountTag = Accounts[i].AccountTag,
                    Status = snapshot is null ? "未查询" : snapshot.IsSuccess ? $"UTC {snapshot.QueriedAt:HH:mm}" : snapshot.Error ?? "查询失败"
                };
                if (snapshot is { IsSuccess: true })
                {
                    row.Requests = snapshot.TotalRequests.ToString("N0");
                    row.Workers = snapshot.WorkersRequests.ToString("N0");
                    row.Pages = snapshot.PagesRequests.ToString("N0");
                    row.Usage = $"{Math.Min(snapshot.TotalRequests * 100d / DailyLimit, 100):F1}%";
                }
                Rows.Add(row);
            }
        }
    }
}