using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ferbento.Core.Audio;

namespace Ferbento.Editor.Sfx.Controls;

public sealed class SfxEffectParamPanel : StackPanel
{
  private readonly Dictionary<string, NumericUpDown> _fields = new();
  private SfxEffect _effect = SfxEffect.None;
  private bool _syncing;
  private Action<int>? _onParamChanged;

  public SfxEffectParamPanel()
  {
    Spacing = 4;
    IsVisible = false;
  }

  public void SetOnParamChanged(Action<int> handler) => _onParamChanged = handler;

  public void SetEffect(SfxEffect effect, int param)
  {
    _effect = effect;
    if (effect == SfxEffect.None)
    {
      IsVisible = false;
      Children.Clear();
      _fields.Clear();
      return;
    }

    IsVisible = true;
    SfxEffectParamCodec.Unpack(effect, param, out int f0, out int f1, out int f2);
    var labels = SfxEffectParamCodec.FieldLabels(effect);

    if (_fields.Count == 0 || !MatchesEffectLayout(effect))
      RebuildFields(effect, labels);

    _syncing = true;
    try
    {
      SetField("f0", f0);
      if (!string.IsNullOrEmpty(labels.f1)) SetField("f1", f1);
      if (!string.IsNullOrEmpty(labels.f2)) SetField("f2", f2);
    }
    finally
    {
      _syncing = false;
    }
  }

  private bool MatchesEffectLayout(SfxEffect effect)
  {
    var labels = SfxEffectParamCodec.FieldLabels(effect);
    bool needF1 = !string.IsNullOrEmpty(labels.f1);
    bool needF2 = !string.IsNullOrEmpty(labels.f2);
    return _fields.ContainsKey("f0")
           && _fields.ContainsKey("f1") == needF1
           && _fields.ContainsKey("f2") == needF2;
  }

  private void RebuildFields(SfxEffect effect, (string f0, string f1, string f2) labels)
  {
    Children.Clear();
    _fields.Clear();

    AddField("f0", labels.f0, effect is SfxEffect.Arpeggio or SfxEffect.Arpeggio2 ? 15 : 255);
    if (!string.IsNullOrEmpty(labels.f1))
      AddField("f1", labels.f1, effect is SfxEffect.Arpeggio or SfxEffect.Arpeggio2 ? 15 : 255);
    if (!string.IsNullOrEmpty(labels.f2))
      AddField("f2", labels.f2, 15);
  }

  private void AddField(string key, string label, int max)
  {
    var input = new NumericUpDown
    {
      Minimum = 0,
      Maximum = max,
      Width = 40,
      MinHeight = 22,
      Height = 22,
      FontSize = 11,
      ShowButtonSpinner = false,
      Padding = new Thickness(4, 0),
      HorizontalContentAlignment = HorizontalAlignment.Center,
      VerticalContentAlignment = VerticalAlignment.Center,
    };
    input.ValueChanged += (_, _) =>
    {
      if (_syncing) return;
      EmitParam();
    };
    _fields[key] = input;

    Children.Add(new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 4,
      Children =
      {
        new TextBlock
        {
          Text = label,
          FontSize = 10,
          Foreground = Brushes.LightGray,
          VerticalAlignment = VerticalAlignment.Center,
          MinWidth = 52,
        },
        input,
      },
    });
  }

  private void SetField(string key, int value)
  {
    if (_fields.TryGetValue(key, out var input))
      input.Value = value;
  }

  private int ReadField(string key) =>
    _fields.TryGetValue(key, out var input) && input.Value.HasValue
      ? (int)input.Value.Value
      : 0;

  private void EmitParam()
  {
    int f0 = ReadField("f0");
    int f1 = ReadField("f1");
    int f2 = ReadField("f2");
    int packed = SfxEffectParamCodec.Pack(_effect, f0, f1, f2);
    _onParamChanged?.Invoke(packed);
  }
}
