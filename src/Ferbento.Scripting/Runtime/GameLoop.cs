using Ferbento.Core.Chips;
using Ferbento.Scripting.Sandbox;

namespace Ferbento.Scripting.Runtime;

public sealed class GameLoop
{
    private readonly ChipBus _bus;
    private readonly ScriptSandbox _sandbox;
    private readonly bool _hasUpdate60;
    private readonly bool _hasUpdate;
    private readonly bool _hasDraw;
    private readonly bool _preserveCartData;
    private bool _initialized;
    private int _frameCounter;
    private double _lastFrameTime;

    public const double UpdateFps = 30.0;
    public const double Update60Fps = 60.0;

    public ScriptSandbox Sandbox => _sandbox;
    public ChipBus Bus => _bus;
    public int FrameCount => _frameCounter;

    public GameLoop(ChipBus bus, ScriptSandbox sandbox, bool preserveCartData = false)
    {
        _bus = bus;
        _sandbox = sandbox;
        _preserveCartData = preserveCartData;
        _hasUpdate60 = sandbox.HasFunction("_update60");
        _hasUpdate = sandbox.HasFunction("_update");
        _hasDraw = sandbox.HasFunction("_draw");
        _lastFrameTime = GetTimeSeconds();
        _sandbox.Context.BootTime = _lastFrameTime;
    }

    public void Boot()
    {
        if (_preserveCartData)
            _bus.ResetForRun();
        else
            _bus.Reset();
        _initialized = false;
        _frameCounter = 0;
        _sandbox.Context.BootTime = GetTimeSeconds();
        _sandbox.Context.RebootRequested = false;

        if (_sandbox.HasFunction("_init"))
            _sandbox.Call("_init");

        _initialized = true;
    }

    public void Step()
    {
        if (!_initialized) Boot();

        _sandbox.Context.ResetCpuBudget();
        _sandbox.Context.FlipCalled = false;
        _sandbox.Context.FrameTime = GetTimeSeconds();
        _sandbox.Context.FrameCount = _frameCounter;

        if (_hasUpdate60)
            _sandbox.Call("_update60");
        else if (_hasUpdate)
            _sandbox.Call("_update");

        var shouldDraw = !_hasUpdate60 || (_frameCounter & 1) == 0;
        if (_hasDraw && shouldDraw)
        {
            _sandbox.Call("_draw");
            if (_preserveCartData && !_sandbox.Context.FlipCalled)
                _bus.Display.Flip();
        }

        _bus.Tick();
        _frameCounter++;

        if (_sandbox.Context.RebootRequested)
            Boot();
    }

    public void RunFrame(double deltaSeconds)
    {
        _lastFrameTime += deltaSeconds;
        Step();
    }

    private double GetTimeSeconds() =>
        System.Environment.TickCount64 / 1000.0;
}
