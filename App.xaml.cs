using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;

namespace Jime;

public partial class App : Application
{
    private const int HotkeyId = 1;
    private const int WmChar = 0x0102;
    private Mutex? _mutex;
    private MainWindow? _window;
    private HwndSource? _source;
    private System.Windows.Forms.NotifyIcon? _tray;
    private IntPtr _target;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, @"Global\Jime.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _window = new MainWindow();
        _window.CommitRequested += OnCommit;
        _window.CancelRequested += () => _window.Hide();
        CreateTray();
        _window.Opacity = 0;
        _window.Show();
        _window.Hide();
        _window.Opacity = 1;

        var helper = new WindowInteropHelper(_window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source.AddHook(WindowMessage);
        if (!NativeMethods.RegisterHotKey(helper.Handle, HotkeyId,
                NativeMethods.ModControl | NativeMethods.ModAlt | NativeMethods.ModNoRepeat, 0x4A))
        {
            System.Windows.MessageBox.Show("无法注册 Ctrl+Alt+J，快捷键可能被其他程序占用。", "Jime",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        if (e.Args.Contains("--preview")) _window.BeginInput();
    }

    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmChar && _window is { IsVisible: true }
            && _window.TryInsertCharacter((char)wParam.ToInt32()))
        {
            handled = true;
        }
        else if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
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
            System.Windows.MessageBox.Show(ex.Message, "Jime", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CreateTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开输入窗口  Ctrl+Alt+J", null, (_, _) => Dispatcher.Invoke(OpenInput));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Shutdown));
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "Jime 日语输入 · Ctrl+Alt+J",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName),
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenInput);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_source != null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _source.RemoveHook(WindowMessage);
        }
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
