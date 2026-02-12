using System.Windows;
using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using SURS.App.Common;
using SURS.App.Services;
using SURS.App.Converters;

namespace SURS.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly ILogger _logger = new FileLogger();
    private readonly IDialogService _dialogService = new DialogService();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 处理未捕获的异常
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        
        _logger.LogInfo("应用程序启动");
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
        var resolvedProperty = bindingExpression.ResolvedSourcePropertyName;
        if (resolvedSource == null || string.IsNullOrWhiteSpace(resolvedProperty))
        {
            return;
        }

        var property = resolvedSource.GetType().GetProperty(resolvedProperty, BindingFlags.Instance | BindingFlags.Public);
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

