using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SURS.App.Converters
{
    /// <summary>
    /// 根据O-RADS级别返回对应的颜色
    /// </summary>
    public class ORadsLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                return level switch
                {
                    0 => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // 灰色 - 不适用
                    1 => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // 绿色 - 正常
                    2 => new SolidColorBrush(Color.FromRgb(139, 195, 74)),  // 浅绿色 - 几乎良性
                    3 => new SolidColorBrush(Color.FromRgb(255, 193, 7)),    // 黄色 - 低风险
                    4 => new SolidColorBrush(Color.FromRgb(255, 152, 0)),     // 橙色 - 中高风险
                    5 => new SolidColorBrush(Color.FromRgb(244, 67, 54)),   // 红色 - 高风险
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
                };
            }

            if (value is Models.ORadsResult result)
            {
                return Convert(result.Level, targetType, parameter, culture);
            }

            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 根据O-RADS级别返回对应的背景颜色（浅色）
    /// </summary>
    public class ORadsLevelToBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                return level switch
                {
                    0 => new SolidColorBrush(Color.FromRgb(245, 245, 245)), // 浅灰色
                    1 => new SolidColorBrush(Color.FromRgb(232, 245, 233)), // 浅绿色
                    2 => new SolidColorBrush(Color.FromRgb(240, 248, 235)),  // 浅绿色
                    3 => new SolidColorBrush(Color.FromRgb(255, 249, 196)),  // 浅黄色
                    4 => new SolidColorBrush(Color.FromRgb(255, 243, 224)),  // 浅橙色
                    5 => new SolidColorBrush(Color.FromRgb(255, 235, 238)), // 浅红色
                    _ => new SolidColorBrush(Color.FromRgb(245, 245, 245))
                };
            }

            if (value is Models.ORadsResult result)
            {
                return Convert(result.Level, targetType, parameter, culture);
            }

            return new SolidColorBrush(Color.FromRgb(245, 245, 245));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 根据O-RADS级别返回对应的边框颜色
    /// </summary>
    public class ORadsLevelToBorderColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                return level switch
                {
                    0 => new SolidColorBrush(Color.FromRgb(189, 189, 189)), // 灰色
                    1 => new SolidColorBrush(Color.FromRgb(76, 175, 80)),  // 绿色
                    2 => new SolidColorBrush(Color.FromRgb(139, 195, 74)), // 浅绿色
                    3 => new SolidColorBrush(Color.FromRgb(255, 193, 7)),  // 黄色
                    4 => new SolidColorBrush(Color.FromRgb(255, 152, 0)),  // 橙色
                    5 => new SolidColorBrush(Color.FromRgb(244, 67, 54)),  // 红色
                    _ => new SolidColorBrush(Color.FromRgb(189, 189, 189))
                };
            }

            if (value is Models.ORadsResult result)
            {
                return Convert(result.Level, targetType, parameter, culture);
            }

            return new SolidColorBrush(Color.FromRgb(189, 189, 189));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

