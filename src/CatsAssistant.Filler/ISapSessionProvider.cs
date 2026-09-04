using System.Net;

namespace CatsAssistant.Filler;

/// <summary>
/// Session SAP portée par cookies (docs/adr/D4-sap-odata-webview2.md). Aucun identifiant SAP n'est jamais
/// stocké ; seuls les cookies de la session en cours sont exposés, en mémoire, à l'implémentation (fenêtre
/// de logon WebView2, issue #27) et au client OData (issue dédiée, hors périmètre ici).
/// </summary>
public interface ISapSessionProvider
{
    SapSessionState State { get; }

    /// <summary>Cookies de la session courante, ou <c>null</c> tant qu'aucun logon n'a réussi.</summary>
    CookieContainer? Cookies { get; }

    /// <summary>Levé à chaque changement de <see cref="State"/> (connexion, expiration).</summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Garantit une session utilisable : retourne immédiatement si déjà <see cref="SapSessionState.Connected"/>,
    /// sinon ouvre la fenêtre de logon. Retourne <c>false</c> si l'utilisateur ferme la fenêtre sans se connecter.
    /// </summary>
    Task<bool> EnsureLogonAsync(CancellationToken cancellationToken = default);

    /// <summary>À appeler par le client OData sur 401/302 : marque la session expirée et propose la reconnexion.</summary>
    void ReportUnauthorized();
}
