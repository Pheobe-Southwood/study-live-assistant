using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.App.Infrastructure;

public sealed class GlobalHotkeyService(Window owner) : IHotkeyService
{
    private const int WmHotkey = 0x0312;
    private readonly Dictionary<int, HotkeyAction> _actions = [];
    private HwndSource? _source;
    private IntPtr _handle;

    public event EventHandler<HotkeyAction>? Triggered;

    public void Attach()
    {
        _handle = new WindowInteropHelper(owner).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
    }

    public IReadOnlyList<string> Register(IEnumerable<HotkeyBinding> bindings)
    {
        UnregisterAll();
        var errors = new List<string>();
        var seen = new HashSet<(uint, uint)>();
        var id = 4100;
        foreach (var binding in bindings)
        {
            if (!seen.Add((binding.Modifiers, binding.VirtualKey)))
            {
                errors.Add($"{binding.DisplayText} 在应用内重复。");
                continue;
            }
            if (!NativeMethods.RegisterHotKey(_handle, id, binding.Modifiers, binding.VirtualKey))
            {
                errors.Add($"{binding.DisplayText} 已被其他程序占用。");
                continue;
            }
            _actions[id] = binding.Action;
            id++;
        }
        return errors;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys) NativeMethods.UnregisterHotKey(_handle, id);
        _actions.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            Triggered?.Invoke(this, action);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UnregisterHotKey(IntPtr hwnd, int id);
    }
}
