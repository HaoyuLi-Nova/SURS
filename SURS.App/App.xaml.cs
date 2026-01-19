using System.Windows;
using System;

namespace SURS.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 处理未捕获的异常
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        string errorMessage = $"未处理的异常: {e.Exception.Message}\n\n堆栈跟踪:\n{e.Exception.StackTrace}";
        System.IO.File.WriteAllText("error.log", errorMessage);
        MessageBox.Show($"应用程序发生错误:\n{e.Exception.Message}\n\n详细信息已保存到 error.log", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            string errorMessage = $"未处理的异常: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}";
            System.IO.File.WriteAllText("error.log", errorMessage);
            MessageBox.Show($"应用程序发生严重错误:\n{ex.Message}\n\n详细信息已保存到 error.log", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

