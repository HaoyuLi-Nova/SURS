using System;
using System.Windows;
using SURS.App.Common;
using SURS.App.Services.License;
using SURS.App.ViewModels;

namespace SURS.App
{
    public partial class LoginWindow : Window
    {
        private readonly LicenseService _licenseService;
        private readonly ILogger? _logger;

        public LoginWindow(LicenseService licenseService, LicenseCheckResult gateResult, ILogger? logger = null)
        {
            InitializeComponent();
            _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
            _logger = logger;
            DataContext = new LoginViewModel(licenseService, gateResult);
            Loaded += (_, _) =>
            {
                if (string.IsNullOrEmpty(AccountInput.Text))
                {
                    AccountInput.Focus();
                }
                else
                {
                    PasswordInput.Focus();
                }
            };
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LoginViewModel viewModel)
            {
                return;
            }

            LoginButton.IsEnabled = false;
            try
            {
                if (viewModel.TrySubmit(PasswordInput.Password, out _))
                {
                    if (viewModel.IsFirstRun)
                    {
                        MessageBox.Show(
                            viewModel.StatusText,
                            viewModel.IsRenewal ? "SURS 续期成功" : "SURS 激活成功",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    DialogResult = true;
                    return;
                }

                PasswordInput.SelectAll();
                PasswordInput.Focus();
            }
            catch (Exception ex)
            {
                _logger?.LogError("登录/激活过程发生异常", ex);
                MessageBox.Show(
                    "登录或激活时发生错误，请检查授权数据后重试。\n\n" + ex.Message,
                    "SURS 授权",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}