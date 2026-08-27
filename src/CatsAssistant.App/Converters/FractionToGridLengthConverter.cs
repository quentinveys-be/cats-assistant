using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CatsAssistant.App.Converters;

/// <summary>Convertit une fraction (0..1) en <see cref="GridLength"/> proportionnelle (Star), utilisé pour
/// dessiner une jauge à deux colonnes (rempli/restant) sans dépendance à une largeur de conteneur connue.</summary>
public sealed class FractionToGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new GridLength(value is double fraction ? Math.Clamp(fraction, 0, 1) : 0, GridUnitType.Star);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
