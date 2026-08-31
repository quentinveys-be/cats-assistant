using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CatsAssistant.App.Converters;

/// <summary>Visible quand la valeur liée est false (ex. contenu masqué par l'état vide, issue #17).</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
