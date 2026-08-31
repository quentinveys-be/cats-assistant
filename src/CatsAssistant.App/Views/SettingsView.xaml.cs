using System.Windows.Controls;
using CatsAssistant.App.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace CatsAssistant.App.Views;

/// <summary>
/// Écran "Paramètres" (issue #24). <see cref="OnTokenPasswordChanged"/> est le seul point de contact entre
/// <see cref="PasswordBox"/> (dont la propriété Password n'est pas bindable, par sécurité WPF) et
/// <see cref="TokenConnectionCardViewModel.PendingToken"/> : partagé par les cartes JIRA et GitLab, il lit
/// simplement le DataContext de l'expéditeur plutôt que de dupliquer ce câblage par carte.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private void OnTokenPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is PasswordBox { DataContext: TokenConnectionCardViewModel card } passwordBox)
        {
            card.PendingToken = passwordBox.Password;
        }
    }

    // PasswordBox.Password n'est pas bindable (sécurité WPF) : Confirmer/Annuler ferment le formulaire côté
    // VM mais laissent le texte tapé visible dans le contrôle si on ne l'efface pas explicitement ici.
    private void OnJiraPasswordFormClosed(object sender, System.Windows.RoutedEventArgs e) => JiraTokenPasswordBox.Clear();

    private void OnGitLabPasswordFormClosed(object sender, System.Windows.RoutedEventArgs e) => GitLabTokenPasswordBox.Clear();
}
