using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ferbento.Core.Audio;
using Ferbento.Editor.ViewModels;

namespace Ferbento.Editor.Views;

public class SfxEditorView : UserControl
{
    private readonly SfxEditorViewModel _vm = new();
    private readonly SfxGridCanvas _gridCanvas;
    private readonly Grid _root;
    private readonly ScrollViewer _scroller = new();
    private readonly DispatcherTimer _playbackTimer = new();
    private readonly Button _playBtn;
    private readonly Button _stopBtn;

    private readonly NumericUpDown _slotControl;
    private readonly NumericUpDown _channelControl;
    private readonly NumericUpDown _speedControl;
    private readonly NumericUpDown _loopStartControl;
    private readonly NumericUpDown _loopEndControl;
    private readonly ComboBox _waveformControl;

    private readonly NumericUpDown _detailVolume;
    private readonly ComboBox _detailEffect;
    private readonly NumericUpDown _detailEffectParam;
    private readonly TextBlock _detailPitchLabel;
    private readonly StackPanel _detailPanel;

    public SfxEditorView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _gridCanvas = new SfxGridCanvas(_vm);

        _playBtn = new Button { Content = "\u25B6 Play", Padding = new Thickness(8, 2) };
        _stopBtn = new Button { Content = "\u23F9 Stop", Padding = new Thickness(8, 2) };

        _slotControl = new NumericUpDown { Value = 0, Minimum = 0, Maximum = 63, Width = 55 };
        _channelControl = new NumericUpDown { Value = 0, Minimum = 0, Maximum = 3, Width = 50 };
        _speedControl = new NumericUpDown { Value = 4, Minimum = 1, Maximum = 255, Width = 55 };
        _loopStartControl = new NumericUpDown { Value = 0, Minimum = 0, Maximum = 31, Width = 45 };
        _loopEndControl = new NumericUpDown { Value = 31, Minimum = 0, Maximum = 31, Width = 45 };
        _waveformControl = new ComboBox { Width = 90, ItemsSource = SfxEditorViewModel.WaveformNames, SelectedIndex = 0 };

        _detailPitchLabel = new TextBlock
        {
            Text = "--",
            Foreground = new SolidColorBrush(Color.Parse("#FFB347")),
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Width = 50,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _detailVolume = new NumericUpDown { Value = 7, Minimum = 0, Maximum = 7, Width = 50 };
        _detailEffect = new ComboBox
        {
            Width = 80,
            ItemsSource = SfxEditorViewModel.EffectNames,
            SelectedIndex = 0,
        };
        _detailEffectParam = new NumericUpDown { Value = 0, Minimum = 0, Maximum = 255, Width = 60 };

        _detailPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 4),
            IsVisible = false,
            Children =
            {
                Label("Note:"),
                _detailPitchLabel,
                Label("Vol:"),
                _detailVolume,
                Label("FX:"),
                _detailEffect,
                Label("Val:"),
                _detailEffectParam,
            },
        };

        WireEvents();

        _playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 30);
        _playbackTimer.Tick += (_, _) =>
        {
            _vm.TickPlayback();
            _gridCanvas.PlayheadCol = _vm.GetPlayheadColumn();
            _gridCanvas.InvalidateVisual();
        };
        _playbackTimer.Start();

        _root = BuildLayout();
        Content = _root;
    }

    private void WireEvents()
    {
        _playBtn.Click += (_, _) => _vm.Play();
        _stopBtn.Click += (_, _) => _vm.Stop();

        _slotControl.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _vm.SelectedSlot = (int)e.NewValue.Value;
        };

        _channelControl.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _vm.SelectedChannel = (int)e.NewValue.Value;
        };

        _speedControl.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _vm.Speed = (int)e.NewValue.Value;
        };

        _loopStartControl.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _vm.LoopStart = (int)e.NewValue.Value;
        };

        _loopEndControl.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _vm.LoopEnd = (int)e.NewValue.Value;
        };

        _waveformControl.SelectionChanged += (_, e) =>
        {
            if (e.AddedItems.Count > 0)
                _vm.DefaultWaveform = _waveformControl.SelectedIndex;
        };

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(_vm.Speed):
                    _speedControl.Value = _vm.Speed;
                    break;
                case nameof(_vm.LoopStart):
                    _loopStartControl.Value = _vm.LoopStart;
                    break;
                case nameof(_vm.LoopEnd):
                    _loopEndControl.Value = _vm.LoopEnd;
                    break;
                case nameof(_vm.SelectedSlot):
                    _slotControl.Value = _vm.SelectedSlot;
                    break;
                case nameof(_vm.SelectedChannel):
                    _channelControl.Value = _vm.SelectedChannel;
                    break;
                case nameof(_vm.ActiveSlot):
                    _gridCanvas.InvalidateVisual();
                    break;
                case nameof(_vm.HasSelectedNote):
                    _detailPanel.IsVisible = _vm.HasSelectedNote;
                    if (_vm.HasSelectedNote)
                    {
                        _detailPitchLabel.Text = _vm.SelectedNotePitch;
                        _detailVolume.Value = _vm.SelectedNoteVolume;
                        _detailEffect.SelectedIndex = (int)_vm.SelectedNoteEffect;
                        _detailEffectParam.Value = _vm.SelectedNoteEffectParam;
                    }
                    break;
            }
        };

        _detailVolume.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue && _vm.HasSelectedNote)
                _vm.SelectedNoteVolume = (int)e.NewValue.Value;
        };

        _detailEffect.SelectionChanged += (_, e) =>
        {
            if (_vm.HasSelectedNote && e.AddedItems.Count > 0)
                _vm.SelectedNoteEffect = (SfxEffect)_detailEffect.SelectedIndex;
        };

        _detailEffectParam.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue && _vm.HasSelectedNote)
                _vm.SelectedNoteEffectParam = (int)e.NewValue.Value;
        };
    }

    private Grid BuildLayout()
    {
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var topBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 4),
            Children =
            {
                Label("Slot:"),
                _slotControl,
                SeparatorV(),
                Label("Ch:"),
                _channelControl,
                SeparatorV(),
                Label("Wave:"),
                _waveformControl,
                SeparatorV(),
                Label("Spd:"),
                _speedControl,
                SeparatorV(),
                Label("Loop:"),
                _loopStartControl,
                Label("-"),
                _loopEndControl,
                SeparatorV(),
                _playBtn,
                _stopBtn,
                new Button { Content = "Clear", Padding = new Thickness(8, 2) },
            },
        };

        var clearBtn = (Button)topBar.Children[^1];
        clearBtn.Click += (_, _) => _vm.ClearPatternAll();

        _scroller.Content = _gridCanvas;
        _scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        Grid.SetRow(topBar, 0);
        Grid.SetRow(_scroller, 1);
        Grid.SetRow(_detailPanel, 2);

        layout.Children.Add(topBar);
        layout.Children.Add(_scroller);
        layout.Children.Add(_detailPanel);

        return layout;
    }

    private static Border SeparatorV() => new()
    {
        Width = 1,
        Height = 20,
        Background = new SolidColorBrush(Color.Parse("#3A3A3A")),
        Margin = new Thickness(2, 0),
    };

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center,
    };
}

internal sealed class SfxGridCanvas : Control
{
    private readonly SfxEditorViewModel _vm;

    private const int CellW = 22;
    private const int CellH = 16;
    private const int PitchLabelW = 36;
    private const int HeaderH = 18;

    private int _hoverStep = -1;
    private int _hoverPitch = -1;
    public int PlayheadCol = -1;

    private static readonly Color BgColor = Color.Parse("#0E0E0E");
    private static readonly Color GridLine = Color.Parse("#1A1A2E");
    private static readonly Color HeaderBg = Color.Parse("#1E1E1E");
    private static readonly Color NoteColor = Color.Parse("#FFB347");
    private static readonly Color NotePlayingColor = Color.Parse("#44FF44");
    private static readonly Color HoverColor = Color.Parse("#334466");
    private static readonly Color PlayColor = Color.Parse("#44AA44");
    private static readonly Color SelectedColor = Color.Parse("#555599");

    private static readonly Typeface Typeface = Typeface.Default;

    public SfxGridCanvas(SfxEditorViewModel vm)
    {
        _vm = vm;
        Width = PitchLabelW + 32 * CellW + 4;
        Height = HeaderH + SfxEditorViewModel.PitchNames.Count * CellH + 4;

        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerExited += (_, _) => { _hoverStep = -1; _hoverPitch = -1; InvalidateVisual(); };
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        var (col, row) = PosToGrid(pos);
        if (col >= 0 && col < 32 && row >= 0)
        {
            int midiNote = 48 + row;
            if (midiNote <= 108)
            {
                _vm.SetNote(col, midiNote);
                _vm.SelectedStep = col;
            }
        }
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var (col, row) = PosToGrid(pos);
        if (col != _hoverStep || row != _hoverPitch)
        {
            _hoverStep = col;
            _hoverPitch = row;
            InvalidateVisual();
        }
    }

    private (int col, int row) PosToGrid(Point pos)
    {
        if (pos.X < PitchLabelW || pos.Y < HeaderH)
            return (-1, -1);
        int col = (int)((pos.X - PitchLabelW) / CellW);
        int row = (int)((pos.Y - HeaderH) / CellH);
        return (col, row);
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var pitchNames = SfxEditorViewModel.PitchNames;
        int totalCols = 32;
        int totalRows = pitchNames.Count;
        int w = PitchLabelW + totalCols * CellW;
        int h = HeaderH + totalRows * CellH;

        ctx.FillRectangle(new SolidColorBrush(HeaderBg), new Rect(0, 0, w, HeaderH));

        for (int c = 0; c < totalCols; c++)
        {
            string label = c.ToString("X").PadLeft(2, '0');
            double x = PitchLabelW + c * CellW + 2;
            var ft = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, 9, null);
            ctx.DrawText(ft, new Point(x, 2));
        }

        for (int r = 0; r < totalRows; r++)
        {
            double y = HeaderH + r * CellH;
            string pitchName = pitchNames[r];

            var pft = new FormattedText(pitchName, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, 9, null);
            ctx.DrawText(pft, new Point(2, y + 1));

            bool isOctaveStart = pitchName.Contains('C') && (pitchName.EndsWith("4") || pitchName.EndsWith("5") || pitchName.EndsWith("6") || pitchName.EndsWith("7") || pitchName.EndsWith("8"));

            for (int c = 0; c < totalCols; c++)
            {
                double x = PitchLabelW + c * CellW;
                var cellRect = new Rect(x, y, CellW, CellH);

                bool hasNote = _vm.HasNoteAt(c, 48 + r);
                bool isHover = c == _hoverStep && r == _hoverPitch;
                bool isPlayhead = c == PlayheadCol;

                Color cellColor = BgColor;
                if (isPlayhead && hasNote)
                    cellColor = NotePlayingColor;
                else if (isPlayhead)
                    cellColor = Color.FromArgb(60, 68, 170, 68);
                else if (isHover)
                    cellColor = HoverColor;
                else if (hasNote)
                    cellColor = NoteColor;
                else if (isOctaveStart)
                    cellColor = Color.FromArgb(15, 255, 255, 255);

                ctx.FillRectangle(new SolidColorBrush(cellColor), cellRect);

                ctx.DrawLine(new Pen(new SolidColorBrush(GridLine), 0.3), new Point(x, y), new Point(x + CellW, y));
                ctx.DrawLine(new Pen(new SolidColorBrush(GridLine), 0.3), new Point(x, y), new Point(x, y + CellH));
            }
        }

        ctx.DrawLine(new Pen(new SolidColorBrush(GridLine), 0.5), new Point(0, h - 1), new Point(w, h - 1));
        ctx.DrawLine(new Pen(new SolidColorBrush(GridLine), 0.5), new Point(w - 1, 0), new Point(w - 1, h));
    }
}
