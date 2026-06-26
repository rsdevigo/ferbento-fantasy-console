using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Ferbento.Editor.Music.Controls;

public sealed class PianoKeyboardView : Control
{
    public static readonly StyledProperty<int> SelectedNoteProperty =
        AvaloniaProperty.Register<PianoKeyboardView, int>(nameof(SelectedNote), 60);

    public static readonly StyledProperty<int> OctaveOffsetProperty =
        AvaloniaProperty.Register<PianoKeyboardView, int>(nameof(OctaveOffset));

    public int SelectedNote
    {
        get => GetValue(SelectedNoteProperty);
        set => SetValue(SelectedNoteProperty, value);
    }

    public int OctaveOffset
    {
        get => GetValue(OctaveOffsetProperty);
        set => SetValue(OctaveOffsetProperty, value);
    }

    public event EventHandler<int>? NoteSelected;

    private static readonly int StartOctave = 4;
    private static readonly int KeyCount = 25;
    private static readonly bool[] BlackKey =
    [
        false, false, true, false, true, false, false, true, false, true, false, true,
        false, false, true, false, true, false, false, true, false, true, false, true, false,
    ];

    static PianoKeyboardView()
    {
        SelectedNoteProperty.Changed.AddClassHandler<PianoKeyboardView>((s, _) => s.InvalidateVisual());
        OctaveOffsetProperty.Changed.AddClassHandler<PianoKeyboardView>((s, _) => s.InvalidateVisual());
    }

    public PianoKeyboardView()
    {
        Height = 72;
        PointerPressed += OnPressed;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        int note = HitTest(pos);
        if (note <= 0) return;
        SelectedNote = note;
        NoteSelected?.Invoke(this, note);
        InvalidateVisual();
    }

    private int HitTest(Point pos)
    {
        double w = Bounds.Width;
        if (w <= 0) return -1;
        double whiteW = w / 15.0;
        int whiteIndex = (int)(pos.X / whiteW);
        if (whiteIndex < 0 || whiteIndex >= 15) return -1;

        int baseNote = (StartOctave + OctaveOffset) * 12;
        int[] whiteOffsets = [0, 2, 4, 5, 7, 9, 11];
        int octaveGroup = whiteIndex / 7;
        int wi = whiteIndex % 7;
        int note = baseNote + octaveGroup * 12 + whiteOffsets[wi];
        return Math.Clamp(note, 0, 127);
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double whiteW = w / 15.0;
        int baseNote = (StartOctave + OctaveOffset) * 12;

        for (int i = 0; i < 15; i++)
        {
            double x = i * whiteW;
            var rect = new Rect(x + 1, 0, whiteW - 2, h - 14);
            bool highlighted = Math.Abs(SelectedNote - (baseNote + WhiteNoteOffset(i))) <= 0;
            int noteAt = baseNote + WhiteNoteOffset(i);
            highlighted = noteAt == SelectedNote;
            ctx.FillRectangle(new SolidColorBrush(highlighted ? MusicTheme.Accent : Colors.White), rect);
            ctx.DrawRectangle(new Pen(new SolidColorBrush(MusicTheme.PanelBorder), 1), rect);
        }

        for (int i = 0; i < 15; i++)
        {
            int note = baseNote + WhiteNoteOffset(i);
            if (note % 12 is 0 or 5)
            {
                string label = note % 12 == 0 ? $"C{note / 12 - 1}" : "";
                if (!string.IsNullOrEmpty(label))
                {
                    var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, Typeface.Default, 9,
                        new SolidColorBrush(MusicTheme.TextDim));
                    ctx.DrawText(ft, new Point(i * whiteW + 2, h - 12));
                }
            }
        }
    }

    private static int WhiteNoteOffset(int whiteIndex)
    {
        int[] map = [0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19, 21, 23, 24];
        return whiteIndex < map.Length ? map[whiteIndex] : 0;
    }
}
