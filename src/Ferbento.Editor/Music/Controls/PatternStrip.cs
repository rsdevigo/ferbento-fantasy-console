using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Ferbento.Editor.Music.Controls;

public sealed class PatternStrip : Control
{
    public static readonly StyledProperty<int> SlotCountProperty =
        AvaloniaProperty.Register<PatternStrip, int>(nameof(SlotCount), 32);

    public static readonly StyledProperty<int> SelectedSlotProperty =
        AvaloniaProperty.Register<PatternStrip, int>(nameof(SelectedSlot));

    public static readonly DirectProperty<PatternStrip, Func<int, int>?> GetPatternProperty =
        AvaloniaProperty.RegisterDirect<PatternStrip, Func<int, int>?>(
            nameof(GetPattern), o => o.GetPattern, (o, v) => o.GetPattern = v);

    private Func<int, int>? _getPattern;
    public Func<int, int>? GetPattern
    {
        get => _getPattern;
        set
        {
            if (_getPattern != value)
            {
                _getPattern = value;
                RaisePropertyChanged(GetPatternProperty, _getPattern, value);
                InvalidateVisual();
            }
        }
    }

    public int SlotCount
    {
        get => GetValue(SlotCountProperty);
        set => SetValue(SlotCountProperty, value);
    }

    public int SelectedSlot
    {
        get => GetValue(SelectedSlotProperty);
        set => SetValue(SelectedSlotProperty, value);
    }

    public event EventHandler<int>? SlotSelected;

    static PatternStrip()
    {
        SlotCountProperty.Changed.AddClassHandler<PatternStrip>((s, _) => s.InvalidateVisual());
        SelectedSlotProperty.Changed.AddClassHandler<PatternStrip>((s, _) => s.InvalidateVisual());
    }

    public PatternStrip()
    {
        Height = 36;
        PointerPressed += OnPressed;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        int slot = HitTest(pos);
        if (slot < 0) return;
        SelectedSlot = slot;
        SlotSelected?.Invoke(this, slot);
        InvalidateVisual();
    }

    private int HitTest(Point pos)
    {
        const int slotW = 28;
        const int pad = 4;
        if (pos.Y < pad || pos.Y > Bounds.Height - pad) return -1;
        int slot = (int)((pos.X - pad) / slotW);
        return slot >= 0 && slot < SlotCount ? slot : -1;
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        const int slotW = 28;
        const int pad = 4;
        double y = pad;
        double h = Math.Max(20, Bounds.Height - pad * 2);

        for (int i = 0; i < SlotCount; i++)
        {
            double x = pad + i * slotW;
            var rect = new Rect(x, y, slotW - 2, h);
            bool selected = i == SelectedSlot;
            var bg = selected ? MusicTheme.Accent : MusicTheme.GridBg;
            var fg = selected ? MusicTheme.PanelBorder : MusicTheme.Text;
            ctx.FillRectangle(new SolidColorBrush(bg), rect);
            ctx.DrawRectangle(new Pen(new SolidColorBrush(MusicTheme.PanelBorder), 1), rect);

            int pat = GetPattern?.Invoke(i) ?? 0;
            var ft = new FormattedText(pat.ToString("D2"), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10,
                new SolidColorBrush(fg));
            ctx.DrawText(ft, new Point(x + 4, y + 2));
        }
    }
}
