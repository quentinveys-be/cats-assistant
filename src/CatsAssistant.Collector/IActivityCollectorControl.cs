namespace CatsAssistant.Collector;

/// <summary>Surface minimale exposée à l'écran Paramètres (issue #23) pour le toggle "capture en pause" —
/// évite de coupler l'UI au type concret <see cref="ActivityCollector"/> (WinEvent hooks, etc.).</summary>
public interface IActivityCollectorControl
{
    bool IsRunning { get; }

    void Start();

    void Stop();
}
