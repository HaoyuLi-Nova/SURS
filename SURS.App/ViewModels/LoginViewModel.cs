using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SURS.App.Services.License;

namespace SURS.App.ViewModels
{
    /// <summary>
    /// 登录 / 首次激活 / 到期续期的视图状态。
    /// 首次或续期时显示“激活卡号 + 账号 + 密码”，已有有效授权时只显示“账号 + 密码”。
    /// 密码由 PasswordBox 直接传入，避免明文回写绑定。
    /// </summary>
    public partial class LoginViewModel : ObservableObject
    {
        private const string CredentialError = "账号或密码错误。";
        private readonly LicenseService _licenseService;

        [ObservableProperty]
        private string cardCode = string.Empty;

        [ObservableProperty]
        private string account = string.Empty;

        [ObservableProperty]
        private string statusText = string.Empty;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        public LoginViewModel(LicenseService licenseService, LicenseCheckResult gateResult)
        {
            _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
            if (gateResult == null)
            {
                throw new ArgumentNullException(nameof(gateResult));
            }

            IsRenewal = gateResult.Status == LicenseStatus.Expired;
            IsActivation = gateResult.Status == LicenseStatus.NotActivated;
            Account = IsActivation ? string.Empty : (_licenseService.InstalledAccount ?? string.Empty);
            StatusText = gateResult.Message;
            if (IsActivation)
            {
                // 随发布包放在 exe 同目录的 license.card 会自动带入卡号与账号（只读预填，不消费）。
                var bundle = LicenseService.ProbeBundledCard();
                if (!string.IsNullOrEmpty(bundle.Card))
                {
                    CardCode = bundle.Card;
                    if (!string.IsNullOrEmpty(bundle.Account))
                    {
                        Account = bundle.Account;
                    }
                }
            }

        }

        /// <summary>本机尚未激活（需要首次激活）。</summary>
        public bool IsActivation { get; }

        /// <summary>本机已到期（需要输入新卡续期）。</summary>
        public bool IsRenewal { get; }

        /// <summary>首次激活或到期续期时显示激活卡号输入框。</summary>
        public bool IsCardInputVisible => IsActivation || IsRenewal;

        public string WindowTitle => IsRenewal ? "SURS 授权续期" : (IsActivation ? "SURS 首次激活" : "SURS 登录");

        public string Heading => IsRenewal ? "授权已到期，请使用新卡续期" : (IsActivation ? "首次激活并登录" : "登录 SURS");

        public string Subheading => IsRenewal
            ? "本机授权已到期，软件当前停用。请输入发放方新签发的激活卡号、账号和密码；新窗口自本次续期当刻重新起算。"
            : (IsActivation
                ? "请输入交付单上的激活卡号、账号和密码。授权自本机首次激活当刻起算，到期后需使用新卡续期。"
                : "请输入发放方为本机构配置的账号与密码。每次启动均需登录。");

        public string LoginButtonText => IsRenewal ? "续期并登录" : (IsActivation ? "激活并登录" : "登录");

        /// <summary>
        /// 提交凭证。首次激活/续期先预校验卡内账号密码，校验通过后才消费卡并开窗；
        /// 已有效授权只做账号密码登录。凭证不匹配统一提示“账号或密码错误”。
        /// </summary>
        public bool TrySubmit(string cardCode, string password, out string error)
        {
            error = string.Empty;
            if (IsBusy)
            {
                error = "正在处理，请稍候。";
                ErrorMessage = error;
                return false;
            }

            var account = (Account ?? string.Empty).Trim();
            var card = (cardCode ?? string.Empty).Trim();
            if (IsCardInputVisible && string.IsNullOrEmpty(card))
            {
                error = "请输入激活卡号、账号和密码。";
                ErrorMessage = error;
                return false;
            }

            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
            {
                error = IsCardInputVisible ? "请输入激活卡号、账号和密码。" : CredentialError;
                ErrorMessage = error;
                return false;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                if (IsCardInputVisible)
                {
                    // 关键顺序：先验证卡内账号/密码，再消费卡；错误密码不会开启窗口。
                    var credentialCheck = _licenseService.ValidateActivationCredentials(card, account, password);
                    if (!credentialCheck.IsValid)
                    {
                        error = credentialCheck.Status == LicenseStatus.InvalidCredentials
                            ? CredentialError
                            : MessageOrGeneric(credentialCheck);
                        ErrorMessage = error;
                        return false;
                    }

                    var activation = _licenseService.TryActivate(card);
                    if (!activation.IsValid)
                    {
                        error = MessageOrGeneric(activation);
                        ErrorMessage = error;
                        return false;
                    }
                }

                var login = _licenseService.CheckLogin(account, password);
                if (!login.IsValid)
                {
                    error = login.Status == LicenseStatus.InvalidCredentials
                        ? CredentialError
                        : MessageOrGeneric(login);
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

        private static string MessageOrGeneric(LicenseCheckResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Message))
            {
                return "操作未完成，请检查输入后重试。";
            }

            return result.Status == LicenseStatus.InvalidCredentials ? CredentialError : result.Message;
        }

        partial void OnAccountChanged(string value) => ErrorMessage = string.Empty;

        partial void OnCardCodeChanged(string value) => ErrorMessage = string.Empty;
    }
}