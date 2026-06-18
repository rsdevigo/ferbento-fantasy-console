using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Ferbento.Core.Chips;
using Ferbento.Editor.ViewModels;
using Ferbento.Profiles.Models;

namespace Ferbento.Editor.Views;

public class PaletteEditorView : UserControl, IProfileAwareEditor
{
    private readonly PaletteEditorViewModel _vm = new();
    private readonly StackPanel _swatchListPanel = new() { Spacing = 2 };
    private readonly TextBlock _headerBlock = new();
    private readonly TextBlock _disabledLabel = new();
    private readonly Border _currentPreview = new();
    private readonly Border _newPreview = new();
    private readonly ColorView _colorView = new() { IsAlphaEnabled = false };
    private readonly TextBox _hexInput = new();
    private readonly TextBox _nameInput = new();
    private readonly CheckBox _transpCheck = new();
    private readonly Button _confirmBtn = new() { Content = "Confirm", Padding = new Thickness(12, 4) };
    private bool _syncingPicker;

    public PaletteEditorView()
    {
        DataContext = _vm;
        Content = BuildLayout();
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.PaletteChanged += OnPaletteChanged;
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
        RebuildSwatchList();
        RefreshAll();
    }

    public void RefreshFromBus()
    {
        _vm.SyncFromDrawState();
        RefreshAll();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PaletteEditorViewModel.ColorCount):
                RebuildSwatchList();
                RefreshAll();
                break;
            case nameof(PaletteEditorViewModel.SelectedIndex):
            case nameof(PaletteEditorViewModel.CommittedHex):
            case nameof(PaletteEditorViewModel.PendingColor):
            case nameof(PaletteEditorViewModel.PendingHex):
            case nameof(PaletteEditorViewModel.PendingName):
            case nameof(PaletteEditorViewModel.CommittedName):
            case nameof(PaletteEditorViewModel.HasPendingChange):
                RefreshPreviewPanel();
                UpdateSwatchHighlight();
                break;
            case nameof(PaletteEditorViewModel.CanEditColors):
            case nameof(PaletteEditorViewModel.EditStatusText):
            case nameof(PaletteEditorViewModel.ProfileName):
                UpdateHeader();
                UpdateEditEnabled();
                break;
            case nameof(PaletteEditorViewModel.Transparency):
                RefreshTransparencyCheck();
                break;
        }
    }

    private void OnPaletteChanged()
    {
        RefreshSwatchColors();
        RefreshSwatchNames();
        RefreshPreviewPanel();
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        };

        _headerBlock.Foreground = Brushes.LightGray;
        _headerBlock.FontSize = 12;
        UpdateHeader();

        root.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = _headerBlock,
        });

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*,220"),
            Margin = new Thickness(12, 8),
        };

        var paletteColumn = new DockPanel { LastChildFill = true };
        var paletteLabel = MakeSectionLabel("Palette");
        DockPanel.SetDock(paletteLabel, Dock.Top);
        paletteColumn.Children.Add(paletteLabel);
        paletteColumn.Children.Add(new ScrollViewer
        {
            Content = _swatchListPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        Grid.SetColumn(paletteColumn, 0);
        body.Children.Add(paletteColumn);

        var pickerColumn = new StackPanel { Spacing = 8 };
        pickerColumn.Children.Add(MakeSectionLabel("Pick color"));
        _colorView.ColorChanged += (_, e) =>
        {
            if (_syncingPicker) return;
            _vm.OnPickerColorChanged(e.NewColor);
        };
        pickerColumn.Children.Add(_colorView);
        Grid.SetColumn(pickerColumn, 1);
        body.Children.Add(pickerColumn);

        var previewColumn = new StackPanel { Spacing = 8, Margin = new Thickness(12, 0, 0, 0) };
        previewColumn.Children.Add(MakeSectionLabel("Current / New"));

        var previewRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        previewRow.Children.Add(BuildPreviewBlock("Current", _currentPreview));
        previewRow.Children.Add(BuildPreviewBlock("New", _newPreview));
        previewColumn.Children.Add(previewRow);

        _hexInput.Width = 120;
        _hexInput.FontSize = 11;
        _hexInput.LostFocus += (_, _) => _vm.OnPendingHexChanged(_hexInput.Text ?? "");
        previewColumn.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Hex:", Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center },
                _hexInput,
            },
        });

        _nameInput.Width = 160;
        _nameInput.FontSize = 11;
        _nameInput.PlaceholderText = "color name";
        _nameInput.LostFocus += (_, _) => _vm.OnPendingNameChanged(_nameInput.Text ?? "");
        previewColumn.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Name:", Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center },
                _nameInput,
            },
        });

        _transpCheck.Content = "Transparent?";
        _transpCheck.Foreground = Brushes.LightGray;
        _transpCheck.FontSize = 11;
        _transpCheck.IsCheckedChanged += (_, _) =>
        {
            if (_transpCheck.IsChecked.HasValue)
                _vm.SetTransparency(_vm.SelectedIndex, _transpCheck.IsChecked.Value);
        };
        previewColumn.Children.Add(_transpCheck);

        _confirmBtn.Click += (_, _) =>
        {
            _vm.CommitPendingColor();
            RefreshSwatchNames();
        };
        previewColumn.Children.Add(_confirmBtn);

        _disabledLabel.Text = "Color editing is disabled for this profile.";
        _disabledLabel.Foreground = Brushes.Orange;
        _disabledLabel.FontSize = 11;
        _disabledLabel.IsVisible = false;
        _disabledLabel.TextWrapping = TextWrapping.Wrap;
        previewColumn.Children.Add(_disabledLabel);

        Grid.SetColumn(previewColumn, 2);
        body.Children.Add(previewColumn);

        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var footer = new StackPanel
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Margin = new Thickness(12),
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                MakeButton("Profile Defaults", () => { _vm.ResetToProfileDefaults(); RefreshAll(); }),
                MakeButton("Pico-8 Canonical", () => { _vm.ResetToPico8Canonical(); RefreshAll(); }),
            },
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        UpdateEditEnabled();
        return root;
    }

    private static StackPanel BuildPreviewBlock(string label, Border swatch)
    {
        swatch.Width = 48;
        swatch.Height = 48;
        swatch.BorderBrush = Brushes.Gray;
        swatch.BorderThickness = new Thickness(1);

        return new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.LightGray,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
                swatch,
            },
        };
    }

    private void RebuildSwatchList()
    {
        _swatchListPanel.Children.Clear();
        var count = _vm.ColorCount;

        for (var i = 0; i < count; i++)
        {
            var idx = i;
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(0, 1),
                Tag = idx,
            };

            var swatch = new Border
            {
                Width = 18,
                Height = 18,
                Background = new SolidColorBrush(Pico8Colors.ToAvaloniaColor(i, _vm.ColorChip)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            row.Children.Add(swatch);

            row.Children.Add(new TextBlock
            {
                Text = $"{i}  {_vm.GetColorName(i)}",
                Foreground = Brushes.LightGray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "name",
            });

            row.PointerPressed += (_, _) => _vm.SelectedIndex = idx;
            _swatchListPanel.Children.Add(row);
        }

        UpdateSwatchHighlight();
    }

    private void RefreshSwatchColors()
    {
        foreach (var child in _swatchListPanel.Children)
        {
            if (child is not StackPanel row || row.Tag is not int idx) continue;
            if (row.Children[0] is Border swatch)
                swatch.Background = new SolidColorBrush(Pico8Colors.ToAvaloniaColor(idx, _vm.ColorChip));
        }
    }

    private void RefreshSwatchNames()
    {
        foreach (var child in _swatchListPanel.Children)
        {
            if (child is not StackPanel row || row.Tag is not int idx) continue;
            foreach (var item in row.Children)
            {
                if (item is TextBlock { Tag: "name" } label)
                    label.Text = $"{idx}  {_vm.GetColorName(idx)}";
            }
        }
    }

    private void UpdateSwatchHighlight()
    {
        foreach (var child in _swatchListPanel.Children)
        {
            if (child is not StackPanel row || row.Tag is not int idx) continue;
            var selected = idx == _vm.SelectedIndex;
            row.Background = selected
                ? new SolidColorBrush(Color.Parse("#3E3E42"))
                : Brushes.Transparent;
            row.Margin = new Thickness(selected ? 0 : 0, 1);
        }
    }

    private void RefreshPreviewPanel()
    {
        _currentPreview.Background = new SolidColorBrush(_vm.CommittedColor());

        _syncingPicker = true;
        try
        {
            _colorView.Color = _vm.PendingColor;
        }
        finally
        {
            _syncingPicker = false;
        }

        _newPreview.Background = new SolidColorBrush(_vm.PendingColor);
        _hexInput.Text = _vm.PendingHex;
        _nameInput.Text = _vm.PendingName;
        _confirmBtn.IsEnabled = _vm.CanEditColors && _vm.HasPendingChange;
        RefreshTransparencyCheck();
    }

    private void RefreshTransparencyCheck()
    {
        var idx = _vm.SelectedIndex;
        if (idx < 0 || idx >= _vm.Transparency.Count) return;
        _transpCheck.IsChecked = _vm.Transparency[idx];
    }

    private void UpdateHeader() =>
        _headerBlock.Text = $"Palette Editor — Profile: {_vm.ProfileName} | {_vm.EditStatusText}";

    private void UpdateEditEnabled()
    {
        var enabled = _vm.CanEditColors;
        _colorView.IsEnabled = enabled;
        _hexInput.IsEnabled = enabled;
        _nameInput.IsEnabled = enabled;
        _confirmBtn.IsEnabled = enabled && _vm.HasPendingChange;
        _disabledLabel.IsVisible = !enabled;
    }

    private void RefreshAll()
    {
        UpdateHeader();
        RefreshSwatchColors();
        RefreshSwatchNames();
        RefreshPreviewPanel();
        UpdateSwatchHighlight();
        UpdateEditEnabled();
    }

    private static TextBlock MakeSectionLabel(string text) => new()
    {
        Text = text,
        Foreground = Brushes.LightGray,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 0, 0, 6),
    };

    private static Button MakeButton(string text, Action action)
    {
        var btn = new Button { Content = text, Padding = new Thickness(8, 3) };
        btn.Click += (_, _) => action();
        return btn;
    }
}
