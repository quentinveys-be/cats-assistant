using CatsAssistant.Store;

namespace CatsAssistant.Tests.Store;

public class DpapiActivityKeyStoreTests
{
    [Fact]
    public void GetOrCreateKey_PersistsAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{Guid.NewGuid():N}.key");

        try
        {
            var firstKey = new DpapiActivityKeyStore(path).GetOrCreateKey();
            var secondKey = new DpapiActivityKeyStore(path).GetOrCreateKey();

            Assert.Equal(firstKey, secondKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetOrCreateKey_StoresKeyProtectedNotInClear()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{Guid.NewGuid():N}.key");

        try
        {
            var key = new DpapiActivityKeyStore(path).GetOrCreateKey();

            Assert.True(File.Exists(path));
            var storedBytes = File.ReadAllBytes(path);
            var rawKeyBytes = Convert.FromBase64String(key);

            Assert.NotEqual(rawKeyBytes, storedBytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetOrCreateKey_GeneratesDistinctKeysForDistinctFiles()
    {
        var firstPath = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{Guid.NewGuid():N}.key");
        var secondPath = Path.Combine(Path.GetTempPath(), $"cats-assistant-tests-{Guid.NewGuid():N}.key");

        try
        {
            var firstKey = new DpapiActivityKeyStore(firstPath).GetOrCreateKey();
            var secondKey = new DpapiActivityKeyStore(secondPath).GetOrCreateKey();

            Assert.NotEqual(firstKey, secondKey);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }
}
