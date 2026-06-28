using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ferbento.Core.Audio;
using Ferbento.Editor.Sfx;

namespace Ferbento.Editor.Sfx.Controls;

public sealed class SfxPianoRoll : Control
{
  private SfxEditorController? _ctrl;
  private const int PianoW = 32;
  private const int HeaderH = 18;
  private const int CellH = 14;

  private int _hoverStep = -1;
  private int _hoverRow = -1;
  private bool _dragging;
  private bool _rubberBand;
  private Point _dragStart;
  private int _rubberFromStep;
  private int _rubberFromRow;

  public event Action<int, int>? StepClicked;
  public event Action<int>? StepHeaderClicked;
  public event Action<int>? PianoKeyClicked;

  public void Bind(SfxEditorController controller)
  {
    _ctrl = controller;
    InvalidateMeasure();
    InvalidateVisual();
  }

  protected override Size MeasureOverride(Size availableSize)
  {
    if (_ctrl == null) return new Size(400, 200);
    int w = PianoW + _ctrl.NotesPerSfx * _ctrl.CellWidth + 4;
    int h = HeaderH + SfxPitch.PitchNames.Count * CellH + 4;
    return new Size(w, h);
  }

  public SfxPianoRoll()
  {
    Focusable = true;
    PointerPressed += OnPointerPressed;
    PointerMoved += OnPointerMoved;
    PointerReleased += OnPointerReleased;
    PointerExited += (_, _) =>
    {
      _hoverStep = -1;
      _hoverRow = -1;
      InvalidateVisual();
    };
  }

  private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    if (_ctrl == null) return;
    var pos = e.GetPosition(this);
    var (step, row, header, piano) = HitTest(pos);
    var props = e.GetCurrentPoint(this).Properties;

    if (piano && row >= 0)
    {
      PianoKeyClicked?.Invoke(SfxPitch.RowToMidi(row));
      return;
    }

    if (header && step >= 0)
    {
      if (props.IsRightButtonPressed)
        _ctrl.DeleteNote(step);
      else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        StepHeaderClicked?.Invoke(step);
      else
        _ctrl.SelectStep(step, e.KeyModifiers.HasFlag(KeyModifiers.Control));
      InvalidateVisual();
      return;
    }

    if (step < 0 || row < 0) return;

    _dragging = true;
    _dragStart = pos;
    _rubberFromStep = step;
    _rubberFromRow = row;

    if (props.IsRightButtonPressed)
    {
      _ctrl.DeleteNote(step);
      InvalidateVisual();
      return;
    }

    if (_ctrl.Tool == SfxEditorTool.Eraser)
    {
      _ctrl.DeleteNote(step);
      return;
    }

    if (_ctrl.Tool == SfxEditorTool.Select)
    {
      if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        _ctrl.SelectStep(step, additive: true);
      else
      {
        _rubberBand = true;
        _ctrl.SelectStep(step);
      }

      InvalidateVisual();
      return;
    }

    if (_ctrl.Tool == SfxEditorTool.Fill)
    {
      _ctrl.CursorStep = step;
      _ctrl.CursorRow = row;
      _ctrl.FillFromCursor();
      InvalidateVisual();
      return;
    }

  if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
    {
      _ctrl.PlaceNote(step, SfxPitch.RowToMidi(row));
      InvalidateVisual();
      return;
    }

    _ctrl.PlaceNote(step, SfxPitch.RowToMidi(row));
    StepClicked?.Invoke(step, row);
    InvalidateVisual();
  }

  private void OnPointerMoved(object? sender, PointerEventArgs e)
  {
    if (_ctrl == null) return;
    var pos = e.GetPosition(this);
    var (step, row, _, _) = HitTest(pos);

    if (step != _hoverStep || row != _hoverRow)
    {
      _hoverStep = step;
      _hoverRow = row;
      InvalidateVisual();
    }

    if (!_dragging) return;

    if (_ctrl.Tool == SfxEditorTool.Pencil && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
      if (step >= 0 && row >= 0)
        _ctrl.PlaceNote(step, SfxPitch.RowToMidi(row));
    }
    else if (_ctrl.Tool == SfxEditorTool.Eraser && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
      if (step >= 0)
        _ctrl.DeleteNote(step);
    }
    else if (_rubberBand && step >= 0)
    {
      _ctrl.SelectRange(_rubberFromStep, step);
    }

    InvalidateVisual();
  }

  private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
  {
    _dragging = false;
    _rubberBand = false;
  }

  private (int step, int row, bool header, bool piano) HitTest(Point pos)
  {
    if (_ctrl == null) return (-1, -1, false, false);

    if (pos.X < PianoW && pos.Y >= HeaderH)
    {
      int row = (int)((pos.Y - HeaderH) / CellH);
      return (-1, row, false, true);
    }

    if (pos.Y < HeaderH && pos.X >= PianoW)
    {
      int step = (int)((pos.X - PianoW) / _ctrl.CellWidth);
      return (step, -1, true, false);
    }

    if (pos.X < PianoW || pos.Y < HeaderH)
      return (-1, -1, false, false);

    int col = (int)((pos.X - PianoW) / _ctrl.CellWidth);
    int r = (int)((pos.Y - HeaderH) / CellH);
    return (col, r, false, false);
  }

  public override void Render(DrawingContext ctx)
  {
    base.Render(ctx);
    if (_ctrl == null) return;

    var pitchNames = SfxPitch.PitchNames;
    int steps = _ctrl.NotesPerSfx;
    int cellW = _ctrl.CellWidth;
    int rows = pitchNames.Count;
    int w = PianoW + steps * cellW;
    int h = HeaderH + rows * CellH;
    int playhead = _ctrl.PlayheadColumn;

    ctx.FillRectangle(new SolidColorBrush(SfxTheme.HeaderBg), new Rect(0, 0, w, HeaderH));

    for (int c = 0; c < steps; c++)
    {
      double x = PianoW + c * cellW;

      string label = c.ToString("X2", CultureInfo.InvariantCulture);
      var ft = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 9,
        c == _ctrl.CursorStep ? Brushes.White : new SolidColorBrush(SfxTheme.MutedText));
      ctx.DrawText(ft, new Point(x + 2, 2));

      if (c >= _ctrl.LoopStart && c <= _ctrl.LoopEnd)
      {
        const int loopBarH = 4;
        ctx.FillRectangle(
          new SolidColorBrush(SfxTheme.LoopRegion),
          new Rect(x, HeaderH - loopBarH, cellW, loopBarH));
      }

      if (c == playhead)
        ctx.DrawLine(new Pen(new SolidColorBrush(SfxTheme.Playhead), 2), new Point(x, HeaderH), new Point(x, h));
    }

    for (int r = 0; r < rows; r++)
    {
      double y = HeaderH + r * CellH;
      string pitchName = pitchNames[r];
      bool blackKey = pitchName.Contains('#');

      ctx.FillRectangle(
        new SolidColorBrush(blackKey ? Color.Parse("#222222") : Color.Parse("#2A2A2A")),
        new Rect(0, y, PianoW, CellH));

      var pft = new FormattedText(pitchName, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default,
        8, new SolidColorBrush(SfxTheme.MutedText));
      ctx.DrawText(pft, new Point(2, y + 1));

      bool octaveLine = pitchName.StartsWith("C", StringComparison.Ordinal);

      for (int c = 0; c < steps; c++)
      {
        double x = PianoW + c * cellW;
        var cellRect = new Rect(x, y, cellW, CellH);
        int midi = SfxPitch.RowToMidi(r);
        bool hasNote = _ctrl.HasNoteAt(c, midi);
        bool isCursor = c == _ctrl.CursorStep && r == _ctrl.CursorRow;
        bool isSelected = _ctrl.IsStepSelected(c) && hasNote;
        bool isHover = c == _hoverStep && r == _hoverRow;
        bool isPlay = c == playhead && hasNote;

        Color cellColor = SfxTheme.CanvasBackground;
        if (octaveLine && !hasNote)
          cellColor = Color.FromArgb(15, 255, 255, 255);
        if (isHover && !hasNote)
          cellColor = SfxTheme.Hover;
        if (hasNote)
          cellColor = isPlay ? SfxTheme.Playhead : SfxTheme.WaveformColor(_ctrl.GetNote(c));

        ctx.FillRectangle(new SolidColorBrush(cellColor), cellRect);

        if (isSelected)
        {
          ctx.DrawRectangle(new Pen(new SolidColorBrush(SfxTheme.SelectionOutline), 2), cellRect.Deflate(1));
        }

        if (isCursor)
        {
          ctx.DrawRectangle(
            new Pen(new SolidColorBrush(SfxTheme.CursorFocus), 1.5),
            cellRect.Deflate(0.5));
        }

        ctx.DrawRectangle(new Pen(new SolidColorBrush(SfxTheme.GridLine), 0.3), cellRect);
      }
    }
  }
}
