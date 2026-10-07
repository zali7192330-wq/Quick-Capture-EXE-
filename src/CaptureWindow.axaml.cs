using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuickCapture;

public partial class CaptureWindow : Window
{
    private readonly List<PendingAttachment> _pending = new();
    private readonly NotionClient _notion =
        new(AppServices.Config.NotionToken, AppServices.Config.DatabaseId);
    private readonly CaptureHighlighter _highlighter = new();
    private bool _suppressTextChanged;

    private class PendingAttachment
    {
        public string TempPath = "";
        public string DisplayName = "";
        public bool DeleteAfter;
        public Avalonia.Media.Imaging.Bitmap? Preview;
    }

    public CaptureWindow()
    {
        InitializeComponent();

        Icon = LoadAppIcon();

        Editor.TextArea.TextView.LineTransformers.Add(_highlighter);
        Editor.TextChanged += (_, _) => OnTextChanged();

        // The editor's inner text area must hold keyboard focus for typing
        // and the caret to appear; make sure clicks anywhere in the input
        // box land there.
        Editor.GotFocus += (_, _) =>
        {
            if (!Editor.TextArea.IsFocused)
                Editor.TextArea.Focus();
        };
        InputBorder.PointerPressed += (_, _) => Editor.TextArea.Focus();

        SaveButton.Click += async (_, _) => await SaveAsync();
        AttachButton.Click += async (_, _) => await PickFilesAsync();

        ListBox.Tapped += (_, _) => ApplyListSelection();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // Tunneling: runs before the editor consumes keys, so Enter/Esc/Ctrl+V
        // behave like a quick-capture bar.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private static WindowIcon LoadAppIcon()
    {
        using var s = AssetLoader.Open(new Uri("avares://QuickCapture/Assets/icon.ico"));
        return new WindowIcon(s);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Open on the monitor holding the mouse cursor, not a fixed screen.
        try
        {
            var pt = System.Windows.Forms.Cursor.Position;
            var target = Screens.All.FirstOrDefault(s =>
                    s.Bounds.X <= pt.X && pt.X < s.Bounds.X + s.Bounds.Width &&
                    s.Bounds.Y <= pt.Y && pt.Y < s.Bounds.Y + s.Bounds.Height)
                ?? Screens.Primary;
            if (target != null)
            {
                var b = target.Bounds;
                double h = Bounds.Height > 0 ? Bounds.Height : 230;
                Position = new PixelPoint(
                    b.X + Math.Max(0, (b.Width - (int)(Bounds.Width * target.Scaling)) / 2),
                    b.Y + Math.Max(0, (int)(b.Height * 0.30) - (int)(h * target.Scaling) / 2));
            }
        }
        catch { }
        // Focus the inner text area directly: focusing the outer editor
        // control does not reliably give it keyboard focus.
        Dispatcher.UIThread.Post(() => Editor.TextArea.Focus(), DispatcherPriority.Loaded);
    }

    private string EditorText => Editor.Text ?? "";
    private int Caret => Math.Min(Editor.TextArea.Caret.Offset, EditorText.Length);

    // ---- smart parsing, coloring + list dropdown -------------------------

    private void OnTextChanged()
    {
        if (_suppressTextChanged) return;
        var text = EditorText;
        var p = SmartParse.Parse(text, AppServices.Config.Lists, AppServices.Config.Categories);

        _highlighter.Tokens = p.Tokens;
        Editor.TextArea.TextView.Redraw();

        Watermark.IsVisible = string.IsNullOrEmpty(text);
        if (string.IsNullOrEmpty(p.Hint))
        {
            ParsedHint.Text = "try: \"Buy milk tomorrow at 5pm #groceries ~Personal #1 !Spark\"";
            ParsedHint.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#71717A"));
        }
        else
        {
            ParsedHint.Text = p.Hint;
            ParsedHint.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#2DD4BF"));
        }
        UpdateListPopup();
    }

    private void UpdateListPopup()
    {
        var text = EditorText;
        int caret = Caret;
        // ~ opens the Topic list popup, ! opens the Category popup
        var m = Regex.Match(text.Substring(0, caret), @"([~!])([A-Za-z0-9_]*)$");
        if (!m.Success)
        {
            ListPopup.IsOpen = false;
            return;
        }
        var trigger = m.Groups[1].Value;
        var partial = m.Groups[2].Value;
        var source = trigger == "~" ? AppServices.Config.Lists : AppServices.Config.Categories;
        var items = source
            .Where(l => l.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (items.Count == 0)
        {
            ListPopup.IsOpen = false;
            return;
        }
        _popupTrigger = trigger;
        ListBox.ItemsSource = items;
        ListBox.SelectedIndex = 0;
        ListPopup.PlacementTarget = Editor;
        ListPopup.IsOpen = true;
    }

    private string _popupTrigger = "~";

    private void ApplyListSelection()
    {
        if (ListBox.SelectedItem is not string chosen)
        {
            ListPopup.IsOpen = false;
            return;
        }
        var text = EditorText;
        int caret = Caret;
        var m = Regex.Match(text.Substring(0, caret), @"([~!])([A-Za-z0-9_]*)$");
        if (!m.Success)
        {
            ListPopup.IsOpen = false;
            return;
        }
        _suppressTextChanged = true;
        var replacement = _popupTrigger + chosen + " ";
        Editor.Document.Text = text.Substring(0, m.Index) + replacement + text.Substring(caret);
        Editor.TextArea.Caret.Offset = m.Index + replacement.Length;
        _suppressTextChanged = false;
        ListPopup.IsOpen = false;
        OnTextChanged();
        Editor.TextArea.Focus();
    }

    private void MoveListSelection(int delta)
    {
        if (ListBox.ItemsSource is not System.Collections.IList items || items.Count == 0) return;
        int i = Math.Max(0, Math.Min(items.Count - 1, ListBox.SelectedIndex + delta));
        ListBox.SelectedIndex = i;
        ListBox.ScrollIntoView(i);
    }

    private void OnPreviewKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (ListPopup.IsOpen)
        {
            if (e.Key == Key.Down) { MoveListSelection(1); e.Handled = true; return; }
            if (e.Key == Key.Up) { MoveListSelection(-1); e.Handled = true; return; }
            if (e.Key == Key.Enter || e.Key == Key.Tab) { ApplyListSelection(); e.Handled = true; return; }
            if (e.Key == Key.Escape) { ListPopup.IsOpen = false; e.Handled = true; return; }
        }
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _ = SaveAsync();
            return;
        }
        // Shift+Enter falls through -> editor inserts a newline.
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.V && ClipboardHasMedia())
        {
            e.Handled = true;
            PasteFromClipboard();
        }
    }

    // ---- attachments ------------------------------------------------------

    private void AddPending(string path, string displayName, bool deleteAfter = false)
    {
        if (!File.Exists(path)) return;
        var p = new PendingAttachment
        {
            TempPath = path,
            DisplayName = displayName,
            DeleteAfter = deleteAfter
        };
        if (NotesStore.IsImageFile(path))
        {
            try { p.Preview = new Avalonia.Media.Imaging.Bitmap(path); } catch { p.Preview = null; }
        }
        _pending.Add(p);
        RenderChips();
    }

    private void RemovePending(PendingAttachment p)
    {
        _pending.Remove(p);
        p.Preview?.Dispose();
        p.Preview = null;
        if (p.DeleteAfter) TryDelete(p.TempPath);
        RenderChips();
    }

    private Avalonia.Controls.Control BuildPreview(PendingAttachment p)
    {
        if (p.Preview != null)
        {
            return new Border
            {
                Width = 56, Height = 56,
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true,
                Child = new Avalonia.Controls.Image
                {
                    Source = p.Preview,
                    Stretch = Stretch.UniformToFill,
                    Width = 56, Height = 56
                }
            };
        }
        var ext = Path.GetExtension(p.DisplayName).TrimStart('.').ToUpperInvariant();
        if (ext.Length > 4) ext = ext.Substring(0, 4);
        if (string.IsNullOrEmpty(ext)) ext = "FILE";
        return new Border
        {
            Width = 56, Height = 56,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#3A3A40")),
            Child = new TextBlock
            {
                Text = ext,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#B9B9C0")),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        };
    }

    private void RenderChips()
    {
        ChipPanel.Children.Clear();
        foreach (var p in _pending.ToList())
        {
            var local = p;
            var name = new TextBlock
            {
                Text = local.DisplayName,
                FontSize = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                MaxWidth = 160,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var remove = new Avalonia.Controls.Button
            {
                Content = "×",
                FontSize = 13,
                Padding = new Thickness(6, 0),
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#F87171")),
                BorderThickness = new Thickness(0),
                Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            remove.Click += (_, _) => RemovePending(local);
            ChipPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#2C2C30")),
                BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#3A3A40")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 8, 8),
                Child = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    Children = { BuildPreview(local), name, remove }
                }
            });
        }
        ChipPanel.IsVisible = _pending.Count > 0;
    }

    private static bool ClipboardHasMedia()
    {
        try
        {
            return System.Windows.Forms.Clipboard.ContainsImage()
                || System.Windows.Forms.Clipboard.ContainsFileDropList();
        }
        catch
        {
            return false;
        }
    }

    private void PasteFromClipboard()
    {
        try
        {
            if (System.Windows.Forms.Clipboard.ContainsImage())
            {
                using var img = System.Windows.Forms.Clipboard.GetImage();
                if (img != null)
                {
                    var tmp = Path.Combine(Path.GetTempPath(), $"qc-paste-{Guid.NewGuid():N}.png");
                    img.Save(tmp, ImageFormat.Png);
                    AddPending(tmp, "pasted-image.png", deleteAfter: true);
                    SetStatus("Image pasted.", "#2DD4BF");
                    return;
                }
            }
            if (System.Windows.Forms.Clipboard.ContainsFileDropList())
            {
                int n = 0;
                foreach (string? f in System.Windows.Forms.Clipboard.GetFileDropList())
                {
                    if (!string.IsNullOrEmpty(f) && File.Exists(f))
                    {
                        AddPending(f, Path.GetFileName(f));
                        n++;
                    }
                }
                if (n > 0)
                {
                    SetStatus($"{n} file(s) attached.", "#2DD4BF");
                    return;
                }
            }
            SetStatus("Clipboard has no image or files.", "#8E8E96");
        }
        catch (Exception ex)
        {
            SetStatus("Paste failed: " + ex.Message, "#F87171");
        }
    }

    private async Task PickFilesAsync()
    {
        var dlg = new Avalonia.Controls.OpenFileDialog
        {
            AllowMultiple = true,
            Title = "Attach files"
        };
#pragma warning disable CS0618
        var files = await dlg.ShowAsync(this);
#pragma warning restore CS0618
        if (files == null) return;
        foreach (var f in files)
            if (File.Exists(f)) AddPending(f, Path.GetFileName(f));
    }

    private void OnDrop(object? sender, Avalonia.Input.DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        if (files == null) return;
        foreach (var item in files)
        {
            var path = item.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                AddPending(path, Path.GetFileName(path));
        }
    }

    // ---- save ---------------------------------------------------------------

    private async Task SaveAsync()
    {
        var parsed = SmartParse.Parse(EditorText, AppServices.Config.Lists, AppServices.Config.Categories);
        var cleanText = parsed.CleanText;
        if (string.IsNullOrWhiteSpace(cleanText) && _pending.Count == 0)
        {
            SetStatus("Write something or attach a file first.", "#F87171");
            return;
        }

        SaveButton.IsEnabled = false;
        SetStatus("Saving…", "#8E8E96");

        try
        {
            var cfg = AppServices.Config;
            var staged = NotesStore.StageAttachments(
                _pending.Select(p => (p.TempPath, p.DisplayName)), cfg.AttachmentsPath);

            var topicParts = new List<string>();
            if (!string.IsNullOrEmpty(parsed.List)) topicParts.Add(parsed.List);
            topicParts.AddRange(parsed.Tags);
            var topic = string.Join(", ", topicParts);

            NotesStore.AppendNote(cfg.NotesFilePath, cleanText, staged,
                parsed.Reminder, topic, parsed.Priority, parsed.Category);

            if (cfg.IsConfigured)
            {
                var (ok, message) = await _notion.CreateCaptureAsync(
                    cleanText, staged, parsed.Reminder, topic, parsed.Priority, parsed.Category);
                if (!ok)
                {
                    SetStatus(message, "#F87171");
                    SaveButton.IsEnabled = true;
                    return;
                }
            }

            CleanupTemp();
            Close();
        }
        catch (Exception ex)
        {
            SetStatus("Save failed: " + ex.Message, "#F87171");
            SaveButton.IsEnabled = true;
        }
    }

    private void CleanupTemp()
    {
        foreach (var p in _pending)
        {
            p.Preview?.Dispose();
            p.Preview = null;
            if (p.DeleteAfter) TryDelete(p.TempPath);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private void SetStatus(string text, string color)
    {
        StatusText.Text = text;
        StatusText.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse(color));
    }
}
