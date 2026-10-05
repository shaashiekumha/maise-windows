using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Maise.App;

public class HotKeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _currentId;
    private bool _disposed;

    public HotKeyManager(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd) ?? throw new InvalidOperationException("Could not create HwndSource for window.");
        _source.AddHook(HwndHook);
    }

    public int Register(uint modifiers, uint key, Action callback)
    {
        var id = ++_currentId;
        if (RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, key))
        {
            _hotkeyActions[id] = callback;
            return id;
        }
        return -1;
    }

    public void Unregister(int id)
    {
        if (_hotkeyActions.Remove(id))
        {
            UnregisterHotKey(_hwnd, id);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var id in _hotkeyActions.Keys.ToList())
            {
                UnregisterHotKey(_hwnd, id);
            }
            _hotkeyActions.Clear();
            _source.RemoveHook(HwndHook);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
