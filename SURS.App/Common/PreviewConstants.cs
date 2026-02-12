namespace SURS.App.Common
{
    /// <summary>
    /// 预览相关常量
    /// </summary>
    public static class PreviewConstants
    {
        /// <summary>
        /// 基准缩放值：0.25 对应 100% 显示
        /// </summary>
        public const double BaseZoom = 0.25;

        /// <summary>
        /// 最小缩放值
        /// </summary>
        public const double MinZoom = 0.25;

        /// <summary>
        /// 最大缩放值
        /// </summary>
        public const double MaxZoom = 3.0;

        /// <summary>
        /// 缩放步进（相对于基准值的 10%）
        /// </summary>
        public const double ZoomStep = 0.025; // BaseZoom * 0.1

        /// <summary>
        /// 预览刷新节流时间（毫秒）
        /// </summary>
        public const int ThrottleMs = 300;

        /// <summary>
        /// 预览图片 DPI
        /// </summary>
        public const int PreviewDpi = 288;
    }
}

