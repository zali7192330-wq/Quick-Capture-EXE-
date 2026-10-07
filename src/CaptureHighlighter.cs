using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using System.Collections.Generic;

namespace QuickCapture;

/// <summary>
/// Colors capture modifiers inside the input bar: priority red,
/// date/time blue, ~lists indigo, #tags purple.
/// </summary>
public class CaptureHighlighter : DocumentColorizingTransformer
{
    public List<TokenSpan> Tokens { get; set; } = new();

    private static IBrush BrushFor(TokenKind kind) => new SolidColorBrush(
        kind switch
        {
            TokenKind.Priority => Avalonia.Media.Color.Parse("#F87171"),
            TokenKind.DateTime => Avalonia.Media.Color.Parse("#60A5FA"),
            TokenKind.List => Avalonia.Media.Color.Parse("#818CF8"),
            TokenKind.Category => Avalonia.Media.Color.Parse("#FBBF24"),
            _ => Avalonia.Media.Color.Parse("#C084FC"),
        });

    protected override void ColorizeLine(DocumentLine line)
    {
        if (Tokens.Count == 0) return;
        int lineStart = line.Offset;
        int lineEnd = lineStart + line.Length;
        foreach (var t in Tokens)
        {
            int s = System.Math.Max(t.Start, lineStart);
            int e = System.Math.Min(t.Start + t.Length, lineEnd);
            if (s >= e) continue;
            var brush = BrushFor(t.Kind);
            ChangeLinePart(s, e, el => el.TextRunProperties.SetForegroundBrush(brush));
        }
    }
}
