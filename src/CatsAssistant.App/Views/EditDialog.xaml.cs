using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CatsAssistant.App.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace CatsAssistant.App.Views;

/// <summary>
/// Dialogue d'édition modal (issue #19). Toute la logique vit dans <see cref="EditDialogViewModel"/> ;
/// ce code-behind ne fait que la plomberie fenêtre : fermeture sur action, et clavier/souris de
/// l'autocomplete (flèche bas vers la liste, Entrée ou clic pour choisir).
/// </summary>
public partial class EditDialog : Window
{
    private readonly EditDialogViewModel _viewModel;

    public EditDialog(EditDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += result => DialogResult = result;
    }

    private void TicketBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Retour de focus depuis la liste après un choix : ne pas rouvrir la liste qui vient de se fermer.
        if (e.OldFocus is not ListBoxItem)
        {
            _viewModel.IsListOpen = true;
        }
    }

    private void TicketBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || SuggestionList.Items.Count == 0)
        {
            return;
        }

        _viewModel.IsListOpen = true;
        SuggestionList.SelectedIndex = 0;
        ((ListBoxItem)SuggestionList.ItemContainerGenerator.ContainerFromIndex(0))?.Focus();
        e.Handled = true;
    }

    private void SuggestionList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SuggestionList.SelectedItem is TicketSuggestion ticket)
        {
            PickTicket(ticket);
            e.Handled = true;
        }
    }

    private void SuggestionItem_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (((ListBoxItem)sender).DataContext is TicketSuggestion ticket)
        {
            PickTicket(ticket);
            e.Handled = true;
        }
    }

    private void PickTicket(TicketSuggestion ticket)
    {
        _viewModel.SelectTicket(ticket);
        TicketBox.Focus();
        TicketBox.CaretIndex = TicketBox.Text.Length;
    }
}
