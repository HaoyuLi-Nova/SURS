using System.Windows;
using Microsoft.Win32;

namespace SURS.App.Services
{
    /// <summary>
    /// 对话框服务实现
    /// </summary>
    public class DialogService : IDialogService
    {
        public bool ShowConfirm(string title, string message)
        {
            var result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            return result == MessageBoxResult.Yes;
        }

        public void ShowMessage(string title, string message)
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        public void ShowError(string title, string message)
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        public string? ShowOpenFileDialog(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string[]? ShowOpenFileDialogMultiple(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = true
            };

            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        public string? ShowSaveFileDialog(string filter, string defaultExt, string fileName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt,
                FileName = fileName
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}

