using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace CatsAssistant.Tests.App;

// Régression pour issue #16 : les deux ResourceDictionary de thème doivent exposer exactement les
// mêmes clés, sans quoi ThemeService.Apply laisserait des DynamicResource non résolues après bascule.
public class ThemeResourceParityTests
{
    [Fact]
    public void DarkTheme_DefinesSameKeysAsLightTheme()
    {
        var lightKeys = ReadResourceKeys("LightTheme.xaml");
        var darkKeys = ReadResourceKeys("DarkTheme.xaml");

        Assert.NotEmpty(lightKeys);
        Assert.Equal(lightKeys, darkKeys);
    }

    private static SortedSet<string> ReadResourceKeys(string fileName, [CallerFilePath] string testFilePath = "")
    {
        var themesDirectory = Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", "..", "..", "src", "CatsAssistant.App", "Themes");
        var path = Path.GetFullPath(Path.Combine(themesDirectory, fileName));
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        var document = XDocument.Load(path);
        return new SortedSet<string>(document.Descendants()
            .Select(element => element.Attribute(xNamespace + "Key")?.Value)
            .Where(key => key is not null)!);
    }
}
