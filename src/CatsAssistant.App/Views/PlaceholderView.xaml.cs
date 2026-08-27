using UserControl = System.Windows.Controls.UserControl;

namespace CatsAssistant.App.Views;

/// <summary>Hôte vide pour les 4 écrans du shell (issue #15) — leur contenu est hors périmètre.</summary>
public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();
}
