namespace CatsAssistant.Store;

public interface IRuleRepository
{
    long Insert(Rule rule);

    void Update(long id, Rule rule);

    void Delete(long id);

    IReadOnlyList<RuleRow> GetAll();

    int CountByOrigin(RuleOrigin origin);

    int DeleteByOrigin(RuleOrigin origin);
}
