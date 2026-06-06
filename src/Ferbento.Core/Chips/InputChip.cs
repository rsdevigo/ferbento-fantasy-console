namespace Ferbento.Core.Chips;

public sealed class InputChip : IChip
{
    public string Name => "Input";

    private const int PlayerCount = 8;
    private const int ButtonCount = 6;
    private const int HoldFrames = 15;
    private const int RepeatFrames = 4;

    private readonly bool[,] _held = new bool[PlayerCount, ButtonCount];
    private readonly bool[,] _pressed = new bool[PlayerCount, ButtonCount];
    private readonly int[,] _holdCounter = new int[PlayerCount, ButtonCount];
    private readonly bool[,] _prevFrame = new bool[PlayerCount, ButtonCount];

    public void Initialize(ChipBus bus) { }

    public void Reset()
    {
        Array.Clear(_held);
        Array.Clear(_pressed);
        Array.Clear(_holdCounter);
        Array.Clear(_prevFrame);
    }

    public void Tick()
    {
        for (var p = 0; p < PlayerCount; p++)
        {
            for (var b = 0; b < ButtonCount; b++)
            {
                _pressed[p, b] = _held[p, b] && !_prevFrame[p, b];
                _prevFrame[p, b] = _held[p, b];

                if (_held[p, b])
                {
                    _holdCounter[p, b]++;
                    if (_holdCounter[p, b] > HoldFrames
                        && (_holdCounter[p, b] - HoldFrames) % RepeatFrames == 0)
                        _pressed[p, b] = true;
                }
                else
                {
                    _holdCounter[p, b] = 0;
                }
            }
        }
    }

    public void SetButton(int player, int button, bool down)
    {
        if (player < 0 || player >= PlayerCount || button < 0 || button >= ButtonCount) return;
        _held[player, button] = down;
    }

    public bool Btn(int button, int player = 0) =>
        player >= 0 && player < PlayerCount && button >= 0 && button < ButtonCount && _held[player, button];

    public bool BtnP(int button, int player = 0) =>
        player >= 0 && player < PlayerCount && button >= 0 && button < ButtonCount && _pressed[player, button];
}
