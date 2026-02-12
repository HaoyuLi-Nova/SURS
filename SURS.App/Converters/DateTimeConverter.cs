using System;
using System.Globalization;
using System.Windows.Data;

namespace SURS.App.Converters
{
    /// <summary>
    /// DateTime 转换器：将 DateTime 转换为 24 小时制格式字符串
    /// </summary>
    public class DateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                // 使用 24 小时制格式：yyyy-MM-dd HH:mm:ss
                // HH 表示 24 小时制（00-23），hh 表示 12 小时制（01-12）
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && DateTime.TryParse(str, out DateTime result))
            {
                return result;
            }
            return value;
        }
    }
}

