namespace CatsAssistant.Connectors;

public sealed class PosidZwpidExtractionResult
{
    public bool IsExtracted { get; }

    public string? Posid { get; }

    public string? Zwpid { get; }

    private PosidZwpidExtractionResult(bool isExtracted, string? posid, string? zwpid)
    {
        IsExtracted = isExtracted;
        Posid = posid;
        Zwpid = zwpid;
    }

    public static PosidZwpidExtractionResult Extracted(string posid, string zwpid) =>
        new(true, posid, zwpid);

    public static PosidZwpidExtractionResult NotExtracted() =>
        new(false, null, null);
}
