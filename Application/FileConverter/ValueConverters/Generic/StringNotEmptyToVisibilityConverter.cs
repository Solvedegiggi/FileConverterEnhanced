namespace FileConverter.ValueConverters.Generic
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
            bool hasText = !string.IsNullOrWhiteSpace(value as string);
            if (invert)
            {
                hasText = !hasText;
            }

            return hasText ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
