using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace SURS.App.Converters
{
    public class ImageIndexConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return "图";
            if (values[0] is ItemsControl itemsControl && values[1] != null)
            {
                int index = itemsControl.Items.IndexOf(values[1]);
                if (index >= 0)
                {
                    return $"图 {index + 1}";
                }
            }
            return "图";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing };
        }
    }
}
