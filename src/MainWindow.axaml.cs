using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Diagnostics;

namespace QuickCapture;

public partial class MainWindow : Window
{
    private readonly NotionClient _notion;

    public MainWindow()
    {
        InitializeComponent();
        using (var s = Avalonia.Platform.AssetLoader.Open(new Uri("avares://QuickCapture/Assets/icon.ico")))
            Icon = new WindowIcon(s);
        _notion = new NotionClient(AppServices.Config.NotionToken, AppServices.Config.DatabaseId);

        NewCaptureButton.Click += (_, _) => ShowCapture();
        OpenNotesButton.Click += (_, _) => OpenNotesFile();
        OpenNotionButton.Click += (_, _) => OpenNotionDatabase();
        HideButton.Click += (_, _) => Hide();

        Closing += (_, e) => { e.Cancel = true; Hide(); }; // X hides to tray; quit via tray menu

        _ = RefreshStatusAsync();
    }

    public void ShowCapture()
    {
        var w = new CaptureWindow();
        w.Show();
        w.Activate();
    }

    private async System.Threading.Tasks.Task RefreshStatusAsync()
    {
        if (!AppServices.Config.IsConfigured)
        {
            SetStatus("Notion not configured — paste your token into config.json", "#F87171");
            return;
        }
        SetStatus("Checking Notion…", "#8E8E96");
        var (ok, message) = await _notion.CheckConnectionAsync();
        SetStatus(message, ok ? "#2DD4BF" : "#F87171");
    }

    private void SetStatus(string text, string color)
    {
        StatusText.Text = text;
        StatusText.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse(color));
    }

    private void OpenNotesFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppServices.Config.NotesFilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SetStatus("Could not open notes file: " + ex.Message, "#F87171");
        }
    }

    private void OpenNotionDatabase()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppServices.Config.NotionDatabaseUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SetStatus("Could not open browser: " + ex.Message, "#F87171");
        }
    }
}
