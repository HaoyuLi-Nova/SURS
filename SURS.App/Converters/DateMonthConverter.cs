using System;
using System.Globalization;
using System.Windows.Data;

namespace SURS.App.Converters
{
    public class DateMonthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                return date.Month.ToString("D2");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing; // 由DateInput_TextChanged处理
        }
    }
}
