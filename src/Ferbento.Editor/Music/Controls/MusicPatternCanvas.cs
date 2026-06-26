using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ferbento.Editor.Music;

namespace Ferbento.Editor.Music.Controls;

public sealed class MusicPatternCanvas : Control
{
    private MusicEditorController? _ctrl;
    private int _hoverRow = -1;
    private int _hoverChannel = -1;

    private const int StepW = 22;
    private const int RowH = 28;
    private const int LabelW = 36;

    public MusicPatternCanvas()
    {
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerExited += (_, _) => { _hoverRow = -1; _hoverChannel = -1; InvalidateVisual(); };
    }

    public void Bind(MusicEditorController controller)
    {
        _ctrl = controller;
        InvalidateMeasure();
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        if (_ctrl == null) return;

        int channels = _ctrl.ChannelCount;
        int length = _ctrl.Length;
        int playhead = _ctrl.IsPlaying ? _ctrl.ClampedPlayheadRow : -1;

        for (int ch = 0; ch < channels; ch++)
        {
            double y = ch * RowH;
            var rowRect = new Rect(0, y, LabelW + length * StepW, RowH);
            ctx.FillRectangle(new SolidColorBrush(MusicTheme.ChannelColor(ch)), rowRect);

            var label = new FormattedText($"CH{ch + 1}", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9,
                new SolidColorBrush(Colors.White));
            ctx.DrawText(label, new Point(4, y + 8));

            for (int step = 0; step < length; step++)
            {
                double x = LabelW + step * StepW;
                var cell = new Rect(x + 1, y + 4, StepW - 2, RowH - 8);
                bool hasNote = _ctrl.HasNoteAt(step, ch);
                bool selected = step == _ctrl.SelectedRow && ch == _ctrl.SelectedChannel;
                bool hover = step == _hoverRow && ch == _hoverChannel;

                var fill = MusicTheme.GridBg;
                if (selected) fill = MusicTheme.Accent;
                else if (hover) fill = MusicTheme.ButtonFace;
                else if (hasNote) fill = Color.FromArgb(180, 255, 255, 255);

                ctx.FillRectangle(new SolidColorBrush(fill), cell);
                ctx.DrawRectangle(new Pen(new SolidColorBrush(MusicTheme.PanelBorder), 1), cell);

                var glyph = new FormattedText(hasNote ? "\u266A" : "+", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, hasNote ? 10 : 12,
                    new SolidColorBrush(hasNote ? MusicTheme.PanelBorder : MusicTheme.TextDim));
                ctx.DrawText(glyph, new Point(x + 6, y + 6));
            }
        }

        if (playhead >= 0 && playhead < length)
        {
            double x = LabelW + playhead * StepW;
            double top = 2;
            double bottom = channels * RowH - 2;
            var playheadPen = new Pen(new SolidColorBrush(Colors.White), 2);
            ctx.DrawLine(playheadPen, new Point(x + StepW / 2.0, top), new Point(x + StepW / 2.0, bottom));

            var headRect = new Rect(x + 2, top, StepW - 4, 4);
            ctx.FillRectangle(new SolidColorBrush(MusicTheme.Playhead), headRect);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_ctrl == null) return new Size(400, 120);
        return new Size(LabelW + _ctrl.Length * StepW + 4, _ctrl.ChannelCount * RowH + 4);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_ctrl == null) return;
        var (row, ch) = HitTest(e.GetPosition(this));
        if (row < 0 || ch < 0) return;
        _ctrl.SelectCell(row, ch);
        _ctrl.ToggleCell(row, ch);
        InvalidateVisual();
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        var (row, ch) = HitTest(e.GetPosition(this));
        if (row != _hoverRow || ch != _hoverChannel)
        {
            _hoverRow = row;
            _hoverChannel = ch;
            InvalidateVisual();
        }
    }

    private (int row, int ch) HitTest(Point pos)
    {
        if (_ctrl == null || pos.X < LabelW) return (-1, -1);
        int ch = (int)(pos.Y / RowH);
        int row = (int)((pos.X - LabelW) / StepW);
        if (ch < 0 || ch >= _ctrl.ChannelCount || row < 0 || row >= _ctrl.Length)
            return (-1, -1);
        return (row, ch);
    }
}
