using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Ferbento.Editor.Chrome;

/// <summary>
/// Routes keyboard input to the editor root.
/// Buttons and toggles take focus otherwise and swallow shortcuts.
/// </summary>
public static class EditorKeyboardRouting
{
    public static void Attach(
        InputElement root,
        EventHandler<KeyEventArgs> onKeyDown,
        EventHandler<KeyEventArgs> onKeyUp,
        EventHandler<TextInputEventArgs>? onTextInput = null)
    {
        root.Focusable = true;
        root.AddHandler(InputElement.KeyDownEvent, onKeyDown, RoutingStrategies.Tunnel);
        root.AddHandler(InputElement.KeyUpEvent, onKeyUp, RoutingStrategies.Tunnel);
        if (onTextInput != null)
            root.AddHandler(InputElement.TextInputEvent, onTextInput, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Prevents toolbar/context buttons from taking keyboard focus away from the editor.
    /// </summary>
    public static void PreventFocusSteal(Control root)
    {
        foreach (var control in root.GetVisualDescendants().OfType<Control>())
        {
            if (control is Button or ToggleButton or CheckBox)
                control.Focusable = false;
        }
    }

    /// <summary>
    /// True when a text field has focus, so shortcuts should not handle the key.
    /// </summary>
    public static bool ShouldDeferToFocusedTextInput(InputElement editorRoot)
    {
        var focused = TopLevel.GetTopLevel(editorRoot)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox or NumericUpDown or ComboBox)
            return true;
        if (focused is Visual visual)
        {
            return visual.FindAncestorOfType<TextBox>() != null
                || visual.FindAncestorOfType<NumericUpDown>() != null;
        }

        return false;
    }

    /// <summary>
    /// True when the pointer target is inside an editable chrome control.
    /// </summary>
    public static bool IsEditableChromeTarget(object? source)
    {
        if (source is not Visual visual)
            return false;
        return visual.FindAncestorOfType<TextBox>() != null
            || visual.FindAncestorOfType<NumericUpDown>() != null
            || visual.FindAncestorOfType<ComboBox>() != null;
    }
}
