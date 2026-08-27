namespace CatsAssistant.Store;

public interface ISettingsRepository
{
    string? Get(string key);

    void Set(string key, string value);
}
