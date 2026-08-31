using System.Windows;
using CatsAssistant.App.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace CatsAssistant.App.Views;

public partial class DayView : UserControl
{
    public DayView()
    {
        InitializeComponent();

        // Le dialogue d'édition (issue #19) a besoin d'une fenêtre propriétaire : c'est le seul service de
        // vue que le DayViewModel ne peut pas porter lui-même (remplacé par un stub en test).
        DataContextChanged += (_, args) =>
        {
            if (args.NewValue is DayViewModel viewModel)
            {
                viewModel.ShowEditDialog = dialogViewModel =>
                    new EditDialog(dialogViewModel) { Owner = Window.GetWindow(this) }.ShowDialog() == true;
            }
        };
    }
}
