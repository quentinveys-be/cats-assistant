using CatsAssistant.Store;

namespace CatsAssistant.Collector;

/// <summary>
/// In-process foreground/idle capture via SetWinEventHook + GetLastInputInfo (ADR D2).
/// Runs inside the WPF App process rather than as a separate service, since the project is user-mode only.
/// </summary>
public sealed class ActivityCollector : IDisposable, IActivityCollectorControl
{
    private readonly IActivityEventRepository _repository;
    private readonly IdleDetector _idleDetector;
    private readonly RetryBackoff _retryBackoff;
    private readonly TimeSpan _idlePollInterval;
    private readonly object _sync = new();

    private NativeMethods.WinEventDelegate? _hookDelegate;
    private IntPtr _foregroundHook = IntPtr.Zero;
    private IntPtr _nameChangeHook = IntPtr.Zero;
    private Timer? _idleTimer;
    private SynchronizationContext? _installContext;
    private string? _lastProcess;
    private string? _lastWindowTitle;
    private bool _hasLastWindow;
    private bool _disposed;

    public ActivityCollector(
        IActivityEventRepository repository,
        TimeSpan? idleThreshold = null,
        TimeSpan? idlePollInterval = null,
        RetryBackoff? retryBackoff = null)
    {
        _repository = repository;
        _idleDetector = new IdleDetector(idleThreshold);
        _idlePollInterval = idlePollInterval ?? TimeSpan.FromSeconds(15);
        _retryBackoff = retryBackoff ?? new RetryBackoff();
    }

    public bool IsRunning { get; private set; }

    public void Start()
    {
        lock (_sync)
        {
            if (IsRunning) return;

            IsRunning = true;

            // Out-of-context WinEvent callbacks are posted to the thread that installed the hook, which must
            // pump messages. Every later re-install has to happen on that same thread, so capture it here.
            _installContext = SynchronizationContext.Current;

            InstallHooks();

            // Nothing fires until the user switches windows, so record the window in focus right now —
            // otherwise the interval between start and the next switch has no event to anchor a segment.
            _hasLastWindow = false;
            TryCaptureForegroundWindow(DateTime.UtcNow, ActivityEventKind.Foreground);

            _idleTimer = new Timer(_ => PollIdle(), null, TimeSpan.Zero, _idlePollInterval);
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!IsRunning) return;

            IsRunning = false;
            _idleTimer?.Dispose();
            _idleTimer = null;
            UninstallHooks();

            // Without a marker the pause is invisible downstream and aggregation stretches the segment that
            // was open at pause time across the whole untracked interval. idle_start is the schema's way of
            // saying "no tracked activity from here on" (docs/data-model.md fixes the kind vocabulary).
            if (!_idleDetector.IsIdle)
            {
                TryInsert(DateTime.UtcNow, ActivityEventKind.IdleStart, null, null);
            }

            _hasLastWindow = false;
            _lastProcess = null;
            _lastWindowTitle = null;
        }
    }

    private void InstallHooks()
    {
        _hookDelegate = OnWinEvent;

        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _hookDelegate,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);

        _nameChangeHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_NAMECHANGE,
            NativeMethods.EVENT_OBJECT_NAMECHANGE,
            IntPtr.Zero,
            _hookDelegate,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_foregroundHook == IntPtr.Zero || _nameChangeHook == IntPtr.Zero)
        {
            ScheduleReinstall();
        }
        else
        {
            _retryBackoff.Reset();
        }
    }

    private void UninstallHooks()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }

        if (_nameChangeHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_nameChangeHook);
            _nameChangeHook = IntPtr.Zero;
        }

        _hookDelegate = null;
    }

    private void ScheduleReinstall()
    {
        var delay = _retryBackoff.NextDelay();
        var context = _installContext;

        _ = Task.Delay(delay).ContinueWith(
            _ =>
            {
                if (context is null)
                {
                    ReinstallHooks();
                    return;
                }

                context.Post(_ => ReinstallHooks(), null);
            },
            TaskScheduler.Default);
    }

    private void ReinstallHooks()
    {
        // Taking _sync keeps a pending re-install from resurrecting the hooks behind a Stop() that already ran.
        lock (_sync)
        {
            if (!IsRunning) return;
            UninstallHooks();
            InstallHooks();
        }
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        // An unhandled exception on the native callback thread would crash the host WPF process outright.
        try
        {
            if (idObject != NativeMethods.OBJID_WINDOW || idChild != NativeMethods.CHILDID_SELF || hwnd == IntPtr.Zero) return;

            // The NAMECHANGE hook is system-wide, so background windows retitling themselves (unread counters,
            // media timers) reach us too. Only the focused window is user activity.
            if (eventType == NativeMethods.EVENT_OBJECT_NAMECHANGE && hwnd != NativeMethods.GetForegroundWindow()) return;

            var process = WindowInfoReader.GetProcessName(hwnd);
            var title = WindowInfoReader.GetWindowTitle(hwnd);

            if (_hasLastWindow && process == _lastProcess && title == _lastWindowTitle) return;

            _hasLastWindow = true;
            _lastProcess = process;
            _lastWindowTitle = title;

            var kind = eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND
                ? ActivityEventKind.Foreground
                : ActivityEventKind.TitleChange;

            _repository.Insert(DateTime.UtcNow, kind, process, title, null);
        }
        catch
        {
            // Best-effort capture: a failed read or a failed insert says nothing about the health of the hook,
            // and tearing the hooks down over it would lose far more activity than the one event we dropped.
        }
    }

    private void PollIdle()
    {
        try
        {
            var lastInputInfo = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>(),
            };

            if (!NativeMethods.GetLastInputInfo(ref lastInputInfo)) return;

            var idleDuration = IdleTickMath.ComputeIdleDuration(NativeMethods.GetTickCount(), lastInputInfo.dwTime);
            var transition = _idleDetector.Evaluate(idleDuration);

            // The transition is only noticed on the next tick, up to a full threshold after the fact.
            // Stamping it at detection time would credit that whole delay to the preceding window.
            var lastInputUtc = DateTime.UtcNow - idleDuration;

            switch (transition)
            {
                case IdleTransition.BecameIdle:
                    TryInsert(lastInputUtc, ActivityEventKind.IdleStart, null, null);
                    break;
                case IdleTransition.BecameActive:
                    TryInsert(lastInputUtc, ActivityEventKind.IdleEnd, null, null);

                    // Resuming in the same window fires no WinEvent, so without this the whole stretch between
                    // the resume and the next window switch would have no event to open a segment from.
                    _hasLastWindow = false;
                    TryCaptureForegroundWindow(lastInputUtc, ActivityEventKind.Foreground);
                    break;
                case IdleTransition.None:
                default:
                    break;
            }
        }
        catch
        {
            // Best-effort polling tick — a transient failure must not crash the host process nor the timer.
        }
    }

    private void TryCaptureForegroundWindow(DateTime timestampUtc, ActivityEventKind kind)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        string? process;
        string? title;

        try
        {
            process = WindowInfoReader.GetProcessName(hwnd);
            title = WindowInfoReader.GetWindowTitle(hwnd);
        }
        catch
        {
            return;
        }

        _hasLastWindow = true;
        _lastProcess = process;
        _lastWindowTitle = title;

        TryInsert(timestampUtc, kind, process, title);
    }

    private void TryInsert(DateTime timestampUtc, ActivityEventKind kind, string? process, string? windowTitle)
    {
        try
        {
            _repository.Insert(timestampUtc, kind, process, windowTitle, null);
        }
        catch
        {
            // Best-effort capture — see OnWinEvent.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
