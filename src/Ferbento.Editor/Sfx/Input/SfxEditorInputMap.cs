namespace Ferbento.Editor.Sfx.Input;

public enum SfxEditorAction
{
  PlayStop,
  PlayFromCursor,
  Stop,
  ToggleLoop,
  PrevSlot,
  NextSlot,
  MoveLeft,
  MoveRight,
  MoveUp,
  MoveDown,
  Home,
  End,
  SelectAll,
  Copy,
  Cut,
  Paste,
  Duplicate,
  Delete,
  ClearSlot,
  Undo,
  Redo,
  TransposeUp,
  TransposeDown,
  TransposeOctaveUp,
  TransposeOctaveDown,
  ShiftRight,
  ShiftLeft,
  ZoomIn,
  ZoomOut,
  ZoomFit,
  ToolPencil,
  ToolEraser,
  ToolSelect,
  ToolFill,
  Waveform1,
  Waveform2,
  Waveform3,
  Waveform4,
  Waveform5,
  Waveform6,
  Waveform7,
  Waveform8,
  CycleEffect,
  CycleEffectBack,
  ToggleCustomInst,
}

public static class SfxEditorInputMap
{
  public static IReadOnlyDictionary<SfxEditorAction, string> DefaultBindings { get; } =
    new Dictionary<SfxEditorAction, string>
    {
      [SfxEditorAction.PlayStop] = "Space",
      [SfxEditorAction.PlayFromCursor] = "Space, Shift",
      [SfxEditorAction.Stop] = "Escape",
      [SfxEditorAction.ToggleLoop] = "L",
      [SfxEditorAction.PrevSlot] = "Q",
      [SfxEditorAction.NextSlot] = "W",
      [SfxEditorAction.MoveLeft] = "Left",
      [SfxEditorAction.MoveRight] = "Right",
      [SfxEditorAction.MoveUp] = "Up",
      [SfxEditorAction.MoveDown] = "Down",
      [SfxEditorAction.Home] = "Home",
      [SfxEditorAction.End] = "End",
      [SfxEditorAction.SelectAll] = "A, Control",
      [SfxEditorAction.Copy] = "C, Control",
      [SfxEditorAction.Cut] = "X, Control",
      [SfxEditorAction.Paste] = "V, Control",
      [SfxEditorAction.Duplicate] = "D, Control",
      [SfxEditorAction.Delete] = "Delete",
      [SfxEditorAction.ClearSlot] = "Delete, Control+Shift",
      [SfxEditorAction.Undo] = "Z, Control",
      [SfxEditorAction.Redo] = "Y, Control",
      [SfxEditorAction.TransposeUp] = "Up, Shift",
      [SfxEditorAction.TransposeDown] = "Down, Shift",
      [SfxEditorAction.TransposeOctaveUp] = "Up, Control",
      [SfxEditorAction.TransposeOctaveDown] = "Down, Control",
      [SfxEditorAction.ShiftRight] = "Right, Control",
      [SfxEditorAction.ShiftLeft] = "Left, Control",
      [SfxEditorAction.ZoomIn] = "OemPlus, Control",
      [SfxEditorAction.ZoomOut] = "OemMinus, Control",
      [SfxEditorAction.ZoomFit] = "D0, Control",
      [SfxEditorAction.ToolPencil] = "P",
      [SfxEditorAction.ToolEraser] = "E",
      [SfxEditorAction.ToolSelect] = "S",
      [SfxEditorAction.ToolFill] = "F",
      [SfxEditorAction.Waveform1] = "D1",
      [SfxEditorAction.Waveform2] = "D2",
      [SfxEditorAction.Waveform3] = "D3",
      [SfxEditorAction.Waveform4] = "D4",
      [SfxEditorAction.Waveform5] = "D5",
      [SfxEditorAction.Waveform6] = "D6",
      [SfxEditorAction.Waveform7] = "D7",
      [SfxEditorAction.Waveform8] = "D8",
      [SfxEditorAction.CycleEffect] = "X",
      [SfxEditorAction.CycleEffectBack] = "X, Shift",
      [SfxEditorAction.ToggleCustomInst] = "I",
    };

  public static bool Matches(string key, bool ctrl, bool shift, bool alt, SfxEditorAction action)
  {
    if (!DefaultBindings.TryGetValue(action, out var binding)) return false;
    foreach (var part in binding.Split(',', StringSplitOptions.TrimEntries))
    {
      var tokens = part.Split('+', StringSplitOptions.TrimEntries);
      bool needCtrl = false, needShift = false, needAlt = false;
      string? keyToken = null;
      foreach (var t in tokens)
      {
        switch (t.ToLowerInvariant())
        {
          case "control": needCtrl = true; break;
          case "shift": needShift = true; break;
          case "alt": needAlt = true; break;
          default: keyToken = t; break;
        }
      }

      if (needCtrl != ctrl || needShift != shift || needAlt != alt) continue;
      if (keyToken != null && key.Equals(keyToken, StringComparison.OrdinalIgnoreCase))
        return true;
    }

    return false;
  }
}
