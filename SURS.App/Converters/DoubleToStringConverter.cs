using System;
using System.Globalization;
using System.Windows.Data;

namespace SURS.App.Converters
{
    public class DoubleToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 支持 int 类型
            if (value is int intValue)
            {
                // 如果值为0，返回空字符串以便显示占位符
                if (intValue == 0)
                {
                    return string.Empty;
                }
                return intValue.ToString(culture);
            }
            // 支持 double 类型
            if (value is double doubleValue)
            {
                // 如果值为0，返回空字符串以便显示占位符
                if (doubleValue == 0)
                {
                    return string.Empty;
                }
                return doubleValue.ToString(culture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    // 根据目标类型返回默认值
                    if (targetType == typeof(int) || targetType == typeof(int?))
                    {
                        return 0;
                    }
                    return 0.0;
                }
                // 尝试解析为 int
                if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    if (int.TryParse(stringValue, NumberStyles.Any, culture, out int intResult))
                    {
                        return intResult;
                    }
                    return 0;
                }
                // 尝试解析为 double
                if (double.TryParse(stringValue, NumberStyles.Any, culture, out double doubleResult))
                {
                    return doubleResult;
                }
            }
            // 根据目标类型返回默认值
            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return 0;
            }
            return 0.0;
        }
    }
}
