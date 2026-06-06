using System;
using System.Reflection;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Ferbento.Editor.Highlighting;

public static class GDScriptHighlighting
{
    public static IHighlightingDefinition Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ferbento.Editor.Highlighting.gdscript.xshd";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
