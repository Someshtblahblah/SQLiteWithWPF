using System;
using System.Globalization;
using System.Windows.Data;

namespace Desktop.ImportTool.Converters
{
    public class BoolToHiddenConverter : IValueConverter
    {
        // If visible, IsHidden = false; if not visible, IsHidden = true
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !(value is bool b) || !b;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool hidden && !hidden;
    }
}