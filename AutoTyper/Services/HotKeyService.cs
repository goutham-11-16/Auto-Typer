using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace AutoTyper.Services
{
    public class HotKeyService : IDisposable
    {
        private IntPtr _windowHandle;
        private HwndSource _source;
        private readonly Dictionary<int, Action> _registry = new Dictionary<int, Action>();
        
        // ID counter for hotkeys. Start high to avoid system conflicts.
        private int _currentId = 9000;

        public bool IsPaused { get; set; } = false;
        public event EventHandler<bool> PausedChanged;

        // Pause Hotkey ID
        private const int PAUSE_HOTKEY_ID = 8999;

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(HwndHook);
            
            // Register Global Pause Hotkey (Ctrl + Alt + P)
            // Need to catch failure here separately but for now we try best effort
            RegisterHotKeyInternal(PAUSE_HOTKEY_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.P));
        }

        public bool Register(ModifierKeys modifiers, Key key, Action updateCallback)
        {
            int id = _currentId++;
            uint fsModifiers = 0;
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) fsModifiers |= NativeMethods.MOD_ALT;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control) fsModifiers |= NativeMethods.MOD_CONTROL;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) fsModifiers |= NativeMethods.MOD_SHIFT;
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) fsModifiers |= NativeMethods.MOD_WIN;

            // Make sure we don't repeat (optional)
            fsModifiers |= NativeMethods.MOD_NOREPEAT;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

            bool result = RegisterHotKeyInternal(id, fsModifiers, vk);
            if (result)
            {
                _registry[id] = updateCallback;
            }
            return result;
        }

        private bool RegisterHotKeyInternal(int id, uint fsModifiers, uint vk)
        {
            return NativeMethods.RegisterHotKey(_windowHandle, id, fsModifiers, vk);
        }

        public void UnregisterAll()
        {
            foreach (var id in _registry.Keys)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
            }
            _registry.Clear();
            _currentId = 9000;
            
            // Unregister Pause Hotkey
            NativeMethods.UnregisterHotKey(_windowHandle, PAUSE_HOTKEY_ID);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                
                if (id == PAUSE_HOTKEY_ID)
                {
                    TogglePause();
                    handled = true;
                }
                else if (_registry.ContainsKey(id))
                {
                    if (!IsPaused)
                    {
                        var action = _registry[id];
                        action?.Invoke();
                    }
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void TogglePause()
        {
            IsPaused = !IsPaused;
            PausedChanged?.Invoke(this, IsPaused);
        }

        public void Dispose()
        {
            UnregisterAll();
            _source?.RemoveHook(HwndHook);
        }
    }
}
