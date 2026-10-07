using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using System;

namespace QuickCapture;

public partial class App : Avalonia.Application
{
    private HotkeyManager? _hotkeys;
    private MainWindow? _main;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        AppServices.Config = AppConfig.Load(
            System.IO.Path.Combine(AppContext.BaseDirectory, "config.json"));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _main = new MainWindow();
            desktop.MainWindow = _main;

            SetupTray(desktop);

            _hotkeys = new HotkeyManager();
            _hotkeys.HotkeyPressed += () =>
                Dispatcher.UIThread.Post(() => _main.ShowCapture());
            _hotkeys.Start();

            desktop.ShutdownRequested += (_, _) => _hotkeys.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        using var s = AssetLoader.Open(new Uri("avares://QuickCapture/Assets/icon.ico"));
        var tray = new TrayIcon
        {
            Icon = new WindowIcon(s),
            ToolTipText = "Quick Capture"
        };

        var menu = new NativeMenu();
        var showItem = new NativeMenuItem("Show");
        showItem.Click += (_, _) => Dispatcher.UIThread.Post(() => { _main?.Show(); _main?.Activate(); });
        var captureItem = new NativeMenuItem("New capture");
        captureItem.Click += (_, _) => Dispatcher.UIThread.Post(() => _main?.ShowCapture());
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => desktop.Shutdown();
        menu.Add(showItem);
        menu.Add(captureItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quitItem);
        tray.Menu = menu;
        tray.Clicked += (_, _) => Dispatcher.UIThread.Post(() => { _main?.Show(); _main?.Activate(); });

        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }
}
