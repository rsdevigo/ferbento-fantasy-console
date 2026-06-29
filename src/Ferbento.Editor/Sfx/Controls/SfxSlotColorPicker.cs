using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ferbento.Core.Chips;

namespace Ferbento.Editor.Sfx.Controls;

public sealed class SfxSlotColorPicker : Control
{
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SfxSlotColorPicker, int>(nameof(SelectedIndex));

    public static readonly StyledProperty<ColorChip?> ColorChipProperty =
        AvaloniaProperty.Register<SfxSlotColorPicker, ColorChip?>(nameof(ColorChip));

    public event Action<int>? ColorSelected;

    private const int SwatchSize = 14;
    private const int SwatchGap = 2;

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public ColorChip? ColorChip
    {
        get => GetValue(ColorChipProperty);
        set => SetValue(ColorChipProperty, value);
    }

    static SfxSlotColorPicker()
    {
        SelectedIndexProperty.Changed.AddClassHandler<SfxSlotColorPicker>((s, _) => s.InvalidateVisual());
        ColorChipProperty.Changed.AddClassHandler<SfxSlotColorPicker>((s, _) => s.InvalidateVisual());
    }

    public SfxSlotColorPicker()
    {
        Focusable = false;
        PointerPressed += OnPointerPressed;
        ToolTip.SetTip(this, "Pattern color for the music editor (editor-only label).");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int w = SfxSlotColors.PaletteCount * (SwatchSize + SwatchGap) - SwatchGap;
        return new Size(w, SwatchSize);
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        for (int i = 0; i < SfxSlotColors.PaletteCount; i++)
        {
            double x = i * (SwatchSize + SwatchGap);
            var rect = new Rect(x, 0, SwatchSize, SwatchSize);
            ctx.FillRectangle(new SolidColorBrush(SfxSlotColors.ToColor(i, ColorChip)), rect);
            var pen = i == SelectedIndex
                ? new Pen(Brushes.White, 2)
                : new Pen(new SolidColorBrush(Color.Parse("#444444")), 1);
            ctx.DrawRectangle(pen, rect);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        int index = (int)(e.GetPosition(this).X / (SwatchSize + SwatchGap));
        if (index < 0 || index >= SfxSlotColors.PaletteCount) return;
        SelectedIndex = index;
        ColorSelected?.Invoke(index);
    }
}
