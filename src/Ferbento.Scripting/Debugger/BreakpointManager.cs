namespace Ferbento.Scripting.Debugger;

public sealed class BreakpointManager
{
    private readonly HashSet<int> _lineNumbers = new();
    private readonly object _lock = new();

    public event Action<int>? BreakpointHit;

    public bool IsPaused { get; set; }
    public bool StepOver { get; set; }
    public bool StepInto { get; set; }
    public bool StepOut { get; set; }
    public int StepOverDepth { get; set; }

    public void SetBreakpoint(int lineNumber)
    {
        _lineNumbers.Add(lineNumber);
    }

    public void ClearBreakpoint(int lineNumber)
    {
        _lineNumbers.Remove(lineNumber);
    }

    public void ClearAllBreakpoints()
    {
        _lineNumbers.Clear();
    }

    public bool HasBreakpoint(int lineNumber)
    {
        return _lineNumbers.Contains(lineNumber);
    }

    public IReadOnlySet<int> BreakpointLines => _lineNumbers;

    public bool CheckBreakpoint(int lineNumber, int callDepth)
    {
        if (IsPaused) return true;

        if (HasBreakpoint(lineNumber))
        {
            IsPaused = true;
            BreakpointHit?.Invoke(lineNumber);
            return true;
        }

        if (StepOver)
        {
            if (callDepth <= StepOverDepth)
            {
                IsPaused = true;
                StepOver = false;
                BreakpointHit?.Invoke(lineNumber);
                return true;
            }
            return false;
        }

        if (StepInto)
        {
            IsPaused = true;
            StepInto = false;
            BreakpointHit?.Invoke(lineNumber);
            return true;
        }

        if (StepOut)
        {
            if (callDepth < StepOverDepth)
            {
                IsPaused = true;
                StepOut = false;
                BreakpointHit?.Invoke(lineNumber);
                return true;
            }
            return false;
        }

        return false;
    }

    public void WaitWhilePaused()
    {
        lock (_lock)
        {
            while (IsPaused)
                Monitor.Wait(_lock, 100);
        }
    }

    public void Continue()
    {
        lock (_lock)
        {
            IsPaused = false;
            StepOver = false;
            StepInto = false;
            StepOut = false;
            Monitor.PulseAll(_lock);
        }
    }

    public void RequestStepOver(int currentDepth)
    {
        lock (_lock)
        {
            IsPaused = false;
            StepOver = true;
            StepOverDepth = currentDepth;
            Monitor.PulseAll(_lock);
        }
    }

    public void RequestStepInto()
    {
        lock (_lock)
        {
            IsPaused = false;
            StepInto = true;
            Monitor.PulseAll(_lock);
        }
    }

    public void RequestStepOut(int currentDepth)
    {
        lock (_lock)
        {
            IsPaused = false;
            StepOut = true;
            StepOverDepth = currentDepth;
            Monitor.PulseAll(_lock);
        }
    }
}
