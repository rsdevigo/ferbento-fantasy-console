using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ferbento.Core.Chips;
using Ferbento.Editor.Chrome;
using Ferbento.Editor.Music;
using Ferbento.Editor.Music.Controls;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class MusicTrackerView : UserControl, IProfileAwareEditor
{
    private readonly MusicEditorController _ctrl = new();
    private readonly MusicPatternCanvas _patternCanvas = new();
    private readonly PatternStrip _patternStrip = new();
    private readonly PianoKeyboardView _piano = new();
    private readonly TextBlock _headerPath = new();
    private readonly TextBlock _headerClock = new();
    private readonly TextBox _songNameBox = new();
    private readonly TextBlock _statusBar = new();
    private readonly StepperControl _patternStepper = new() { Label = "PATTERN" };
    private readonly StepperControl _lengthStepper = new() { Label = "LENGTH" };
    private readonly StepperControl _tempoStepper = new() { Label = "TEMPO" };
    private readonly StepperControl _autoIncStepper = new() { Label = "AUTO INCREMENT" };
    private readonly StepperControl _instrumentStepper = new() { Label = "INSTRUMENT" };
    private readonly StepperControl _octaveStepper = new() { Label = "OCTAVE" };
    private readonly StepperControl _volumeStepper = new() { Label = "VOLUME" };
    private readonly ComboBox _noteCombo = new();
    private readonly ScrollViewer _gridScroll = new();
    private readonly DispatcherTimer _playbackTimer = new();

    private Action? _onEdited;
    private string _projectLabel = "UNTITLED/MUSIC.JSON";

    public MusicEditorController Controller => _ctrl;

    public void SetEditedHandler(Action handler) => _onEdited = handler;

    public MusicTrackerView()
    {
        Focusable = true;
        Background = new SolidColorBrush(MusicTheme.Background);
        Content = BuildLayout();

        _patternCanvas.Bind(_ctrl);
        WireEvents();

        _playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 30);
        _playbackTimer.Tick += (_, _) =>
        {
            _ctrl.TickPlayback();
            _patternCanvas.InvalidateVisual();
        };
        _playbackTimer.Start();

        Loaded += (_, _) => Focus();
    }

    public void SetProfile(HardwareProfile profile) => _ctrl.SetProfile(profile);

    public void SetChipBus(ChipBus bus) => _ctrl.SetChipBus(bus);

    public void RefreshFromBus() => _ctrl.RefreshFromBus();

    public void SetProjectLabel(string label)
    {
        _projectLabel = label;
        UpdateHeader();
    }

    public MusicProjectData ExportMusicData() => _ctrl.ExportProjectData();

    public void ImportMusicData(MusicProjectData? data) => _ctrl.ImportProjectData(data);

    private void WireEvents()
    {
        _ctrl.Edited += () => _onEdited?.Invoke();
        _ctrl.RefreshRequested += OnRefresh;
        _ctrl.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(_ctrl.CurrentPattern):
                    _patternStepper.Value = _ctrl.CurrentPattern;
                    break;
                case nameof(_ctrl.SequenceSlot):
                    _patternStrip.SelectedSlot = _ctrl.SequenceSlot;
                    break;
                case nameof(_ctrl.Length):
                    _lengthStepper.Value = _ctrl.Length;
                    _lengthStepper.Maximum = _ctrl.RowsPerPattern - 1;
                    _patternCanvas.InvalidateMeasure();
                    break;
                case nameof(_ctrl.Tempo):
                    _tempoStepper.Value = _ctrl.Tempo;
                    break;
                case nameof(_ctrl.AutoIncrement):
                    _autoIncStepper.Value = _ctrl.AutoIncrement;
                    break;
                case nameof(_ctrl.SelectedInstrument):
                    _instrumentStepper.Value = _ctrl.SelectedInstrument;
                    break;
                case nameof(_ctrl.SelectedOctave):
                    _octaveStepper.Value = _ctrl.SelectedOctave;
                    break;
                case nameof(_ctrl.SelectedVolume):
                    _volumeStepper.Value = _ctrl.SelectedVolume;
                    break;
                case nameof(_ctrl.SongName):
                    if (_songNameBox.Text != _ctrl.SongName)
                        _songNameBox.Text = _ctrl.SongName;
                    break;
                case nameof(_ctrl.StatusMessage):
                    _statusBar.Text = _ctrl.StatusMessage;
                    break;
                case nameof(_ctrl.PatternCount):
                case nameof(_ctrl.ChannelCount):
                    SyncLimits();
                    break;
                case nameof(_ctrl.ProfileLabel):
                    UpdateHeader();
                    break;
            }
        };

        _songNameBox.TextChanged += (_, _) => _ctrl.SongName = _songNameBox.Text ?? "";

        _patternStepper.ValueChanged += (_, v) => _ctrl.CurrentPattern = v;
        _lengthStepper.ValueChanged += (_, v) => _ctrl.Length = v;
        _tempoStepper.ValueChanged += (_, v) => _ctrl.Tempo = v;
        _autoIncStepper.ValueChanged += (_, v) => _ctrl.AutoIncrement = v;
        _instrumentStepper.ValueChanged += (_, v) => _ctrl.SelectedInstrument = v;
        _octaveStepper.ValueChanged += (_, v) =>
            _ctrl.SetNoteLetterOctave(_noteCombo.SelectedItem as string ?? "C", v);
        _volumeStepper.ValueChanged += (_, v) => _ctrl.SelectedVolume = v;

        _noteCombo.SelectionChanged += (_, _) =>
        {
            if (_noteCombo.SelectedItem is string letter)
                _ctrl.SetNoteLetterOctave(letter, _ctrl.SelectedOctave);
        };

        _patternStrip.SlotSelected += (_, slot) =>
        {
            _ctrl.SequenceSlot = slot;
            _ctrl.CurrentPattern = _ctrl.GetSequencePattern(slot);
        };
        _patternStrip.GetPattern = slot => _ctrl.GetSequencePattern(slot);

        _piano.NoteSelected += (_, note) => _ctrl.PlaceNoteAtSelection(note);

        EditorKeyboardRouting.Attach(this, OnKeyDown, OnKeyUp, OnTextInput);
    }

    private void SyncLimits()
    {
        _patternStepper.Maximum = Math.Max(0, _ctrl.PatternCount - 1);
        _patternStepper.Value = _ctrl.CurrentPattern;
        _instrumentStepper.Maximum = 7;
        _patternStrip.SlotCount = _ctrl.Document.Sequence.Length;
        _patternCanvas.Bind(_ctrl);
        _patternCanvas.InvalidateMeasure();
        _patternCanvas.InvalidateVisual();
        _patternStrip.InvalidateVisual();
    }

    private void OnRefresh()
    {
        _patternCanvas.InvalidateVisual();
        _patternStrip.InvalidateVisual();
        UpdateHeader();
    }

    private void UpdateHeader()
    {
        _headerPath.Text = $"{_projectLabel}*  [{_ctrl.ProfileLabel}]";
        _headerClock.Text = DateTime.Now.ToString("ddd hh:mmtt yyyy").ToUpperInvariant();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_ctrl.HandleKey(e.Key.ToString(), e.KeyModifiers.HasFlag(KeyModifiers.Control),
                e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
            e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e) { }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && _ctrl.HandleTextInput(e.Text))
            e.Handled = true;
    }

    private Control BuildLayout()
    {
        _noteCombo.ItemsSource = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        _noteCombo.SelectedIndex = 0;
        _noteCombo.Width = 60;
        _noteCombo.Focusable = false;

        _songNameBox.Text = "Untitled";
        _songNameBox.Background = new SolidColorBrush(MusicTheme.GridBg);
        _songNameBox.Foreground = new SolidColorBrush(MusicTheme.Accent);
        _songNameBox.BorderBrush = new SolidColorBrush(MusicTheme.PanelBorder);
        _songNameBox.MinWidth = 120;

        _headerPath.Foreground = new SolidColorBrush(MusicTheme.Text);
        _headerPath.FontSize = 11;
        _headerClock.Foreground = new SolidColorBrush(MusicTheme.TextDim);
        _headerClock.FontSize = 10;

        _statusBar.Foreground = new SolidColorBrush(MusicTheme.TextDim);
        _statusBar.FontSize = 10;
        _statusBar.Text = _ctrl.StatusMessage;

        _gridScroll.Content = _patternCanvas;
        _gridScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _gridScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

        _patternStrip.GetPattern = slot => _ctrl.GetSequencePattern(slot);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,*"),
            Margin = new Thickness(8),
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                _headerPath,
                _headerClock,
            },
        };
        Grid.SetColumn(_headerClock, 1);

        var transport = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 6),
            Children =
            {
                MakeTransportButton("\u270E", "Generate Pattern", () => _ctrl.RandomGeneratePattern()),
                MakeTransportButton("\u23ED", "Play Song", () => _ctrl.PlaySong()),
                MakeTransportButton("\u25B6", "Play Pattern", () => _ctrl.PlayPattern()),
                MakeTransportButton("\u23F9", "Stop", () => _ctrl.Stop()),
                new TextBlock { Text = "SEQ", Foreground = new SolidColorBrush(MusicTheme.TextDim), VerticalAlignment = VerticalAlignment.Center },
                MakeArrowButton("<", () => _ctrl.SequenceSlot = Math.Max(0, _ctrl.SequenceSlot - 1)),
                MakeSequenceSlotLabel(),
                MakeArrowButton(">", () => _ctrl.SequenceSlot = Math.Min(_ctrl.Document.Sequence.Length - 1, _ctrl.SequenceSlot + 1)),
                new TextBlock { Text = "NAME", Foreground = new SolidColorBrush(MusicTheme.TextDim), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) },
                _songNameBox,
            },
        };

        var patternRow = new Border
        {
            Background = new SolidColorBrush(MusicTheme.Panel),
            BorderBrush = new SolidColorBrush(MusicTheme.PanelBorder),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4),
            Child = _patternStrip,
        };

        var settingsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6),
            Children = { _patternStepper, _lengthStepper, _tempoStepper, _autoIncStepper },
        };

        var instrumentRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6),
            Children =
            {
                _instrumentStepper,
                MakeNotePanel(),
                _octaveStepper,
                _volumeStepper,
            },
        };

        var pianoRow = new Border
        {
            Background = new SolidColorBrush(MusicTheme.Panel),
            BorderBrush = new SolidColorBrush(MusicTheme.PanelBorder),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4),
            Child = _piano,
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(transport, 1);
        Grid.SetRow(patternRow, 2);
        Grid.SetRow(settingsRow, 3);
        Grid.SetRow(_gridScroll, 4);
        Grid.SetRow(instrumentRow, 5);
        Grid.SetRow(pianoRow, 6);
        Grid.SetRow(_statusBar, 7);

        root.Children.Add(header);
        root.Children.Add(transport);
        root.Children.Add(patternRow);
        root.Children.Add(settingsRow);
        root.Children.Add(_gridScroll);
        root.Children.Add(instrumentRow);
        root.Children.Add(pianoRow);
        root.Children.Add(_statusBar);

        var chrome = root;
        EditorKeyboardRouting.PreventFocusSteal(chrome);
        chrome.PointerPressed += (_, _) => Focus();

        SyncInitialSteppers();
        return chrome;
    }

    private Control MakeSequenceSlotLabel()
    {
        var tb = new TextBlock
        {
            Foreground = new SolidColorBrush(MusicTheme.Accent),
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 28,
            TextAlignment = TextAlignment.Center,
            Text = _ctrl.SequenceSlot.ToString("D2"),
        };
        _ctrl.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_ctrl.SequenceSlot))
                tb.Text = _ctrl.SequenceSlot.ToString("D2");
        };
        return tb;
    }

    private Control MakeNotePanel()
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = "NOTE",
            Foreground = new SolidColorBrush(MusicTheme.TextDim),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(_noteCombo);
        return panel;
    }

    private static Button MakeTransportButton(string icon, string tip, Action onClick)
    {
        var btn = new Button
        {
            Content = icon,
            Padding = new Thickness(8, 4),
            Background = new SolidColorBrush(MusicTheme.ButtonFace),
            Foreground = new SolidColorBrush(MusicTheme.Accent),
            BorderBrush = new SolidColorBrush(MusicTheme.PanelBorder),
            BorderThickness = new Thickness(2),
            Focusable = false,
        };
        ToolTip.SetTip(btn, tip);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakeArrowButton(string glyph, Action onClick)
    {
        var btn = new Button
        {
            Content = glyph,
            Padding = new Thickness(6, 2),
            Background = new SolidColorBrush(MusicTheme.ButtonFace),
            Foreground = new SolidColorBrush(MusicTheme.Accent),
            Focusable = false,
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void SyncInitialSteppers()
    {
        _patternStepper.Minimum = 0;
        _patternStepper.Maximum = 63;
        _lengthStepper.Minimum = 1;
        _lengthStepper.Maximum = 63;
        _lengthStepper.Value = 32;
        _tempoStepper.Minimum = 1;
        _tempoStepper.Maximum = 255;
        _tempoStepper.Value = 8;
        _autoIncStepper.Minimum = 0;
        _autoIncStepper.Maximum = 16;
        _autoIncStepper.Value = 1;
        _instrumentStepper.Minimum = 0;
        _instrumentStepper.Maximum = 7;
        _octaveStepper.Minimum = 0;
        _octaveStepper.Maximum = 8;
        _octaveStepper.Value = 4;
        _volumeStepper.Minimum = 0;
        _volumeStepper.Maximum = 7;
        _volumeStepper.Value = 7;
    }
}
