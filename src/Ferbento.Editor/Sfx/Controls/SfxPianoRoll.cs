using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
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
  private bool _batchEdit;
  private Point _dragStart;
  private int _rubberFromStep;
  private int _rubberFromRow;
  private int _lastPaintStep = -1;
  private int _lastPaintRow = -1;
  private bool _redrawQueued;

  private static readonly IBrush HeaderBgBrush = new SolidColorBrush(SfxTheme.HeaderBg);
  private static readonly IBrush CanvasBgBrush = new SolidColorBrush(SfxTheme.CanvasBackground);
  private static readonly IBrush MutedTextBrush = new SolidColorBrush(SfxTheme.MutedText);
  private static readonly IBrush PlayheadBrush = new SolidColorBrush(SfxTheme.Playhead);
  private static readonly IBrush LoopRegionBrush = new SolidColorBrush(SfxTheme.LoopRegion);
  private static readonly IBrush HoverBrush = new SolidColorBrush(SfxTheme.Hover);
  private static readonly IBrush SelectionBrush = new SolidColorBrush(SfxTheme.SelectionOutline);
  private static readonly IBrush CursorBrush = new SolidColorBrush(SfxTheme.CursorFocus);
  private static readonly IBrush BlackKeyBrush = new SolidColorBrush(Color.Parse("#222222"));
  private static readonly IBrush WhiteKeyBrush = new SolidColorBrush(Color.Parse("#2A2A2A"));
  private static readonly IBrush OctaveLineBrush = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
  private static readonly Pen GridPen = new(new SolidColorBrush(SfxTheme.GridLine), 0.3);
  private static readonly Pen PlayheadPen = new(PlayheadBrush, 2);
  private static readonly Pen SelectionPen = new(SelectionBrush, 2);
  private static readonly Pen CustomInstPen = new(new SolidColorBrush(SfxTheme.CustomInstAccent), 1.5);
  private static readonly Pen CursorPen = new(CursorBrush, 1.5);

  private static readonly IBrush[] NoteBrushes = BuildNoteBrushes();
  private static readonly IBrush PlayNoteBrush = new SolidColorBrush(SfxTheme.Playhead);

  private FormattedText[]? _stepLabels;
  private FormattedText[]? _pitchLabels;
  private int _cachedSteps;
  private int _cachedRows;

  public event Action<int, int>? StepClicked;
  public event Action<int>? StepHeaderClicked;
  public event Action<int>? PianoKeyClicked;

  public void Bind(SfxEditorController controller)
  {
    _ctrl = controller;
    RebuildTextCache();
    InvalidateMeasure();
    RequestRedraw();
  }

  private static IBrush[] BuildNoteBrushes()
  {
    var brushes = new IBrush[8 * 8];
    for (int inst = 0; inst < 8; inst++)
    {
      for (int vol = 0; vol < 8; vol++)
        brushes[inst * 8 + vol] = new SolidColorBrush(SfxTheme.WaveformColor(inst, vol));
    }

    return brushes;
  }

  private static IBrush NoteBrush(SfxNote note)
  {
    int idx = Math.Clamp(note.Instrument & 7, 0, 7) * 8 + Math.Clamp(note.Volume, 0, 7);
    return NoteBrushes[idx];
  }

  private void RebuildTextCache()
  {
    if (_ctrl == null) return;
    int steps = _ctrl.NotesPerSfx;
    int rows = SfxPitch.PitchNames.Count;
    _cachedSteps = steps;
    _cachedRows = rows;

    _stepLabels = new FormattedText[steps];
    for (int c = 0; c < steps; c++)
    {
      string label = c.ToString("X2", CultureInfo.InvariantCulture);
      _stepLabels[c] = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        Typeface.Default, 9, MutedTextBrush);
    }

    var pitchNames = SfxPitch.PitchNames;
    _pitchLabels = new FormattedText[rows];
    for (int r = 0; r < rows; r++)
    {
      _pitchLabels[r] = new FormattedText(pitchNames[r], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        Typeface.Default, 8, MutedTextBrush);
    }
  }

  private void RequestRedraw()
  {
    if (_redrawQueued) return;
    _redrawQueued = true;
    Dispatcher.UIThread.Post(() =>
    {
      _redrawQueued = false;
      InvalidateVisual();
    }, DispatcherPriority.Render);
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
    PointerCaptureLost += (_, _) => EndDrag();
    PointerExited += (_, _) =>
    {
      if (_dragging) return;
      _hoverStep = -1;
      _hoverRow = -1;
      RequestRedraw();
    };
  }

  private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    if (_ctrl == null) return;
    e.Pointer.Capture(this);
    var pos = e.GetPosition(this);
    var (step, row, header, piano) = HitTest(pos);
    var props = e.GetCurrentPoint(this).Properties;

    if (piano && row >= 0)
    {
      PianoKeyClicked?.Invoke(SfxPitch.RowToPitch(row));
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
      RequestRedraw();
      return;
    }

    if (step < 0 || row < 0) return;

    _dragging = true;
    _dragStart = pos;
    _rubberFromStep = step;
    _rubberFromRow = row;
    _lastPaintStep = -1;
    _lastPaintRow = -1;

    if (props.IsRightButtonPressed)
    {
      BeginPaintBatch();
      _ctrl.DeleteNote(step);
      RequestRedraw();
      return;
    }

    if (_ctrl.Tool == SfxEditorTool.Eraser)
    {
      BeginPaintBatch();
      _ctrl.DeleteNote(step);
      RequestRedraw();
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

      RequestRedraw();
      return;
    }

    if (_ctrl.Tool == SfxEditorTool.Fill)
    {
      _ctrl.CursorStep = step;
      _ctrl.CursorRow = row;
      _ctrl.FillFromCursor();
      RequestRedraw();
      return;
    }

    BeginPaintBatch();
    PaintCell(step, row, preview: true, updateCursor: true);
    StepClicked?.Invoke(step, row);
    RequestRedraw();
  }

  private void OnPointerMoved(object? sender, PointerEventArgs e)
  {
    if (_ctrl == null) return;
    var pos = e.GetPosition(this);
    var (step, row, _, _) = HitTest(pos);

    bool hoverChanged = step != _hoverStep || row != _hoverRow;
    _hoverStep = step;
    _hoverRow = row;

    if (!_dragging)
    {
      if (hoverChanged) RequestRedraw();
      return;
    }

    if (_ctrl.Tool == SfxEditorTool.Pencil && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
      if (step >= 0 && row >= 0)
        PaintCell(step, row, preview: false, updateCursor: false);
    }
    else if (_ctrl.Tool == SfxEditorTool.Eraser && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
      if (step >= 0 && step != _lastPaintStep)
      {
        _lastPaintStep = step;
        _ctrl.DeleteNote(step);
      }
    }
    else if (_rubberBand && step >= 0)
    {
      _ctrl.SelectRange(_rubberFromStep, step);
    }

    RequestRedraw();
  }

  private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
  {
    e.Pointer.Capture(null);
    EndDrag();
  }

  private void EndDrag()
  {
    if (_batchEdit)
    {
      _ctrl?.EndBatchEdit();
      _batchEdit = false;
    }

    if (_lastPaintStep >= 0 && _lastPaintRow >= 0 && _ctrl != null)
      _ctrl.MoveCursorTo(_lastPaintStep, _lastPaintRow);

    _dragging = false;
    _rubberBand = false;
    _lastPaintStep = -1;
    _lastPaintRow = -1;
  }

  private void BeginPaintBatch()
  {
    if (_batchEdit || _ctrl == null) return;
    _ctrl.BeginBatchEdit();
    _batchEdit = true;
  }

  private void PaintCell(int step, int row, bool preview, bool updateCursor)
  {
    if (_ctrl == null) return;
    if (step == _lastPaintStep && row == _lastPaintRow) return;
    _lastPaintStep = step;
    _lastPaintRow = row;
    _ctrl.PlaceNote(step, SfxPitch.RowToPitch(row), preview, updateCursor);
  }

  private (int step, int row, bool header, bool piano) HitTest(Point pos)
  {
    if (_ctrl == null) return (-1, -1, false, false);

    if (pos.X < PianoW && pos.Y >= HeaderH)
    {
      int row = (int)((pos.Y - HeaderH) / CellH);
      if (row >= SfxPitch.PitchNames.Count) row = -1;
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
    if (r >= SfxPitch.PitchNames.Count) return (-1, -1, false, false);
    return (col, r, false, false);
  }

  public override void Render(DrawingContext ctx)
  {
    base.Render(ctx);
    if (_ctrl == null) return;

    int steps = _ctrl.NotesPerSfx;
    int rows = SfxPitch.PitchNames.Count;
    if (_stepLabels == null || _pitchLabels == null || _cachedSteps != steps || _cachedRows != rows)
      RebuildTextCache();

    int cellW = _ctrl.CellWidth;
    int w = PianoW + steps * cellW;
    int h = HeaderH + rows * CellH;
    int playhead = _ctrl.PlayheadColumn;
    int cursorStep = _ctrl.CursorStep;
    int cursorRow = _ctrl.CursorRow;

    ctx.FillRectangle(HeaderBgBrush, new Rect(0, 0, w, HeaderH));
    ctx.FillRectangle(CanvasBgBrush, new Rect(PianoW, HeaderH, steps * cellW, rows * CellH));

    for (int c = 0; c < steps; c++)
    {
      double x = PianoW + c * cellW;
      if (_stepLabels != null)
        ctx.DrawText(_stepLabels[c], new Point(x + 2, 2));

      if (c >= _ctrl.LoopStart && c <= _ctrl.LoopEnd)
      {
        const int loopBarH = 4;
        ctx.FillRectangle(LoopRegionBrush, new Rect(x, HeaderH - loopBarH, cellW, loopBarH));
      }

      if (c == playhead)
        ctx.DrawLine(PlayheadPen, new Point(x, HeaderH), new Point(x, h));
    }

    for (int r = 0; r < rows; r++)
    {
      double y = HeaderH + r * CellH;
      string pitchName = SfxPitch.PitchNames[r];
      bool blackKey = pitchName.Contains('#');
      ctx.FillRectangle(blackKey ? BlackKeyBrush : WhiteKeyBrush, new Rect(0, y, PianoW, CellH));
      if (_pitchLabels != null)
        ctx.DrawText(_pitchLabels[r], new Point(2, y + 1));

      bool octaveLine = pitchName.StartsWith("C", StringComparison.Ordinal);
      if (octaveLine)
        ctx.FillRectangle(OctaveLineBrush, new Rect(PianoW, y, steps * cellW, CellH));
    }

    if (_hoverStep >= 0 && _hoverRow >= 0 && !_ctrl.HasNoteAt(_hoverStep, SfxPitch.RowToPitch(_hoverRow)))
    {
      double x = PianoW + _hoverStep * cellW;
      double y = HeaderH + _hoverRow * CellH;
      ctx.FillRectangle(HoverBrush, new Rect(x, y, cellW, CellH));
    }

    for (int c = 0; c < steps; c++)
    {
      var note = _ctrl.GetNote(c);
      if (note.IsEmpty) continue;

      int row = SfxPitch.PitchToRow(note.Pitch);
      if (row < 0 || row >= rows) continue;

      double x = PianoW + c * cellW;
      double y = HeaderH + row * CellH;
      var cellRect = new Rect(x, y, cellW, CellH);
      bool isPlay = c == playhead;
      bool isSelected = _ctrl.IsStepSelected(c);

      ctx.FillRectangle(isPlay ? PlayNoteBrush : NoteBrush(note), cellRect);
      if (note.CustomInst)
        ctx.DrawRectangle(CustomInstPen, cellRect.Deflate(1.5));
      if (isSelected)
        ctx.DrawRectangle(SelectionPen, cellRect.Deflate(1));
    }

    if (cursorRow >= 0 && cursorRow < rows)
    {
      double x = PianoW + cursorStep * cellW;
      double y = HeaderH + cursorRow * CellH;
      ctx.DrawRectangle(CursorPen, new Rect(x, y, cellW, CellH).Deflate(0.5));
    }

    for (int c = 0; c <= steps; c++)
    {
      double x = PianoW + c * cellW;
      ctx.DrawLine(GridPen, new Point(x, HeaderH), new Point(x, h));
    }

    for (int r = 0; r <= rows; r++)
    {
      double y = HeaderH + r * CellH;
      ctx.DrawLine(GridPen, new Point(PianoW, y), new Point(w, y));
    }
  }
}
