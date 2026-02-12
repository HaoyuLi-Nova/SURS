namespace SURS.App.Services
{
    /// <summary>
    /// 对话框服务接口：统一管理对话框，便于测试和替换实现
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// 显示确认对话框
        /// </summary>
        bool ShowConfirm(string title, string message);

        /// <summary>
        /// 显示信息对话框
        /// </summary>
        void ShowMessage(string title, string message);

        /// <summary>
        /// 显示错误对话框
        /// </summary>
        void ShowError(string title, string message);

        /// <summary>
        /// 显示文件选择对话框（单选）
        /// </summary>
        string? ShowOpenFileDialog(string filter);

        /// <summary>
        /// 显示文件选择对话框（多选）
        /// </summary>
        string[]? ShowOpenFileDialogMultiple(string filter);

        /// <summary>
        /// 显示文件保存对话框
        /// </summary>
        string? ShowSaveFileDialog(string filter, string defaultExt, string fileName);
    }
}

