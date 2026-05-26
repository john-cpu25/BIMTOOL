using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RincoNhan.Tools.RebarColumn.UI.Converters
{
    public class ScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                double scale = 1.0;
                double offset = 0;

                // Parameter format: "ScaleValue, Offset"
                if (parameter is string paramStr)
                {
                    var parts = paramStr.Split(',');
                    if (parts.Length > 0) double.TryParse(parts[0], out scale);
                    if (parts.Length > 1) double.TryParse(parts[1], out offset);
                }

                return (d * scale) + offset - 6; // Subtracting 6 to center the 12px ellipse
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
