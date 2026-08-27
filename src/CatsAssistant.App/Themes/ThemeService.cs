using System.Windows;
using Application = System.Windows.Application;

namespace CatsAssistant.App.Themes;

/// <summary>
/// Bascule clair/sombre à chaud (issue #16) : remplace le dictionnaire de thème à l'index 0 des
/// ressources fusionnées de l'application (voir App.xaml). Les styles consommateurs doivent lier
/// leurs couleurs en DynamicResource pour suivre le remplacement.
/// </summary>
public static class ThemeService
{
    public static void Apply(bool isDarkTheme)
    {
        var themeUri = new Uri(isDarkTheme ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = themeUri };
    }
}
