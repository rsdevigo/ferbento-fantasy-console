namespace Ferbento.Editor.Music.Input;

public enum MusicEditorAction
{
    PlayStop,
    PlayPattern,
    PlaySong,
    Stop,
    PrevPattern,
    NextPattern,
    TempoDown,
    TempoUp,
    MoveLeft,
    MoveRight,
    MoveUp,
    MoveDown,
    DeleteCell,
    Undo,
    Redo,
    RandomGenerate,
}

public static class MusicEditorInputMap
{
    public static IReadOnlyDictionary<MusicEditorAction, string> DefaultBindings { get; } =
        new Dictionary<MusicEditorAction, string>
        {
            [MusicEditorAction.PlayStop] = "Space",
            [MusicEditorAction.PlayPattern] = "Enter",
            [MusicEditorAction.Stop] = "Escape",
            [MusicEditorAction.PrevPattern] = "OemOpenBrackets",
            [MusicEditorAction.NextPattern] = "OemCloseBrackets",
            [MusicEditorAction.TempoDown] = "OemMinus",
            [MusicEditorAction.TempoUp] = "OemPlus",
            [MusicEditorAction.MoveLeft] = "Left",
            [MusicEditorAction.MoveRight] = "Right",
            [MusicEditorAction.MoveUp] = "Up",
            [MusicEditorAction.MoveDown] = "Down",
            [MusicEditorAction.DeleteCell] = "Delete",
            [MusicEditorAction.Undo] = "Z, Control",
            [MusicEditorAction.Redo] = "Y, Control",
            [MusicEditorAction.RandomGenerate] = "R, Control",
        };

    public static bool Matches(string key, bool ctrl, bool shift, bool alt, MusicEditorAction action)
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
