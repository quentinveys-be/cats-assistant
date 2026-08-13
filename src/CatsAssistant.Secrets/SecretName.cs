namespace CatsAssistant.Secrets;

/// <summary>
/// Identifiants des secrets admis dans le coffre (ADR D6). Aucun credential SAP : voir D4 (logon WebView2 interactif).
/// </summary>
public enum SecretName
{
    JiraApiToken,
    GitLabPersonalToken,
}
