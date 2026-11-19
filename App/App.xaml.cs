using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Application = Avalonia.Application;
using Button = Avalonia.Controls.Button;

namespace Jime;

public partial class App : Application
{
    private const int HotkeyId = 1;
    private const int WmHotkey = 0x0312;
    private Mutex? _mutex;
    private bool _ownsMutex;
    private MainWindow? _window;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private IntPtr _target;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        _desktop = desktop;
        CreateTrayMenu();
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.Exit += OnDesktopExit;

        _mutex = new Mutex(true, @"Global\Jime.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            desktop.Shutdown();
            return;
        }
        _ownsMutex = true;

        _window = new MainWindow();
        _window.CommitRequested += OnCommit;
        _window.CancelRequested += () => _window.Hide();
        desktop.MainWindow = _window;
        _window.Opacity = 0;
        _window.Show();
        _window.Hide();
        _window.Opacity = 1;

        var handle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        NativeMethods.ImmAssociateContext(handle, IntPtr.Zero);
        Win32Properties.AddWndProcHookCallback(_window, WindowMessage);
        if (!NativeMethods.RegisterHotKey(handle, HotkeyId,
                NativeMethods.ModControl | NativeMethods.ModAlt | NativeMethods.ModNoRepeat, 0x4A))
        {
            ShowWarning("无法注册 Ctrl+Alt+J，快捷键可能被其他程序占用。");
        }

        if (Environment.GetCommandLineArgs().Contains("--preview")) _window.BeginInput();
        base.OnFrameworkInitializationCompleted();
    }

    private IntPtr WindowMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            OpenInput();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OpenInput()
    {
        if (_window == null) return;
        if (_window.IsVisible)
        {
            _window.Hide();
            return;
        }
        _target = NativeMethods.GetForegroundWindow();
        _window.BeginInput();
    }

    private async void OnCommit(string text)
    {
        if (_window == null) return;
        _window.Hide();
        try
        {
            await TextInjector.SendAsync(_target, text);
        }
        catch (Exception ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private void CreateTrayMenu()
    {
        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Jime/Resources/tray.ico"))),
            ToolTipText = "Jime 日语输入 · Ctrl+Alt+J"
        };
        TrayIcon.SetIcons(this, new TrayIcons { tray });
        var open = new NativeMenuItem("打开输入窗口  Ctrl+Alt+J");
        var exit = new NativeMenuItem("退出");
        open.Click += OnOpenInput;
        exit.Click += OnExit;
        tray.Menu = new NativeMenu { Items = { open, exit } };
    }

    private void OnOpenInput(object? sender, EventArgs e) => OpenInput();

    private void OnExit(object? sender, EventArgs e) => _desktop?.Shutdown();

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_window != null)
        {
            var handle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            NativeMethods.UnregisterHotKey(handle, HotkeyId);
        }
        if (_window != null) Win32Properties.RemoveWndProcHookCallback(_window, WindowMessage);
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }

    private void ShowWarning(string message)
    {
        var dialog = new Window
        {
            Title = "Jime",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new Button
                    {
                        Content = "确定",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        MinWidth = 80
                    }
                }
            }
        };
        var panel = (StackPanel)dialog.Content!;
        var button = (Button)panel.Children[1];
        button.Click += (_, _) => dialog.Close();
        if (_window is { IsVisible: true }) dialog.ShowDialog(_window);
        else dialog.Show();
    }
}
