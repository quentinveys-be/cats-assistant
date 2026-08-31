using System.Collections.ObjectModel;
using CatsAssistant.App.Mvvm;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>
/// Onglet "Règles" des Paramètres (issue #24) : CRUD sur la table <c>rules</c> via <see cref="IRuleRepository"/>.
/// Chaque écriture atteint directement la base consommée par <see cref="CatsAssistant.Correlator.RuleEvaluator"/>
/// à la prochaine corrélation (celui-ci recharge <see cref="IRuleRepository.GetAll"/> à chaque appel, jamais
/// de cache) : aucun câblage supplémentaire n'est nécessaire pour que les règles créées ici s'appliquent.
/// </summary>
public sealed class RulesViewModel : ObservableObject
{
    private readonly IRuleRepository _repository;

    public RulesViewModel(IRuleRepository repository)
    {
        _repository = repository;
        Rows = [];
        AddRuleCommand = new RelayCommand(AddRule);
        Reload();
    }

    public ObservableCollection<RuleRowViewModel> Rows { get; }

    public RelayCommand AddRuleCommand { get; }

    private void Reload()
    {
        Rows.Clear();
        foreach (var row in _repository.GetAll())
        {
            Rows.Add(CreateRow(row));
        }
    }

    private RuleRowViewModel CreateRow(RuleRow row) => new(
        row.Id, row.Rule.MatcherKind, row.Rule.MatcherValue, row.Rule.Target, row.Rule.Priority, row.Rule.Origin,
        isNew: false, OnSave, OnDelete, OnCancel);

    private void AddRule()
    {
        var nextPriority = Rows.Count == 0 ? 1 : Rows.Max(r => r.Priority) + 1;
        Rows.Add(new RuleRowViewModel(
            0, RuleMatcherKind.Process, matcherValue: "", target: "", nextPriority, RuleOrigin.Manual,
            isNew: true, OnSave, OnDelete, OnCancel));
    }

    private void OnSave(RuleRowViewModel row)
    {
        if (row.IsNew)
        {
            var id = _repository.Insert(row.ToRule());
            row.MarkSaved(id);
        }
        else
        {
            _repository.Update(row.Id, row.ToRule());
            row.MarkSaved(row.Id);
        }
    }

    private void OnDelete(RuleRowViewModel row)
    {
        if (!row.IsNew)
        {
            _repository.Delete(row.Id);
        }

        Rows.Remove(row);
    }

    private void OnCancel(RuleRowViewModel row)
    {
        if (row.IsNew)
        {
            Rows.Remove(row);
            return;
        }

        var stored = _repository.GetAll().Single(r => r.Id == row.Id);
        row.RestoreFrom(stored);
    }
}
