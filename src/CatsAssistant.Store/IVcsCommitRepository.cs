using CatsAssistant.Connectors;

namespace CatsAssistant.Store;

public interface IVcsCommitRepository
{
    void Upsert(VcsCommit commit);

    IReadOnlyList<VcsCommit> GetByDateRange(DateTimeOffset fromUtc, DateTimeOffset toUtc);
}
