using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Ferbento.Core.Audio;
using Ferbento.Editor.Sfx;

namespace Ferbento.Editor.Sfx.Controls;

public sealed class SfxInspectorPanel : UserControl
{
  private SfxEditorController? _ctrl;
  private readonly SfxEnvelopePreview _envelope = new()
  {
    Height = 40,
    MinHeight = 40,
    Margin = new Thickness(0, 4),
  };
  private readonly WrapPanel _waveformGrid = new() { Orientation = Orientation.Horizontal };
  private readonly Slider _volumeSlider = new() { Minimum = 0, Maximum = 7, Width = 110 };
  private readonly TextBlock _volumeValue = new() { FontSize = 10, Foreground = Brushes.LightGray, MinWidth = 12 };
  private readonly ComboBox _effectCombo = new() { Width = 130, MaxDropDownHeight = 220 };
  private readonly SfxEffectParamPanel _effectParamPanel = new();
  private readonly NumericUpDown _previewChannel = new()
  {
    Minimum = 0,
    Maximum = 3,
    Width = 36,
    MinHeight = 22,
    Height = 22,
    FontSize = 11,
    ShowButtonSpinner = false,
    Padding = new Thickness(4, 0),
    HorizontalContentAlignment = HorizontalAlignment.Center,
    VerticalContentAlignment = VerticalAlignment.Center,
  };
  private readonly Dictionary<SfxFilters, CheckBox> _filterBoxes = new();
  private readonly Button _previewPlay = new() { Padding = new Thickness(6, 2) };
  private readonly Button _previewStop = new() { Padding = new Thickness(6, 2) };
  private bool _syncing;

  public SfxInspectorPanel()
  {
    Width = 180;
    Background = new SolidColorBrush(SfxTheme.Background);
    Content = BuildLayout();
    WireLocalEvents();

    _previewPlay.Content = SfxToolIcons.CreatePlayIcon();
    _previewStop.Content = SfxToolIcons.CreateStopIcon();
    ToolTip.SetTip(_previewChannel,
      "Audio channel used only for preview playback in the editor (not saved in the SFX).");
  }

  public void Bind(SfxEditorController controller)
  {
    _ctrl = controller;
    _envelope.Bind(controller);
    _previewChannel.Maximum = Math.Max(0, controller.ChannelCount - 1);
    SyncFromController();
    controller.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName is nameof(SfxEditorController.DefaultWaveform)
          or nameof(SfxEditorController.DefaultVolume)
          or nameof(SfxEditorController.DefaultEffect)
          or nameof(SfxEditorController.DefaultEffectParam)
          or nameof(SfxEditorController.Filters)
          or nameof(SfxEditorController.Speed)
          or nameof(SfxEditorController.SelectedChannel)
          or nameof(SfxEditorController.ChannelCount)
          or nameof(SfxEditorController.Document))
        SyncFromController();
    };
    controller.RefreshRequested += () =>
    {
      _envelope.InvalidateVisual();
      SyncFromController();
    };
  }

  private Control BuildLayout()
  {
    var root = new StackPanel { Spacing = 6, Margin = new Thickness(8) };

    root.Children.Add(SectionHeader("WAVEFORM"));
    BuildWaveformButtons();
    root.Children.Add(_waveformGrid);

    root.Children.Add(Label("Vol"));
    root.Children.Add(new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 4,
      Children = { _volumeSlider, _volumeValue },
    });

    root.Children.Add(Label("Effect"));
    _effectCombo.ItemsSource = SfxTheme.EffectNames;
    _effectCombo.MaxDropDownHeight = 220;
    root.Children.Add(_effectCombo);
    root.Children.Add(_effectParamPanel);

    root.Children.Add(SectionHeader("ENVELOPE"));
    root.Children.Add(new Border
    {
      Height = 44,
      Child = _envelope,
      Background = new SolidColorBrush(Color.Parse("#141414")),
    });

    root.Children.Add(SectionHeader("FILTERS"));
    var filtersPanel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 0) };
    foreach (var (filter, label) in new (SfxFilters, string)[]
             {
               (SfxFilters.Noiz, "Noiz"),
               (SfxFilters.Buzz, "Buzz"),
               (SfxFilters.Detune1, "Det×1"),
               (SfxFilters.Detune2, "Oct+1"),
               (SfxFilters.Reverb, "Reverb"),
               (SfxFilters.Dampen, "Dampen"),
             })
    {
      var cb = new CheckBox { Content = label, FontSize = 10 };
      _filterBoxes[filter] = cb;
      filtersPanel.Children.Add(cb);
    }
    root.Children.Add(filtersPanel);

    root.Children.Add(SectionHeader("PREVIEW"));
    root.Children.Add(new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 4,
      Children =
      {
        _previewPlay,
        _previewStop,
        Label("Ch"),
        _previewChannel,
      },
    });

    return root;
  }

  private void BuildWaveformButtons()
  {
    _waveformGrid.Children.Clear();
    for (int i = 0; i < 8; i++)
    {
      int idx = i;
      var btn = new ToggleButton
      {
        Width = 38,
        Height = 22,
        Padding = new Thickness(2),
        Content = new SfxWaveformIcon { Instrument = idx, Width = 30, Height = 14 },
      };
      ToolTip.SetTip(btn, $"{idx + 1}: {SfxTheme.WaveformNames[idx]}");
      btn.Click += (_, _) =>
      {
        if (_ctrl == null) return;
        _ctrl.DefaultWaveform = idx;
        HighlightWaveform(idx);
      };
      _waveformGrid.Children.Add(btn);
    }
  }

  private void WireLocalEvents()
  {
    _effectParamPanel.SetOnParamChanged(param =>
    {
      if (_syncing || _ctrl == null) return;
      _ctrl.DefaultEffectParam = param;
    });

    _volumeSlider.PropertyChanged += (_, e) =>
    {
      if (_syncing || _ctrl == null || e.Property != Slider.ValueProperty) return;
      _ctrl.DefaultVolume = (int)_volumeSlider.Value;
      _volumeValue.Text = ((int)_volumeSlider.Value).ToString();
    };

    _effectCombo.SelectionChanged += (_, _) =>
    {
      if (_syncing || _ctrl == null || _effectCombo.SelectedIndex < 0) return;
      _ctrl.DefaultEffect = (SfxEffect)_effectCombo.SelectedIndex;
      _effectParamPanel.SetEffect(_ctrl.DefaultEffect, _ctrl.DefaultEffectParam);
    };

    _previewChannel.ValueChanged += (_, e) =>
    {
      if (_syncing || _ctrl == null || !e.NewValue.HasValue) return;
      _ctrl.SelectedChannel = (int)e.NewValue.Value;
    };

    foreach (var (filter, cb) in _filterBoxes)
    {
      cb.IsCheckedChanged += (_, _) =>
      {
        if (_syncing || _ctrl == null) return;
        var flags = SfxFilters.None;
        foreach (var (f, box) in _filterBoxes)
        {
          if (box.IsChecked == true)
            flags |= f;
        }

        _ctrl.Filters = flags;
      };
    }

    _previewPlay.Click += (_, _) => _ctrl?.Play();
    _previewStop.Click += (_, _) => _ctrl?.Stop();
  }

  private void SyncFromController()
  {
    if (_ctrl == null) return;
    _syncing = true;
    try
    {
      _previewChannel.Maximum = Math.Max(0, _ctrl.ChannelCount - 1);
      _volumeSlider.Value = _ctrl.DefaultVolume;
      _volumeValue.Text = _ctrl.DefaultVolume.ToString();
      _effectCombo.SelectedIndex = (int)_ctrl.DefaultEffect;
      _effectParamPanel.SetEffect(_ctrl.DefaultEffect, _ctrl.DefaultEffectParam);
      _previewChannel.Value = _ctrl.SelectedChannel;
      HighlightWaveform(_ctrl.DefaultWaveform);
      foreach (var (filter, cb) in _filterBoxes)
        cb.IsChecked = _ctrl.Filters.HasFlag(filter);
      _envelope.InvalidateVisual();
    }
    finally
    {
      _syncing = false;
    }
  }

  private void HighlightWaveform(int active)
  {
    for (int i = 0; i < _waveformGrid.Children.Count; i++)
    {
      if (_waveformGrid.Children[i] is ToggleButton tb)
      {
        bool on = i == active;
        tb.IsChecked = on;
        tb.Background = new SolidColorBrush(on ? Color.Parse("#569CD6") : Color.Parse("#333337"));
        if (tb.Content is SfxWaveformIcon icon)
          icon.Instrument = i;
      }
    }
  }

  private static TextBlock SectionHeader(string text) => new()
  {
    Text = text,
    FontSize = 10,
    LetterSpacing = 2,
    Foreground = new SolidColorBrush(SfxTheme.MutedText),
    Margin = new Thickness(0, 4, 0, 0),
  };

  private static TextBlock Label(string text) => new()
  {
    Text = text,
    FontSize = 10,
    Foreground = Brushes.LightGray,
  };
}
