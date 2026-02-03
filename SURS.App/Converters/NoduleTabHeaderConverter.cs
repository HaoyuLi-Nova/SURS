using System;
using System.Globalization;
using System.Windows.Data;

namespace SURS.App.Converters
{
    public class NoduleTabHeaderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int idx)
                return $"结节{idx + 1}";
            return "结节";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

