using System;
using System.Globalization;
using System.Windows.Data;

namespace DesktopWeather.Models;

public sealed class WeatherModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value.ToString() == parameter.ToString();
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && Enum.TryParse(parameter.ToString(), out WeatherMode mode) ? mode : Binding.DoNothing;
}
