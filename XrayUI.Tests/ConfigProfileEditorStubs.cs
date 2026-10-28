global using CommunityToolkit.Mvvm.Input;
global using Microsoft.UI.Xaml;

// The editor state machine is tested without a Windows App SDK runtime or user files.
namespace Microsoft.UI.Xaml
{
    public sealed class XamlRoot { }
}

namespace Microsoft.UI.Xaml.Controls
{
    public enum InfoBarSeverity { Informational, Success, Warning, Error }
}

namespace XrayUI.Services
{
    public sealed class SettingsService
    {
        public Models.AppSettings Settings { get; } = new();
        public Func<Task<Models.AppSettings>>? Load { get; set; }
        public Func<Models.AppSettings, Task<bool>> Save { get; set; } = _ => Task.FromResult(true);
        public Task<Models.AppSettings> LoadSettingsAsync() => Load?.Invoke() ?? Task.FromResult(Settings);
        public Task<bool> SaveSettingsAsync(Models.AppSettings settings) => Save(settings);
    }

    public sealed class ConfigProfileStore
    {
        public Func<bool, Task<string?>> Read { get; set; } = _ => Task.FromResult<string?>(null);
        public Func<bool, string, Task> Write { get; set; } = (_, _) => Task.CompletedTask;
        public Task<string?> ReadAsync(bool tunSlot) => Read(tunSlot);
        public Task WriteAsync(bool tunSlot, string text) => Write(tunSlot, text);
        public static void OpenFolder() { }
    }

    public interface IDialogService
    {
        Task<bool> ShowConfirmationAsync(string title, string message, bool isDanger = false, XamlRoot? xamlRoot = null);
        Task ShowErrorAsync(string title, string message, XamlRoot? xamlRoot = null);
    }

    public sealed class ProfileTestDialogs : IDialogService
    {
        public Func<Task<bool>> Confirm { get; set; } = () => Task.FromResult(true);
        public Task<bool> ShowConfirmationAsync(string title, string message, bool isDanger = false, XamlRoot? xamlRoot = null) => Confirm();
        public Task ShowErrorAsync(string title, string message, XamlRoot? xamlRoot = null) => Task.CompletedTask;
    }

}
