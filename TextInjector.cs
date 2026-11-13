using System.Runtime.InteropServices;
using Clipboard = System.Windows.Clipboard;

namespace Jime;

internal static class TextInjector
{
    public static async Task SendAsync(IntPtr target, string text)
    {
        var originalClipboard = Clipboard.GetDataObject();
        await SetClipboardTextAsync(text);
        try
        {
            RestoreForeground(target);
            await Task.Delay(80);
            var inputs = new[]
            {
                Key(0x11, false),
                Key(0x56, false),
                Key(0x56, true),
                Key(0x11, true)
            };
            var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
            if (sent != inputs.Length)
                throw new InvalidOperationException($"Ctrl+V 注入失败（SendInput {sent}/4，错误 {Marshal.GetLastWin32Error()}）。");
            await Task.Delay(150);
        }
        finally
        {
            if (originalClipboard != null) Clipboard.SetDataObject(originalClipboard, true);
        }
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (Exception ex) when (ex is ExternalException or COMException)
            {
                if (attempt >= 4) throw;
                await Task.Delay(30);
            }
        }
    }

    private static void RestoreForeground(IntPtr target)
    {
        if (target == IntPtr.Zero) return;
        if (NativeMethods.IsIconic(target)) NativeMethods.ShowWindow(target, 9);
        if (NativeMethods.GetForegroundWindow() == target) return;

        var currentThread = NativeMethods.GetCurrentThreadId();
        var targetThread = NativeMethods.GetWindowThreadProcessId(target, out _);
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        var attachedToTarget = targetThread != 0 && NativeMethods.AttachThreadInput(currentThread, targetThread, true);
        var attachedToForeground = foregroundThread != 0 && NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            NativeMethods.BringWindowToTop(target);
            NativeMethods.SetForegroundWindow(target);
        }
        finally
        {
            if (attachedToTarget) NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            if (attachedToForeground) NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
        }
        if (NativeMethods.GetForegroundWindow() == target) return;

        SendAltTap();
        NativeMethods.BringWindowToTop(target);
        NativeMethods.SetForegroundWindow(target);
    }

    private static void SendAltTap()
    {
        var inputs = new[] { Key(0x12, false), Key(0x12, true) };
        NativeMethods.SendInput(2, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT Key(ushort virtualKey, bool up) => new()
    {
        type = NativeMethods.InputKeyboard,
        U = new NativeMethods.INPUTUNION
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = virtualKey,
                dwFlags = up ? NativeMethods.KeyeventfKeyup : 0
            }
        }
    };
}
