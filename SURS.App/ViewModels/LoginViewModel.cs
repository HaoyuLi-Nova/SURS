using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SURS.App.Services.License;

namespace SURS.App.ViewModels
{
    /// <summary>登录/首次激活页面的视图状态。密码由 PasswordBox 直接传入，避免明文绑定。</summary>
    public partial class LoginViewModel : ObservableObject
    {
        private readonly LicenseService _licenseService;

        [ObservableProperty]
        private string account = string.Empty;

        [ObservableProperty]
        private string cardNumber = string.Empty;

        [ObservableProperty]
        private string statusText = string.Empty;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        public LoginViewModel(LicenseService licenseService, LicenseCheckResult gateResult)
        {
            _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
            if (gateResult == null) throw new ArgumentNullException(nameof(gateResult));

            IsFirstRun = gateResult.Status == LicenseStatus.NotActivated || gateResult.Status == LicenseStatus.Expired;
            IsRenewal = gateResult.Status == LicenseStatus.Expired;
            Account = _licenseService.InstalledAccount ?? string.Empty;
            StatusText = gateResult.IsValid ? gateResult.Message : string.Empty;
        }

        public bool IsFirstRun { get; }
        public bool IsRenewal { get; }

        public string WindowTitle => IsRenewal ? "SURS 授权续期" : (IsFirstRun ? "SURS 首次激活" : "SURS 登录");

        public string Heading => IsRenewal ? "使用新卡续期并登录" : (IsFirstRun ? "激活本机授权并登录" : "登录 SURS");

        public string Subheading => IsRenewal
            ? "本机授权已到期。请输入发放方提供的新激活卡、账号和密码，续期窗口自本次激活当刻起算。"
            : (IsFirstRun
                ? "请输入发放方提供的激活卡号、账号和密码。授权自本次激活当刻起计时。"
                : "请输入本机构的授权账号和密码。");

        public string LoginButtonText => IsRenewal ? "续期并登录" : (IsFirstRun ? "激活并登录" : "登录");

        public bool TrySubmit(string password, out string error)
        {
            error = string.Empty;
            if (IsBusy)
            {
                error = "正在处理，请稍候。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Account))
            {
                error = IsFirstRun ? "请输入账号。" : "账号或密码错误。";
                ErrorMessage = error;
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                error = IsFirstRun ? "请输入密码。" : "账号或密码错误。";
                ErrorMessage = error;
                return false;
            }

            if (IsFirstRun && string.IsNullOrWhiteSpace(CardNumber))
            {
                error = "请输入激活卡号。";
                ErrorMessage = error;
                return false;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                if (IsFirstRun)
                {
                    var credentialCheck = _licenseService.ValidateActivationCredentials(
                        CardNumber, Account.Trim(), password);
                    if (!credentialCheck.IsValid)
                    {
                        error = credentialCheck.Status == LicenseStatus.InvalidCredentials
                            ? "账号或密码错误。"
                            : credentialCheck.Message;
                        ErrorMessage = error;
                        return false;
                    }

                    var activation = _licenseService.TryActivate(CardNumber);
                    if (!activation.IsValid)
                    {
                        error = activation.Message;
                        ErrorMessage = error;
                        return false;
                    }
                }

                var login = _licenseService.CheckLogin(Account.Trim(), password);
                if (!login.IsValid)
                {
                    error = login.Status == LicenseStatus.InvalidCredentials
                        ? "账号或密码错误。"
                        : login.Message;
                    ErrorMessage = error;
                    return false;
                }

                StatusText = login.Message;
                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnAccountChanged(string value) => ErrorMessage = string.Empty;
        partial void OnCardNumberChanged(string value) => ErrorMessage = string.Empty;
    }
}