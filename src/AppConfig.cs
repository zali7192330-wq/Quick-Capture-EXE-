using System;
using System.IO;
using System.Text.Json;

namespace QuickCapture;

public class AppConfig
{
    public string NotionToken { get; set; } = "PASTE_YOUR_TOKEN_HERE";
    public string DatabaseId { get; set; } = "81299005-88a2-4022-b2e4-744c09e6ccb1";
    public string NotesFile { get; set; } = "notes.md";
    public string AttachmentsFolder { get; set; } = "attachments";

    // Lists offered in the ~list popup of the capture bar
    public string[] Lists { get; set; } =
        { "Inbox", "Education", "Career", "Fitness", "Health", "Personal" };

    // Categories offered in the !category popup of the capture bar
    public string[] Categories { get; set; } =
        { "Spark", "TIL", "Prompt" };

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(NotionToken) && !NotionToken.StartsWith("PASTE", StringComparison.OrdinalIgnoreCase);

    public string NotesFilePath =>
        Path.IsPathRooted(NotesFile) ? NotesFile : Path.Combine(AppContext.BaseDirectory, NotesFile);

    public string AttachmentsPath =>
        Path.IsPathRooted(AttachmentsFolder) ? AttachmentsFolder : Path.Combine(AppContext.BaseDirectory, AttachmentsFolder);

    public string NotionDatabaseUrl =>
        "https://www.notion.so/" + DatabaseId.Replace("-", "");

    public static AppConfig Load(string path)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
        try
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonSerializer.Serialize(new AppConfig(), options));
                return new AppConfig();
            }
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), options) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }
}
