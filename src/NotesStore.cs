using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuickCapture;

/// <summary>Local storage: appends notes to a markdown file and stages
/// attachments into the attachments folder.</summary>
public static class NotesStore
{
    private static readonly string[] ImageExts =
        { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };

    public static bool IsImageFile(string path) =>
        ImageExts.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static List<CaptureAttachment> StageAttachments(
        IEnumerable<(string path, string displayName)> items, string attachmentsDir)
    {
        Directory.CreateDirectory(attachmentsDir);
        var result = new List<CaptureAttachment>();
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        int n = 0;
        foreach (var (path, displayName) in items)
        {
            if (!File.Exists(path)) continue;
            var safe = string.Concat(displayName.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safe)) safe = "attachment";
            var dest = Path.Combine(attachmentsDir, $"{stamp}-{n++}-{safe}");
            File.Copy(path, dest, overwrite: true);
            result.Add(new CaptureAttachment
            {
                SourcePath = path,
                StoredPath = dest,
                DisplayName = displayName,
                IsImage = IsImageFile(dest)
            });
        }
        return result;
    }

    public static void AppendNote(string notesFile, string text, List<CaptureAttachment> attachments,
        DateTimeOffset? reminder = null, string topic = "", string? priority = null, string? category = null)
    {
        var dir = Path.GetDirectoryName(notesFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var w = new StreamWriter(notesFile, append: true);
        w.WriteLine($"## {DateTime.Now:yyyy-MM-dd HH:mm}");
        w.WriteLine();
        w.WriteLine(string.IsNullOrWhiteSpace(text) ? "(no text)" : text.Trim());
        if (reminder.HasValue) w.WriteLine($"- Reminder: `{reminder:yyyy-MM-dd HH:mm}`");
        if (!string.IsNullOrWhiteSpace(topic)) w.WriteLine($"- Topic: `{topic}`");
        if (!string.IsNullOrWhiteSpace(priority)) w.WriteLine($"- Priority: `{priority}`");
        if (!string.IsNullOrWhiteSpace(category)) w.WriteLine($"- Category: `{category}`");
        foreach (var a in attachments)
            w.WriteLine($"- Attachment: `{Path.GetFileName(a.StoredPath)}`");
        w.WriteLine();
    }
}
