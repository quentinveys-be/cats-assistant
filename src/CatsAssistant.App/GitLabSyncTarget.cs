using CatsAssistant.Connectors;

namespace CatsAssistant.App;

/// <summary>
/// Dépôt/branche à synchroniser via <see cref="IGitLabConnector.GetCommitsAsync"/> — GitLab n'offre pas
/// de découverte automatique des dépôts de l'utilisateur, la liste est donc fournie par l'appelant
/// (onboarding config, Phase 5 ; en attendant, câblée par variable d'environnement dans App.xaml.cs).
/// </summary>
public sealed record GitLabSyncTarget(string ProjectId, string Branch);
