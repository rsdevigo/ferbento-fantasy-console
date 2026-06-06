using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using Ferbento.Editor.Completion;
using Ferbento.Editor.Highlighting;

namespace Ferbento.Editor.Views;

public class CodeEditorView : UserControl
{
    private readonly TextEditor _scriptEditor;
    private CompletionWindow? _completionWindow;
    private bool _suppressChangeNotification;
    private static readonly IBrush EditorBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
    private static readonly IBrush EditorForeground = new SolidColorBrush(Color.Parse("#D4D4D4"));

    public CodeEditorView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _scriptEditor = CreateScriptEditor();
        _scriptEditor.TextChanged += (_, _) => OnSourceCodeChanged();
        Content = BuildLayout(_scriptEditor);
        _scriptEditor.Text = DefaultScript;
        Loaded += (_, _) =>
        {
            ApplySyntaxColors();
            AttachCompletion();
        };
    }

    public string SourceCode
    {
        get => _scriptEditor.Text;
        set
        {
            _suppressChangeNotification = true;
            _scriptEditor.Text = value;
            _suppressChangeNotification = false;
        }
    }

    public event EventHandler? SourceCodeChanged;

    private void OnSourceCodeChanged()
    {
        if (!_suppressChangeNotification)
            SourceCodeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Grid BuildLayout(TextEditor editor)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D30")),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = "Code Editor — GDScript",
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                FontSize = 12,
            },
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(editor, 1);
        grid.Children.Add(header);
        grid.Children.Add(editor);

        return grid;
    }

    private TextEditor CreateScriptEditor()
    {
        var editor = new TextEditor
        {
            ShowLineNumbers = true,
            WordWrap = false,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = 14,
            Background = EditorBackground,
            Foreground = EditorForeground,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        editor.TextArea.Background = EditorBackground;
        editor.TextArea.Foreground = EditorForeground;
        editor.TextArea.CaretBrush = Brushes.White;
        editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(128, 51, 153, 255));
        editor.TextArea.SelectionForeground = Brushes.White;
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 4;

        return editor;
    }

    private void AttachCompletion()
    {
        _scriptEditor.TextArea.TextEntered += (_, e) =>
        {
            if (_completionWindow != null || string.IsNullOrEmpty(e.Text)) return;

            var ch = e.Text[0];
            if (char.IsLetter(ch) || ch == '_')
            {
                var completions = Pico8CompletionDataSource
                    .GetCompletions(e.Text)
                    .Cast<ICompletionData>()
                    .ToList();
                if (completions.Count == 0) return;

                _completionWindow = new CompletionWindow(_scriptEditor.TextArea);
                foreach (var c in completions)
                    _completionWindow.CompletionList.CompletionData.Add(c);
                _completionWindow.Show();
                _completionWindow.Closed += (_, _) => _completionWindow = null;
            }
        };

        _scriptEditor.TextArea.TextEntering += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Text)) return;
            var ch = e.Text[0];
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                _completionWindow?.Close();
        };
    }

    private void ApplySyntaxColors()
    {
        _scriptEditor.SyntaxHighlighting = null;
        _scriptEditor.TextArea.TextView.LineTransformers.Add(new GDScriptColorizer());
        _scriptEditor.TextArea.TextView.Redraw();
        _scriptEditor.TextArea.Caret.Show();
    }

    private const string DefaultScript = """
        # Ferbento - Pico-8 compatible GDScript

        func _init():
            cls(0)

        func _update():
            if btn(0):
                pass

        func _draw():
            circfill(64, 64, 20, 8)
            flip()
        """;
}
