namespace SURS.App.Common
{
    /// <summary>
    /// 日志服务接口
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 记录信息日志
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// 记录警告日志
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// 记录错误日志
        /// </summary>
        void LogError(string message, Exception? exception = null);

        /// <summary>
        /// 记录调试日志
        /// </summary>
        void LogDebug(string message);
    }
}

