using CatsAssistant.App.Mvvm;
using CatsAssistant.Store;

namespace CatsAssistant.App.ViewModels;

/// <summary>Une ligne éditable du tableau "Règles" (issue #24) : bascule affichage/édition en place, sans dialogue séparé.</summary>
public sealed class RuleRowViewModel : ObservableObject
{
    private readonly Action<RuleRowViewModel> _onSave;
    private readonly Action<RuleRowViewModel> _onDelete;
    private readonly Action<RuleRowViewModel> _onCancel;

    private RuleMatcherKind _matcherKind;
    private string _matcherValue;
    private string _target;
    private int _priority;
    private bool _isEditing;

    public RuleRowViewModel(
        long id,
        RuleMatcherKind matcherKind,
        string matcherValue,
        string target,
        int priority,
        RuleOrigin origin,
        bool isNew,
        Action<RuleRowViewModel> onSave,
        Action<RuleRowViewModel> onDelete,
        Action<RuleRowViewModel> onCancel)
    {
        Id = id;
        _matcherKind = matcherKind;
        _matcherValue = matcherValue;
        _target = target;
        _priority = priority;
        Origin = origin;
        IsNew = isNew;
        _isEditing = isNew;
        _onSave = onSave;
        _onDelete = onDelete;
        _onCancel = onCancel;

        EditCommand = new RelayCommand(() => IsEditing = true);
        SaveCommand = new RelayCommand(
            () => _onSave(this),
            () => !string.IsNullOrWhiteSpace(MatcherValue) && !string.IsNullOrWhiteSpace(Target));
        CancelCommand = new RelayCommand(() => _onCancel(this));
        DeleteCommand = new RelayCommand(() => _onDelete(this));
    }

    public long Id { get; private set; }

    public bool IsNew { get; private set; }

    public IReadOnlyList<RuleMatcherKind> MatcherKinds { get; } = Enum.GetValues<RuleMatcherKind>();

    public RuleMatcherKind MatcherKind
    {
        get => _matcherKind;
        set => SetProperty(ref _matcherKind, value);
    }

    public string MatcherValue
    {
        get => _matcherValue;
        set => SetProperty(ref _matcherValue, value);
    }

    public string Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    public int Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    /// <summary>Toujours <see cref="RuleOrigin.Manual"/> pour une règle créée dans l'UI ; jamais éditable (issue #24).</summary>
    public RuleOrigin Origin { get; private set; }

    public string OriginLabel => Origin == RuleOrigin.Learned ? "apprise" : "manuelle";

    public bool IsEditing
    {
        get => _isEditing;
        private set => SetProperty(ref _isEditing, value);
    }

    public RelayCommand EditCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public Rule ToRule() => new(MatcherKind, MatcherValue, Target, Priority, Origin);

    internal void MarkSaved(long id)
    {
        Id = id;
        IsNew = false;
        IsEditing = false;
    }

    internal void RestoreFrom(RuleRow row)
    {
        MatcherKind = row.Rule.MatcherKind;
        MatcherValue = row.Rule.MatcherValue;
        Target = row.Rule.Target;
        Priority = row.Rule.Priority;
        IsEditing = false;
    }
}
