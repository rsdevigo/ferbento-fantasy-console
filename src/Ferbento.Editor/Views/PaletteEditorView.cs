using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ferbento.Core.Chips;
using Ferbento.Editor.ViewModels;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class PaletteEditorView : UserControl, IProfileAwareEditor
{
    private readonly PaletteEditorViewModel _vm = new();
    private readonly StackPanel _swatchRow = new() { Margin = new Thickness(12, 8) };
    private readonly StackPanel _drawRemapPanel = new();
    private readonly StackPanel _screenRemapPanel = new();
    private readonly StackPanel _transparencyPanel = new();
    private TextBlock _colorNameBlock = null!;
    private TextBlock _colorHexBlock = null!;
    private TextBlock _colorRgbBlock = null!;
    private TextBlock _colorIndexBlock = null!;
    private TextBox _hexInput = null!;
    private NumericUpDown _rInput = null!;
    private NumericUpDown _gInput = null!;
    private NumericUpDown _bInput = null!;

    public PaletteEditorView()
    {
        DataContext = _vm;
        Content = BuildLayout();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PaletteEditorViewModel.SelectedIndex)
                or nameof(PaletteEditorViewModel.ColorCount))
            {
                if (e.PropertyName == nameof(PaletteEditorViewModel.ColorCount))
                    RebuildDynamicControls();
                UpdateDetailPanel();
                UpdateSwatchHighlight();
            }
        };
        _vm.PaletteChanged += () =>
        {
            RefreshSwatchColors();
            UpdateDetailPanel();
        };
    }

    public void SetProfile(HardwareProfile profile) => _vm.SetProfile(profile);

    public void SetEditedHandler(Action handler) => _vm.Edited += handler;

    public event Action? PaletteChanged
    {
        add => _vm.PaletteChanged += value;
        remove => _vm.PaletteChanged -= value;
    }

    public void SetChipBus(ChipBus bus)
    {
        _vm.SetChipBus(bus);
        RebuildDynamicControls();
    }

    public void RefreshFromBus()
    {
        _vm.SyncFromDrawState();
        RefreshAll();
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        root.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Palette Editor — RGB colors, draw/screen palette, transparency",
                Foreground = Brushes.LightGray,
                FontSize = 12,
            },
        });

        _swatchRow.Orientation = Orientation.Horizontal;
        Grid.SetRow(_swatchRow, 1);
        root.Children.Add(_swatchRow);

        var mappings = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            Margin = new Thickness(12, 0, 12, 8),
        };

        mappings.Children.Add(_drawRemapPanel);
        Grid.SetColumn(_drawRemapPanel, 0);
        mappings.Children.Add(_screenRemapPanel);
        Grid.SetColumn(_screenRemapPanel, 1);
        mappings.Children.Add(_transparencyPanel);
        Grid.SetColumn(_transparencyPanel, 2);

        Grid.SetRow(mappings, 2);
        root.Children.Add(mappings);

        var footer = new StackPanel
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Margin = new Thickness(12),
            Spacing = 6,
        };
        _colorIndexBlock = new TextBlock
        {
            Text = "Index: 0",
            Foreground = new SolidColorBrush(Color.Parse("#569CD6")),
            FontSize = 12,
            FontWeight = FontWeight.Bold,
        };
        _colorNameBlock = new TextBlock { Text = "black", Foreground = Brushes.LightGray, FontSize = 11 };
        _colorHexBlock = new TextBlock { Text = "#000000", Foreground = Brushes.LightGray, FontSize = 11 };
        _colorRgbBlock = new TextBlock { Text = "RGB(0, 0, 0)", Foreground = Brushes.LightGray, FontSize = 11 };

        var rgbRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _hexInput = new TextBox { Width = 90, PlaceholderText = "#RRGGBB", FontSize = 11 };
        _hexInput.LostFocus += (_, _) => _vm.SetColorHex(_vm.SelectedIndex, _hexInput.Text ?? "");
        _rInput = MakeColorSpinner();
        _gInput = MakeColorSpinner();
        _bInput = MakeColorSpinner();
        void ApplyRgb()
        {
            _vm.SetColorRgb(_vm.SelectedIndex,
                (byte)(int)(_rInput.Value ?? 0),
                (byte)(int)(_gInput.Value ?? 0),
                (byte)(int)(_bInput.Value ?? 0));
            RefreshSwatchColors();
        }
        _rInput.ValueChanged += (_, _) => ApplyRgb();
        _gInput.ValueChanged += (_, _) => ApplyRgb();
        _bInput.ValueChanged += (_, _) => ApplyRgb();
        rgbRow.Children.Add(new TextBlock { Text = "RGB:", Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center });
        rgbRow.Children.Add(_hexInput);
        rgbRow.Children.Add(_rInput);
        rgbRow.Children.Add(_gInput);
        rgbRow.Children.Add(_bInput);

        footer.Children.Add(_colorIndexBlock);
        footer.Children.Add(_colorNameBlock);
        footer.Children.Add(_colorHexBlock);
        footer.Children.Add(_colorRgbBlock);
        footer.Children.Add(rgbRow);
        footer.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                MakeButton("Reset Draw", () => { _vm.ResetDrawPalette(); RefreshRemapControls(); }),
                MakeButton("Reset Screen", () => { _vm.ResetScreenPalette(); RefreshRemapControls(); }),
                MakeButton("Reset palt", () => { _vm.ResetTransparency(); RefreshRemapControls(); }),
                MakeButton("Profile Defaults", () => { _vm.ResetToProfileDefaults(); RefreshAll(); }),
                MakeButton("Pico-8 Canonical", () => { _vm.ResetToPico8Canonical(); RefreshAll(); }),
            },
        });
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        return root;
    }

    private static NumericUpDown MakeColorSpinner() => new()
    {
        Width = 56,
        Minimum = 0,
        Maximum = 255,
        FontSize = 11,
    };

    private void RebuildDynamicControls()
    {
        _swatchRow.Children.Clear();
        _drawRemapPanel.Children.Clear();
        _screenRemapPanel.Children.Clear();
        _transparencyPanel.Children.Clear();

        var count = _vm.ColorCount;
        var remapCount = Math.Min(count, 16);

        _drawRemapPanel.Children.Add(MakeSectionLabel("Draw Palette (pal c0,c1,0)"));
        _screenRemapPanel.Children.Add(MakeSectionLabel("Screen Palette (pal c0,c1,1)"));
        _transparencyPanel.Children.Add(MakeSectionLabel("Transparency (palt)"));

        for (var i = 0; i < count; i++)
        {
            var idx = i;
            var swatch = new Border
            {
                Width = Math.Min(70, 280 / Math.Max(1, count)),
                Height = 50,
                Background = new SolidColorBrush(Pico8Colors.ToAvaloniaColor(i, _vm.ColorChip)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(2),
                Tag = idx,
                Child = new TextBlock
                {
                    Text = $"{i}",
                    FontSize = 10,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0),
                },
            };
            swatch.PointerPressed += (_, _) =>
            {
                _vm.SelectedIndex = idx;
                UpdateSwatchHighlight();
            };
            _swatchRow.Children.Add(swatch);

            if (idx < remapCount)
            {
                _drawRemapPanel.Children.Add(BuildRemapRow(idx, true));
                _screenRemapPanel.Children.Add(BuildRemapRow(idx, false));

                var cb = new CheckBox
                {
                    Content = $"{i}",
                    Foreground = Brushes.LightGray,
                    FontSize = 10,
                    Tag = idx,
                };
                cb.IsCheckedChanged += (_, _) =>
                {
                    if (cb.IsChecked.HasValue)
                        _vm.SetTransparency(idx, cb.IsChecked.Value);
                };
                _transparencyPanel.Children.Add(cb);
            }
        }

        RefreshAll();
    }

    private Control BuildRemapRow(int idx, bool isDraw)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                new Border
                {
                    Width = 14, Height = 14,
                    Background = new SolidColorBrush(Pico8Colors.ToAvaloniaColor(idx)),
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock { Text = "→", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center },
            },
        };

        var combo = new ComboBox
        {
            Width = 80,
            ItemsSource = Enumerable.Range(0, Math.Min(_vm.ColorCount, 16)).Select(n => n.ToString()).ToList(),
            SelectedIndex = Math.Min(idx, Math.Min(_vm.ColorCount, 16) - 1),
            FontSize = 10,
            Tag = (idx, isDraw),
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0) return;
            if (isDraw) _vm.SetDrawRemap(idx, combo.SelectedIndex);
            else _vm.SetScreenRemap(idx, combo.SelectedIndex);
        };
        row.Children.Add(combo);
        return row;
    }

    private static TextBlock MakeSectionLabel(string text) => new()
    {
        Text = text,
        Foreground = Brushes.LightGray,
        FontSize = 11,
        Margin = new Thickness(0, 0, 0, 4),
    };

    private static Button MakeButton(string text, Action action)
    {
        var btn = new Button { Content = text, Padding = new Thickness(8, 3) };
        btn.Click += (_, _) => action();
        return btn;
    }

    private void RefreshRemapControls()
    {
        var drawIdx = 0;
        var screenIdx = 0;
        var transIdx = 0;
        foreach (var child in _drawRemapPanel.Children.Skip(1))
            if (child is StackPanel sp && sp.Children[^1] is ComboBox dc && drawIdx < _vm.DrawRemap.Count)
                dc.SelectedIndex = _vm.DrawRemap[drawIdx++];
        foreach (var child in _screenRemapPanel.Children.Skip(1))
            if (child is StackPanel sp && sp.Children[^1] is ComboBox sc && screenIdx < _vm.ScreenRemap.Count)
                sc.SelectedIndex = _vm.ScreenRemap[screenIdx++];
        foreach (var child in _transparencyPanel.Children.Skip(1))
            if (child is CheckBox cb && transIdx < _vm.Transparency.Count)
                cb.IsChecked = _vm.Transparency[transIdx++];
    }

    private void RefreshSwatchColors()
    {
        foreach (var child in _swatchRow.Children)
        {
            if (child is Border swatch && swatch.Tag is int idx)
                swatch.Background = new SolidColorBrush(Pico8Colors.ToAvaloniaColor(idx, _vm.ColorChip));
        }
    }

    private void RefreshAll()
    {
        RefreshSwatchColors();
        RefreshRemapControls();
        UpdateDetailPanel();
        UpdateSwatchHighlight();
    }

    private void UpdateSwatchHighlight()
    {
        foreach (var child in _swatchRow.Children)
        {
            if (child is Border swatch && swatch.Tag is int idx)
            {
                swatch.BorderBrush = idx == _vm.SelectedIndex ? Brushes.White : Brushes.Transparent;
                swatch.BorderThickness = new Thickness(idx == _vm.SelectedIndex ? 2 : 0);
            }
        }
        UpdateDetailPanel();
    }

    private void UpdateDetailPanel()
    {
        _colorIndexBlock.Text = $"Index: {_vm.SelectedIndex}";
        _colorNameBlock.Text = _vm.ColorName;
        _colorHexBlock.Text = _vm.ColorHex;
        _colorRgbBlock.Text = _vm.ColorRgb;

        var hex = _vm.ColorHex.TrimStart('#');
        _hexInput.Text = $"#{hex}";

        if (_vm.SelectedIndex >= 0)
        {
            var rgb = _vm.SwatchColor(_vm.SelectedIndex);
            _rInput.Value = (rgb >> 16) & 0xFF;
            _gInput.Value = (rgb >> 8) & 0xFF;
            _bInput.Value = rgb & 0xFF;
        }
    }
}
