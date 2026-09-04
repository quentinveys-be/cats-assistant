namespace CatsAssistant.Filler;

/// <summary>Constantes de session SAP (docs/sap-cats-api.md) : FLP de logon et cookies attendus après authentification.</summary>
public static class SapSession
{
    public const string FlpUrl = "https://p09.sap.ulg.ac.be:50001/sap/bc/ui2/flp?sap-client=010";

    public const string CookieOrigin = "https://p09.sap.ulg.ac.be:50001";

    public static readonly IReadOnlyList<string> RequiredCookieNames =
    [
        "SAP_SESSIONID_P09_010",
        "sap-usercontext",
        "Active",
    ];
}
