using System;
using System.Diagnostics;
using XrayUI.Helpers;
using XrayUI.ViewModels;
using WinUIEx;

namespace XrayUI.Views;

public sealed partial class CloudflareMonitorWindow
{
    public CloudflareMonitorWindow(CloudflareMonitorViewModel viewModel)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            InitializeComponent();
            Monitor.ViewModel = viewModel;
            Monitor.DataContext = viewModel;
            Title = "Cloudflare 账号监控";
            this.SetWindowSize(1080, 720);
            AppWindow.Title = Title;
            ThemeHelper.FollowAppTheme(this, WindowRoot);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CloudflareWindow] Construction failed: {ex}");
            Title = "Cloudflare 账号监控";
        }
    }
}
