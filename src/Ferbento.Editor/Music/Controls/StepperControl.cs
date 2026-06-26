using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Ferbento.Editor.Music.Controls;

public sealed class StepperControl : UserControl
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<StepperControl, int>(nameof(Value));
    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<StepperControl, int>(nameof(Minimum), 0);

    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<StepperControl, int>(nameof(Maximum), 63);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StepperControl, string>(nameof(Label), "");

    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public event EventHandler<int>? ValueChanged;

    private readonly TextBlock _valueText;
    private readonly Button _decBtn;
    private readonly Button _incBtn;

    static StepperControl()
    {
        ValueProperty.Changed.AddClassHandler<StepperControl>((s, _) =>
        {
            if (s._valueText == null) return;
            s._valueText.Text = s.Value.ToString("D2");
            s.ValueChanged?.Invoke(s, s.Value);
        });
    }

    public StepperControl()
    {
        var label = new TextBlock
        {
            Foreground = new SolidColorBrush(MusicTheme.TextDim),
            FontSize = 9,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };
        label.Bind(TextBlock.TextProperty, this.GetObservable(LabelProperty));

        _valueText = new TextBlock
        {
            Foreground = new SolidColorBrush(MusicTheme.Accent),
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            MinWidth = 28,
            TextAlignment = TextAlignment.Center,
        };

        _decBtn = MakeArrow("\u25C0");
        _incBtn = MakeArrow("\u25B6");
        _decBtn.Click += (_, _) => Change(-1);
        _incBtn.Click += (_, _) => Change(1);

        var row = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                label,
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 4,
                    Children = { _decBtn, _valueText, _incBtn },
                },
            },
        };

        Background = new SolidColorBrush(MusicTheme.Panel);
        BorderBrush = new SolidColorBrush(MusicTheme.PanelBorder);
        BorderThickness = new Thickness(2);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(6, 4);
        Content = row;
        _valueText.Text = Value.ToString("D2");
    }

    private void Change(int delta)
    {
        Value = Math.Clamp(Value + delta, Minimum, Maximum);
    }

    private static Button MakeArrow(string glyph) => new()
    {
        Content = glyph,
        Padding = new Thickness(4, 0),
        Background = new SolidColorBrush(MusicTheme.ButtonFace),
        Foreground = new SolidColorBrush(MusicTheme.Accent),
        BorderBrush = new SolidColorBrush(MusicTheme.PanelBorder),
        BorderThickness = new Thickness(1),
        Focusable = false,
    };
}
