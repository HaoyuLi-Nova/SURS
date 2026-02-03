using System;
using System.Globalization;
using System.Windows.Data;

namespace SURS.App.Converters
{
    public class DoubleToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
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
                    return 0.0;
                }
                if (double.TryParse(stringValue, NumberStyles.Any, culture, out double result))
                {
                    return result;
                }
            }
            return 0.0;
        }
    }
}
