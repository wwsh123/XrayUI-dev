using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using XrayUI.Services;
using XrayUI.ViewModels;

namespace XrayUI.Views
{
    public sealed partial class CloudflareMonitorControl : UserControl
    {
        private CloudflareMonitorViewModel? _viewModel;
        public CloudflareMonitorViewModel ViewModel
        {
            get => _viewModel ??= new CloudflareMonitorViewModel(new SettingsService());
            set
            {
                if (ReferenceEquals(_viewModel, value)) return;
                if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel = value;
                DataContext = _viewModel;
                if (IsLoaded) AttachViewModel();
            }
        }

        public CloudflareMonitorControl()
        {
            InitializeComponent();
            Loaded += (_, _) => AttachViewModel();
            Unloaded += (_, _) =>
            {
                if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            };
        }

        private void AttachViewModel()
        {
            if (_viewModel is null) return;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
            TokenBox.Password = _viewModel.ApiToken;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CloudflareMonitorViewModel.ApiToken)
                && _viewModel is not null && TokenBox.Password != _viewModel.ApiToken)
                TokenBox.Password = _viewModel.ApiToken;
        }

        private void TokenBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel is not null && _viewModel.ApiToken != TokenBox.Password)
                _viewModel.ApiToken = TokenBox.Password;
        }

        private async void SaveAccountButton_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.SaveAccountCommand.ExecuteAsync(null);
        }
    }
}