using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CatsAssistant.App.Converters;

/// <summary>Visible si la valeur liée n'est pas null (ex. badge de navigation, masqué tant qu'aucun compte n'est fourni).</summary>
public sealed class NullableToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
