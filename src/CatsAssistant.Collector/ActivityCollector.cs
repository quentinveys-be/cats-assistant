using CatsAssistant.Store;

namespace CatsAssistant.Collector;

/// <summary>
/// In-process foreground/idle capture via SetWinEventHook + GetLastInputInfo (ADR D2).
/// Runs inside the WPF App process (no separate service) — see CONVENTIONS.md decision #6.
/// </summary>
public sealed class ActivityCollector : IDisposable
{
    private readonly IActivityEventRepository _repository;
    private readonly IdleDetector _idleDetector;
    private readonly RetryBackoff _retryBackoff;
    private readonly TimeSpan _idlePollInterval;

    private NativeMethods.WinEventDelegate? _hookDelegate;
    private IntPtr _foregroundHook = IntPtr.Zero;
    private IntPtr _nameChangeHook = IntPtr.Zero;
    private Timer? _idleTimer;
    private string? _lastProcess;
    private string? _lastWindowTitle;
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
        if (IsRunning) return;

        IsRunning = true;
        InstallHooks();
        _idleTimer = new Timer(_ => PollIdle(), null, TimeSpan.Zero, _idlePollInterval);
    }

    public void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;
        _idleTimer?.Dispose();
        _idleTimer = null;
        UninstallHooks();
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
        _ = Task.Delay(delay).ContinueWith(
            _ =>
            {
                if (!IsRunning) return;
                UninstallHooks();
                InstallHooks();
            },
            TaskScheduler.Default);
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
        // An unhandled exception on the native callback thread would crash the host WPF process outright,
        // so a failed read is treated as a lost hook and folds into the same retry path as a failed install.
        try
        {
            if (idObject != NativeMethods.OBJID_WINDOW || hwnd == IntPtr.Zero) return;

            var process = WindowInfoReader.GetProcessName(hwnd);
            var title = WindowInfoReader.GetWindowTitle(hwnd);

            if (process == _lastProcess && title == _lastWindowTitle) return;

            _lastProcess = process;
            _lastWindowTitle = title;

            var kind = eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND
                ? ActivityEventKind.Foreground
                : ActivityEventKind.TitleChange;

            _repository.Insert(DateTime.UtcNow, kind, process, title, null);
        }
        catch
        {
            ScheduleReinstall();
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

            switch (transition)
            {
                case IdleTransition.BecameIdle:
                    _repository.Insert(DateTime.UtcNow, ActivityEventKind.IdleStart, null, null, null);
                    break;
                case IdleTransition.BecameActive:
                    _repository.Insert(DateTime.UtcNow, ActivityEventKind.IdleEnd, null, null, null);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
