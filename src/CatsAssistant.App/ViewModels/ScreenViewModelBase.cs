using CatsAssistant.App.Mvvm;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Base commune aux 4 écrans navigables. Le contenu de chaque écran est hors périmètre de cette étape
/// (issue #15, shell uniquement) — chaque VM concret ne fait qu'exposer son titre pour l'instant.
/// </summary>
public abstract class ScreenViewModelBase(string title) : ObservableObject
{
    public string Title { get; } = title;
}
