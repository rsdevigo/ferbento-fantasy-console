using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ferbento.Core.Chips;
using Ferbento.Editor.Chrome;
using Ferbento.Editor.Sfx;
using Ferbento.Editor.Sfx.Controls;
using Ferbento.Editor.ViewModels;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class SfxEditorView : UserControl, IProfileAwareEditor
{
  private readonly SfxEditorViewModel _vm = new();
  private SfxEditorController Ctrl => _vm.Controller;

  private readonly SfxPianoRoll _pianoRoll = new();
  private readonly SfxPropertyStrip _propertyStrip = new();
  private readonly SfxInspectorPanel _inspector = new();
  private readonly EditorToolStrip _toolStrip = new();
  private readonly EditorStatusBar _statusBar = new();
  private readonly ScrollViewer _rollScroll = new();
  private readonly ScrollViewer _stripScroll = new();
  private readonly DispatcherTimer _playbackTimer = new();

  private readonly TextBox _slotField = CreateCompactField(30, 3);
  private readonly TextBlock _slotHex = new()
  {
    FontSize = 9,
    Foreground = new SolidColorBrush(SfxTheme.MutedText),
    VerticalAlignment = VerticalAlignment.Center,
  };
  private readonly TextBox _nameBox = CreateCompactField(72, 8, monospace: false);
  private readonly TextBox _speedField = CreateCompactField(30, 3);
  private readonly TextBox _loopStartField = CreateCompactField(26, 2);
  private readonly TextBox _loopEndField = CreateCompactField(26, 2);
  private readonly TextBlock _loopDash = new()
  {
    Text = "-",
    Foreground = Brushes.LightGray,
    VerticalAlignment = VerticalAlignment.Center,
    FontSize = 11,
  };
  private readonly ToggleButton _loopToggle = new() { Content = "Loop", Padding = new Thickness(6, 2), FontSize = 11 };

  private Action? _onEdited;
  private bool _syncing;

  public SfxEditorController Controller => Ctrl;

  public void SetEditedHandler(Action handler) => _onEdited = handler;

  public SfxEditorView()
  {
    Focusable = true;
    HorizontalAlignment = HorizontalAlignment.Stretch;
    VerticalAlignment = VerticalAlignment.Stretch;
    Background = new SolidColorBrush(SfxTheme.Background);

    Content = BuildLayout();
    WireEvents();

    _playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 30);
    _playbackTimer.Tick += (_, _) =>
    {
      Ctrl.TickPlayback();
      _pianoRoll.InvalidateVisual();
    };
    _playbackTimer.Start();

    EditorKeyboardRouting.Attach(this, OnKeyDown, OnKeyUp, OnTextInput);
    Loaded += (_, _) => Focus();
  }

  public void SetProfile(HardwareProfile profile)
  {
    Ctrl.SetProfile(profile);
    ApplyProfileLimits();
    RefreshVisuals();
  }

  public void SetChipBus(ChipBus bus)
  {
    Ctrl.SetChipBus(bus);
    ApplyProfileLimits();
    RefreshVisuals();
  }

  private void ApplyProfileLimits()
  {
    _pianoRoll.Bind(Ctrl);
    _propertyStrip.Bind(Ctrl);
    _inspector.Bind(Ctrl);
    SyncControls();
  }

  public void RefreshFromBus()
  {
    Ctrl.RefreshFromBus();
    SyncControls();
    RefreshVisuals();
  }

  private void WireEvents()
  {
    Ctrl.Edited += () => _onEdited?.Invoke();
    Ctrl.RefreshRequested += RefreshVisuals;
    Ctrl.PropertyChanged += (_, e) =>
    {
      switch (e.PropertyName)
      {
        case nameof(SfxEditorController.SelectedSlot):
          SyncControls();
          break;
        case nameof(SfxEditorController.Speed):
        case nameof(SfxEditorController.LoopStart):
        case nameof(SfxEditorController.LoopEnd):
        case nameof(SfxEditorController.SlotName):
        case nameof(SfxEditorController.LoopPlayback):
          SyncControls();
          break;
        case nameof(SfxEditorController.Tool):
          _toolStrip.HighlightTool(Ctrl.Tool.ToString().ToLowerInvariant());
          break;
        case nameof(SfxEditorController.StatusMessage):
          _statusBar.Text = Ctrl.StatusMessage;
          break;
        case nameof(SfxEditorController.ZoomLevel):
        case nameof(SfxEditorController.CellWidth):
          _pianoRoll.InvalidateMeasure();
          _propertyStrip.InvalidateMeasure();
          RefreshVisuals();
          break;
        case nameof(SfxEditorController.SfxSlotCount):
        case nameof(SfxEditorController.NotesPerSfx):
        case nameof(SfxEditorController.ChannelCount):
          ApplyProfileLimits();
          RefreshVisuals();
          break;
      }
    };

    _slotField.LostFocus += (_, _) => CommitSlotField();
    _slotField.KeyDown += (_, e) =>
    {
      if (e.Key == Key.Enter)
      {
        CommitSlotField();
        e.Handled = true;
      }
    };

    WireCompactField(_speedField, CommitSpeedField);
    WireCompactField(_loopStartField, CommitLoopStartField);
    WireCompactField(_loopEndField, CommitLoopEndField);

    _nameBox.TextChanged += (_, _) =>
    {
      if (_syncing) return;
      Ctrl.SlotName = _nameBox.Text ?? "";
    };

    _loopToggle.IsCheckedChanged += (_, _) =>
    {
      if (_syncing) return;
      Ctrl.LoopPlayback = _loopToggle.IsChecked == true;
    };

    _pianoRoll.PianoKeyClicked += midi => Ctrl.PreviewPitch(midi);
    _pianoRoll.StepHeaderClicked += step => Ctrl.SelectStep(step);
  }

  private void CommitSlotField()
  {
    if (_syncing) return;
    if (int.TryParse(_slotField.Text, out int slot))
      Ctrl.SelectedSlot = slot;
    else
      SyncSlotField();
  }

  private void WireCompactField(TextBox field, Action commit)
  {
    field.LostFocus += (_, _) => commit();
    field.KeyDown += (_, e) =>
    {
      if (e.Key == Key.Enter)
      {
        commit();
        e.Handled = true;
      }
    };
  }

  private void CommitSpeedField()
  {
    if (_syncing) return;
    if (TryParseSpeed(_speedField.Text, out int speed))
      Ctrl.Speed = speed;
    else
      SyncSpeedField();
  }

  private void CommitLoopStartField()
  {
    if (_syncing) return;
    if (TryParseStepHex(_loopStartField.Text, Ctrl.NotesPerSfx, out int step))
      Ctrl.LoopStart = step;
    else
      SyncLoopFields();
  }

  private void CommitLoopEndField()
  {
    if (_syncing) return;
    if (TryParseStepHex(_loopEndField.Text, Ctrl.NotesPerSfx, out int step))
      Ctrl.LoopEnd = step;
    else
      SyncLoopFields();
  }

  private static bool TryParseStepHex(string? text, int notesPerSfx, out int step)
  {
    step = 0;
    if (string.IsNullOrWhiteSpace(text)) return false;
    text = text.Trim();
    if (int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out step))
      return step >= 0 && step < notesPerSfx;
    return int.TryParse(text, out step) && step >= 0 && step < notesPerSfx;
  }

  private static bool TryParseSpeed(string? text, out int speed)
  {
    speed = 0;
    if (string.IsNullOrWhiteSpace(text)) return false;
    text = text.Trim();
    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
      return int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out speed)
             && speed is >= 1 and <= 255;
    return int.TryParse(text, out speed) && speed is >= 1 and <= 255;
  }

  private void SyncSlotField()
  {
    _slotField.Text = Ctrl.SelectedSlot.ToString("D2");
    _slotHex.Text = $"/ 0x{Ctrl.SelectedSlot:X2}";
  }

  private void SyncSpeedField() => _speedField.Text = Ctrl.Speed.ToString();

  private void SyncLoopFields()
  {
    _loopStartField.Text = Ctrl.LoopStart.ToString("X2");
    _loopEndField.Text = Ctrl.LoopEnd.ToString("X2");
  }

  private void SyncControls()
  {
    _syncing = true;
    try
    {
      SyncSlotField();
      _nameBox.Text = Ctrl.SlotName;
      SyncSpeedField();
      SyncLoopFields();
      _loopToggle.IsChecked = Ctrl.LoopPlayback;
      _statusBar.Text = Ctrl.StatusMessage;
    }
    finally
    {
      _syncing = false;
    }
  }

  private void RefreshVisuals()
  {
    _pianoRoll.InvalidateVisual();
    _propertyStrip.InvalidateVisual();
    _statusBar.Text = Ctrl.StatusMessage;
  }

  private Control BuildLayout()
  {
    _rollScroll.Content = _pianoRoll;
    _rollScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
    _rollScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

    _stripScroll.Content = _propertyStrip;
    _stripScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
    _stripScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    _stripScroll.Height = 62;

    _rollScroll.ScrollChanged += (_, _) =>
    {
      if (Math.Abs(_stripScroll.Offset.X - _rollScroll.Offset.X) > 0.5)
        _stripScroll.Offset = _stripScroll.Offset.WithX(_rollScroll.Offset.X);
    };
    _stripScroll.ScrollChanged += (_, _) =>
    {
      if (Math.Abs(_rollScroll.Offset.X - _stripScroll.Offset.X) > 0.5)
        _rollScroll.Offset = _rollScroll.Offset.WithX(_stripScroll.Offset.X);
    };

    _statusBar.Text = Ctrl.StatusMessage;

    var toolbar = new ScrollViewer
    {
      HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
      VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
      Content = BuildToolbarPanel(),
    };
    BuildToolStrip();

    var editorPanel = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto"),
    };

    Grid.SetRow(toolbar, 0);
    Grid.SetRow(_rollScroll, 1);
    Grid.SetRow(_toolStrip, 2);
    Grid.SetRow(_stripScroll, 3);
    Grid.SetRow(_statusBar, 4);

    editorPanel.Children.Add(toolbar);
    editorPanel.Children.Add(_rollScroll);
    editorPanel.Children.Add(_toolStrip);
    editorPanel.Children.Add(_stripScroll);
    editorPanel.Children.Add(_statusBar);

    var outer = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("180,*"),
    };

    Grid.SetColumn(_inspector, 0);
    Grid.SetColumn(editorPanel, 1);
    outer.Children.Add(_inspector);
    outer.Children.Add(editorPanel);

    EditorKeyboardRouting.PreventFocusSteal(outer);
    outer.PointerPressed += (_, e) =>
    {
      if (EditorKeyboardRouting.IsEditableChromeTarget(e.Source))
        return;
      Focus();
    };

    return outer;
  }

  private Control BuildToolbarPanel()
  {
    var slotPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 4,
      VerticalAlignment = VerticalAlignment.Center,
      Children =
      {
        MakeBtn("\u25C0", "Previous slot (Q)", () => Ctrl.SelectedSlot--),
        _slotField,
        _slotHex,
        MakeBtn("\u25B6", "Next slot (W)", () => Ctrl.SelectedSlot++),
      },
    };

    return new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 6,
      Margin = new Thickness(0, 0, 0, 4),
      Children =
      {
        slotPanel,
        Label("Name:"),
        _nameBox,
        Sep(),
        MakeIconBtn(SfxToolIcons.CreatePlayIcon(), "Play (Space)", () => Ctrl.TogglePlayStop()),
        MakeIconBtn(SfxToolIcons.CreateStopIcon(), "Stop (Esc)", () => Ctrl.Stop()),
        _loopToggle,
        Sep(),
        DelayLabel(),
        _speedField,
        Sep(),
        Label("Loop:"),
        _loopStartField,
        _loopDash,
        _loopEndField,
        Sep(),
        MakeBtn("Clear", "Clear slot", () => Ctrl.ClearSlot()),
        MakeBtn("Undo", "Undo", () => Ctrl.Undo.Undo()),
        MakeBtn("Redo", "Redo", () => Ctrl.Undo.Redo()),
        Sep(),
        MakeBtn("-", "Zoom out", () => Ctrl.ZoomLevel--),
        MakeBtn("+", "Zoom in", () => Ctrl.ZoomLevel++),
      },
    };
  }

  private void BuildToolStrip()
  {
    _toolStrip.SetTools(
      new (string, string?, Func<Control>?)[]
      {
        ("pencil", "P", SfxToolIcons.CreatePencilIcon),
        ("eraser", "E", SfxToolIcons.CreateEraserIcon),
        ("select", "S", SfxToolIcons.CreateSelectIcon),
        ("fill", "F", SfxToolIcons.CreateFillIcon),
      },
      Ctrl.Tool.ToString().ToLowerInvariant());

    _toolStrip.ToolSelected += id =>
    {
      Ctrl.Tool = id switch
      {
        "eraser" => SfxEditorTool.Eraser,
        "select" => SfxEditorTool.Select,
        "fill" => SfxEditorTool.Fill,
        _ => SfxEditorTool.Pencil,
      };
    };
  }

  private void OnKeyDown(object? sender, KeyEventArgs e)
  {
    if (EditorKeyboardRouting.ShouldDeferToFocusedTextInput(this))
      return;
    if (Ctrl.HandleKey(e.Key.ToString(), e.KeyModifiers.HasFlag(KeyModifiers.Control),
        e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
      e.Handled = true;
  }

  private void OnKeyUp(object? sender, KeyEventArgs e) { }

  private void OnTextInput(object? sender, TextInputEventArgs e)
  {
    if (EditorKeyboardRouting.ShouldDeferToFocusedTextInput(this))
      return;
    if (!string.IsNullOrEmpty(e.Text) && Ctrl.HandleTextInput(e.Text))
      e.Handled = true;
  }

  private static TextBox CreateCompactField(int width, int maxLength, bool monospace = true) => new()
  {
    Width = width,
    MinWidth = width,
    MaxLength = maxLength,
    MinHeight = 22,
    Height = 22,
    FontFamily = monospace
      ? new FontFamily("Consolas,Courier New,monospace")
      : FontFamily.Default,
    FontSize = 11,
    Padding = new Thickness(4, 0),
    TextAlignment = TextAlignment.Center,
    VerticalContentAlignment = VerticalAlignment.Center,
    HorizontalContentAlignment = HorizontalAlignment.Center,
  };

  private static Button MakeIconBtn(Control icon, string tip, Action onClick)
  {
    var btn = new Button
    {
      Content = icon,
      Padding = new Thickness(6, 2),
      Focusable = false,
    };
    ToolTip.SetTip(btn, tip);
    btn.Click += (_, _) => onClick();
    return btn;
  }

  private static Button MakeBtn(string content, string tip, Action onClick)
  {
    var btn = new Button
    {
      Content = content,
      Padding = new Thickness(8, 2),
      Focusable = false,
    };
    ToolTip.SetTip(btn, tip);
    btn.Click += (_, _) => onClick();
    return btn;
  }

  private static Border Sep() => new()
  {
    Width = 1,
    Height = 20,
    Background = new SolidColorBrush(Color.Parse("#3A3A3A")),
    Margin = new Thickness(2, 0),
  };

  private static TextBlock DelayLabel()
  {
    var label = Label("Delay:");
    ToolTip.SetTip(label,
      "Pico-8 spd — frames per step (1–255). Higher = slower. Default 4.");
    return label;
  }

  private static TextBlock Label(string text) => new()
  {
    Text = text,
    Foreground = Brushes.LightGray,
    FontSize = 12,
    VerticalAlignment = VerticalAlignment.Center,
  };
}
