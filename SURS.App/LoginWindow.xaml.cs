using System;
using System.Windows;
using SURS.App.Common;
using SURS.App.Services.License;
using SURS.App.ViewModels;

namespace SURS.App
{
    /// <summary>
    /// 首次激活 / 登录 / 到期续期窗口。
    /// 首次或续期显示激活卡号、账号、密码；已有有效授权时只显示账号、密码。
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly ILogger? _logger;

        public LoginWindow(LicenseService licenseService, LicenseCheckResult gateResult, ILogger? logger = null)
        {
            InitializeComponent();
            _ = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
            _logger = logger;
            DataContext = new LoginViewModel(licenseService, gateResult);
            Loaded += (_, _) =>
            {
                if (DataContext is LoginViewModel { IsCardInputVisible: true })
                {
                    CardInput.Focus();
                }
                else if (string.IsNullOrEmpty(AccountInput.Text))
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
                if (viewModel.TrySubmit(CardInput.Text, PasswordInput.Password, out _))
                {
                    DialogResult = true;
                    return;
                }

                PasswordInput.SelectAll();
                PasswordInput.Focus();
            }
            catch (Exception ex)
            {
                _logger?.LogError("登录或激活过程发生异常", ex);
                MessageBox.Show(
                    "登录或激活时发生错误，请检查输入后重试。\n\n" + ex.Message,
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