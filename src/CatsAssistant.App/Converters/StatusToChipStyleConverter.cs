using System.Globalization;
using System.Windows.Data;
using CatsAssistant.Store;
using Application = System.Windows.Application;

namespace CatsAssistant.App.Converters;

/// <summary>
/// Statut d'une ligne CATS (issue #18) → style de chip (Themes/ControlStyles.xaml, issue #16 — les 4
/// styles existent déjà pour cet usage précis : Proposé/Modifié/Validé/Soumis).
/// </summary>
public sealed class StatusToChipStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            TimeBlockStatus.Proposed => "AccentStatusChipStyle",
            TimeBlockStatus.Edited => "CautionStatusChipStyle",
            TimeBlockStatus.Validated => "SuccessStatusChipStyle",
            TimeBlockStatus.Submitted => "NeutralStatusChipStyle",
            _ => "NeutralStatusChipStyle",
        };
        return Application.Current.TryFindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
