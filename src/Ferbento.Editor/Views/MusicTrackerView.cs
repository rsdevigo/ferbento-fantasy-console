using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ferbento.Editor.ViewModels;

namespace Ferbento.Editor.Views;

public class MusicTrackerView : UserControl
{
    private readonly MusicTrackerViewModel _vm = new();
    private readonly MusicGridCanvas _gridCanvas;
    private readonly Grid _root;
    private readonly ScrollViewer _scroller = new();
    private readonly DispatcherTimer _playbackTimer = new();
    private readonly Button _playBtn;
    private readonly Button _stopBtn;
    private readonly NumericUpDown _patternControl;
    private readonly NumericUpDown _speedControl;
    private readonly NumericUpDown _loopStartControl;
    private readonly NumericUpDown _loopEndControl;

    public MusicTrackerView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _gridCanvas = new MusicGridCanvas(_vm);

        _playBtn = new Button { Content = "\u25B6 Play", Padding = new Thickness(8, 2) };
        _stopBtn = new Button { Content = "\u23F9 Stop", Padding = new Thickness(8, 2) };
        _patternControl = new NumericUpDown { Value = 0, Minimum = 0, Maximum = 63, Width = 60 };
        _speedControl = new NumericUpDown { Value = 8, Minimum = 1, Maximum = 32, Width = 55 };
        _loopStartControl = new NumericUpDown { Value = 0, Minimum = 0, Maximum = 63, Width = 45 };
        _loopEndControl = new NumericUpDown { Value = 63, Minimum = 0, Maximum = 63, Width = 45 };

        WireEvents();

        _playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 30);
        _playbackTimer.Tick += (_, _) =>
        {
            _vm.TickPlayback();
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

        _patternControl.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue)
                _vm.CurrentPattern = (int)e.NewValue.Value;
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

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(_vm.CurrentPattern):
                    _patternControl.Value = _vm.CurrentPattern;
                    break;
                case nameof(_vm.Speed):
                    _speedControl.Value = _vm.Speed;
                    break;
                case nameof(_vm.LoopStart):
                    _loopStartControl.Value = _vm.LoopStart;
                    break;
                case nameof(_vm.LoopEnd):
                    _loopEndControl.Value = _vm.LoopEnd;
                    break;
                case nameof(_vm.PatternData):
                    _gridCanvas.InvalidateVisual();
                    break;
            }
        };
    }

    private Grid BuildLayout()
    {
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Music Tracker — 64 patterns, 4 channels",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 12,
            },
        };

        var topBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 4),
            Children =
            {
                Label("Pat:"),
                _patternControl,
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
        clearBtn.Click += (_, _) => _vm.ClearPattern();

        _scroller.Content = _gridCanvas;
        _scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        var topPanel = new StackPanel
        {
            Children = { header, topBar },
        };

        Grid.SetRow(topPanel, 0);
        Grid.SetRow(_scroller, 1);

        layout.Children.Add(topPanel);
        layout.Children.Add(_scroller);

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

internal sealed class MusicGridCanvas : Control
{
    private readonly MusicTrackerViewModel _vm;

    private const int RowH = 18;
    private const int RowNumW = 30;
    private const int CellW = 72;
    private const int HeaderH = 20;

    private const int TotalRows = 64;
    private const int TotalChannels = 4;

    private int _hoverRow = -1;
    private int _hoverCh = -1;

    private static readonly Color BgColor = Color.Parse("#0E0E0E");
    private static readonly Color GridLine = Color.Parse("#1A1A2E");
    private static readonly Color HeaderBg = Color.Parse("#1E1E1E");
    private static readonly Color NoteColor = Color.Parse("#44AA44");
    private static readonly Color HoverColor = Color.Parse("#334466");
    private static readonly Color AltRowColor = Color.FromArgb(10, 255, 255, 255);

    private static readonly Typeface Typeface = Typeface.Default;
    private static readonly string[] ChLabels = { "CH1", "CH2", "CH3", "CH4" };

    public MusicGridCanvas(MusicTrackerViewModel vm)
    {
        _vm = vm;
        Width = RowNumW + TotalChannels * CellW + 4;
        Height = HeaderH + TotalRows * RowH + 4;

        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerExited += (_, _) => { _hoverRow = -1; _hoverCh = -1; InvalidateVisual(); };
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        var (row, ch) = PosToGrid(pos);
        if (row >= 0 && row < TotalRows && ch >= 0 && ch < TotalChannels)
        {
            _vm.ToggleCell(row, ch);
        }
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var (row, ch) = PosToGrid(pos);
        if (row != _hoverRow || ch != _hoverCh)
        {
            _hoverRow = row;
            _hoverCh = ch;
            InvalidateVisual();
        }
    }

    private (int row, int ch) PosToGrid(Point pos)
    {
        if (pos.X < RowNumW || pos.Y < HeaderH)
            return (-1, -1);
        int row = (int)((pos.Y - HeaderH) / RowH);
        int ch = (int)((pos.X - RowNumW) / CellW);
        return (row, ch);
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        int w = RowNumW + TotalChannels * CellW;
        int h = HeaderH + TotalRows * RowH;

        ctx.FillRectangle(new SolidColorBrush(HeaderBg), new Rect(0, 0, w, HeaderH));

        for (int c = 0; c < TotalChannels; c++)
        {
            double x = RowNumW + c * CellW + 4;
            var ft = new FormattedText(ChLabels[c], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, 10, null);
            ctx.DrawText(ft, new Point(x, 2));
        }

        for (int r = 0; r < TotalRows; r++)
        {
            double y = HeaderH + r * RowH;

            if (r % 8 == 0)
            {
                var rowNumFt = new FormattedText(r.ToString("X").PadLeft(2, '0'), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, 9, null);
                ctx.DrawText(rowNumFt, new Point(2, y + 1));
            }

            if (r % 2 == 1)
            {
                ctx.FillRectangle(new SolidColorBrush(AltRowColor), new Rect(RowNumW, y, TotalChannels * CellW, RowH));
            }

            for (int c = 0; c < TotalChannels; c++)
            {
                double x = RowNumW + c * CellW;
                var cellRect = new Rect(x, y, CellW, RowH);

                bool hasNote = _vm.HasNoteAt(r, c);
                bool isHover = r == _hoverRow && c == _hoverCh;

                Color cellColor = BgColor;
                if (isHover)
                    cellColor = HoverColor;
                else if (hasNote)
                    cellColor = NoteColor;

                ctx.FillRectangle(new SolidColorBrush(cellColor), cellRect);

                if (hasNote)
                {
                    string display = _vm.GetCellDisplay(r, c);
                    var ft = new FormattedText(display, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, 9, null);
                    ctx.DrawText(ft, new Point(x + 2, y + 1));
                }

                ctx.DrawLine(new Pen(new SolidColorBrush(GridLine), 0.3), new Point(x, y), new Point(x + CellW, y));
                ctx.DrawLine(new Pen(new SolidColorBrush(GridLine), 0.3), new Point(x, y), new Point(x, y + RowH));
            }
        }

        ctx.DrawLine(new Pen(new SolidColorBrush(GridLine), 0.5), new Point(0, h - 1), new Point(w, h - 1));
        ctx.DrawLine(new Pen(new SolidColorBrush(GridLine), 0.5), new Point(w - 1, 0), new Point(w - 1, h));
    }
}
