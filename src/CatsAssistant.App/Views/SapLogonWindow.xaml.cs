using System.Net;
using System.Windows;
using CatsAssistant.Filler;
using Microsoft.Web.WebView2.Core;
using Application = System.Windows.Application;

namespace CatsAssistant.App.Views;

/// <summary>
/// Fenêtre de logon SAP (issue #27, docs/adr/D4-sap-odata-webview2.md) : le formulaire/SAML/Kerberos est géré
/// entièrement par WebView2 (aucun champ identifiant n'est jamais lu par CATS Assistant). Dès que les cookies
/// de session attendus (<see cref="SapSession.RequiredCookieNames"/>) apparaissent, ils sont récupérés en
/// mémoire et la fenêtre se ferme d'elle-même ; aucune valeur de cookie n'est jamais loggée.
/// </summary>
public partial class SapLogonWindow : Window
{
    private TaskCompletionSource<CookieContainer?>? _completion;
    private bool _capturing;

    public SapLogonWindow()
    {
        InitializeComponent();
        Owner = Application.Current?.MainWindow;
        Closed += (_, _) => _completion?.TrySetResult(null);
    }

    public async Task<CookieContainer?> LogonAsync(CancellationToken cancellationToken)
    {
        _completion = new TaskCompletionSource<CookieContainer?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => _completion.TrySetResult(null));

        await Browser.EnsureCoreWebView2Async();
        Browser.CoreWebView2.SourceChanged += async (_, _) => await TryCaptureSessionAsync();
        Browser.CoreWebView2.NavigationCompleted += async (_, _) => await TryCaptureSessionAsync();
        Browser.CoreWebView2.Navigate(SapSession.FlpUrl);

        Show();
        var cookies = await _completion.Task;
        Close();
        return cookies;
    }

    private async Task TryCaptureSessionAsync()
    {
        if (_capturing || Browser.CoreWebView2 is null)
        {
            return;
        }

        _capturing = true;
        try
        {
            UrlText.Text = "Connexion sécurisée · " + Browser.CoreWebView2.Source;

            var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync(SapSession.CookieOrigin);
            var container = new CookieContainer();
            var originUri = new Uri(SapSession.CookieOrigin);

            foreach (var name in SapSession.RequiredCookieNames)
            {
                var cookie = cookies.FirstOrDefault(c => c.Name == name);
                if (cookie is null)
                {
                    return;
                }

                container.Add(originUri, new Cookie(cookie.Name, cookie.Value));
            }

            _completion?.TrySetResult(container);
        }
        finally
        {
            _capturing = false;
        }
    }
}
