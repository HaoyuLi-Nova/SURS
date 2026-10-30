using System.Windows;
using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using SURS.App.Common;
using SURS.App.Services;
using SURS.App.Services.License;
using SURS.App.Converters;

namespace SURS.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly ILogger _logger = new FileLogger();
    private readonly IDialogService _dialogService = new DialogService();

    private LicenseService? _licenseService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 登录窗口关闭到主窗口显示之间没有活动窗口，必须显式控制退出。
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        _logger.LogInfo("应用程序启动");

        try
        {
            _licenseService = new LicenseService(_logger);
            var gate = _licenseService.StartupGate();

            // 门禁分派：
            //   Valid / NotActivated / Expired → 进入登录（首次激活/续期）页面；
            //   其它（Tampered / ClockRollback / StorageError …）→ 提示原因并退出，绝不进入 UI。
            if (gate.Status is not (LicenseStatus.Valid or LicenseStatus.NotActivated or LicenseStatus.Expired))
            {
                _logger.LogWarning("启动门禁拒绝进入：" + gate.Status + "；" + gate.Message);
                _dialogService.ShowError("SURS 授权", gate.Message);
                Shutdown(1);
                return;
            }

            if (gate.Status == LicenseStatus.Expired)
            {
                _logger.LogWarning("授权已到期，进入续期流程：" + gate.Message);
            }


            var loginWindow = new LoginWindow(_licenseService, gate, _logger);
            if (loginWindow.ShowDialog() != true)
            {
                _logger.LogInfo("用户取消登录，应用程序退出");
                Shutdown(0);
                return;
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Closed += (_, _) => Shutdown(0);
            mainWindow.Show();
            _logger.LogInfo("登录成功，主窗口已打开");
        }
        catch (Exception ex)
        {
            _logger.LogError("启动授权/登录流程失败", ex);
            _dialogService.ShowError("SURS 启动失败",
                "无法完成授权校验或登录，软件将退出。\n\n" + ex.Message + "\n\n详细信息已记录到日志。");
            Shutdown(1);
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError("未处理的 UI 线程异常", e.Exception);
        _dialogService.ShowError("应用程序错误",
            $"应用程序发生错误:\n{e.Exception.Message}\n\n详细信息已记录到日志文件。");
        e.Handled = true; // 标记为已处理，防止应用崩溃
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            _logger.LogError("未处理的应用程序域异常", ex);
            _dialogService.ShowError("严重错误",
                $"应用程序发生严重错误:\n{ex.Message}\n\n详细信息已记录到日志文件。");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger.LogInfo("应用程序退出");
        base.OnExit(e);
    }

    private void RadioButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not RadioButton radio || radio.IsChecked != true)
        {
            return;
        }

        var bindingExpression = BindingOperations.GetBindingExpression(radio, ToggleButton.IsCheckedProperty);
        if (bindingExpression == null)
        {
            return;
        }

        var resolvedSource = bindingExpression.ResolvedSource;
        var resolvedSourcePropertyName = bindingExpression.ResolvedSourcePropertyName;
        if (resolvedSource == null || string.IsNullOrWhiteSpace(resolvedSourcePropertyName))
        {
            return;
        }

        var property = resolvedSource.GetType().GetProperty(resolvedSourcePropertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || !property.CanWrite)
        {
            return;
        }

        // For bool properties using RadioBoolConverter with True/False, keep existing behavior.
        var parentBinding = bindingExpression.ParentBinding;
        if (property.PropertyType == typeof(bool) &&
            parentBinding?.Converter is RadioBoolConverter &&
            IsTrueFalseParameter(parentBinding.ConverterParameter))
        {
            return;
        }

        object? clearValue = GetClearValue(property.PropertyType);
        property.SetValue(resolvedSource, clearValue);
        radio.IsChecked = false;
        e.Handled = true;
    }

    private static bool IsTrueFalseParameter(object? parameter)
    {
        var text = parameter?.ToString();
        return string.Equals(text, "True", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(text, "False", StringComparison.OrdinalIgnoreCase);
    }

    private static object? GetClearValue(Type propertyType)
    {
        if (propertyType == typeof(string))
        {
            return string.Empty;
        }

        var underlyingNullable = Nullable.GetUnderlyingType(propertyType);
        if (underlyingNullable != null)
        {
            return null;
        }

        if (propertyType.IsValueType)
        {
            return Activator.CreateInstance(propertyType);
        }

        return null;
    }
}