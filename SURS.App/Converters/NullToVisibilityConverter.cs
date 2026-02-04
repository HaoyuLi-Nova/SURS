using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SURS.App.Converters
{
    /// <summary>
    /// 将null转换为Visibility的转换器
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool invert = parameter?.ToString() == "Invert";
            
            if (invert)
            {
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 将字符串转换为Visibility的转换器（空字符串或null时隐藏）
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEmpty = string.IsNullOrWhiteSpace(value?.ToString());
            bool invert = parameter?.ToString() == "Invert";
            
            if (invert)
            {
                return isEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
            return isEmpty ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

