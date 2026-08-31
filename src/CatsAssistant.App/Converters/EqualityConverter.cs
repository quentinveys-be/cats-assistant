using System.Globalization;
using System.Windows.Data;

namespace CatsAssistant.App.Converters;

/// <summary>Vrai si la valeur liée égale <c>ConverterParameter</c> — met en évidence le segment actif
/// d'un contrôle segmenté (issue #23 : durée min. de bloc, rétention) sans dupliquer un booléen par segment.</summary>
public sealed class EqualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && string.Equals(
            System.Convert.ToString(value, CultureInfo.InvariantCulture),
            System.Convert.ToString(parameter, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
