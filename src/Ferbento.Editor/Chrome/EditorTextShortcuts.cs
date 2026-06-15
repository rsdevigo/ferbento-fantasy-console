namespace Ferbento.Editor.Chrome;

/// <summary>
/// Text-input fallbacks for editor shortcuts (ABNT2 and layouts where KeyDown uses other names).
/// </summary>
public static class EditorTextShortcuts
{
    public static bool TryZoom(string text, Action zoomIn, Action zoomOut)
    {
        if (text.Length != 1) return false;
        return text[0] switch
        {
            '+' or '=' => Execute(zoomIn),
            '-' => Execute(zoomOut),
            _ => false,
        };
    }

    public static bool TryBrushSize(string text, Action smaller, Action larger)
    {
        if (text.Length != 1) return false;
        return text[0] switch
        {
            '[' => Execute(smaller),
            ']' => Execute(larger),
            _ => false,
        };
    }

    private static bool Execute(Action action)
    {
        action();
        return true;
    }
}
