using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace OneExtend.Addin
{
    /// <summary>
    /// Registers Windows global hotkeys on a hidden message-only window and
    /// dispatches them on the calling (OneNote main, STA) thread. This is the
    /// guaranteed entry channel; ribbon injection is an additional one (M0 spike).
    /// </summary>
    public sealed class HotkeyService : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint ModAlt = 0x1;
        public const uint ModControl = 0x2;
        public const uint ModShift = 0x4;

        private const int WmHotkey = 0x0312;

        private readonly HwndSource _source;
        private readonly Action<int> _onHotkey;

        public HotkeyService(Action<int> onHotkey)
        {
            _onHotkey = onHotkey ?? throw new ArgumentNullException(nameof(onHotkey));
            _source = new HwndSource(new HwndSourceParameters("OneExtendMessageWindow"));
            _source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey)
            {
                var id = wParam.ToInt32();
                try
                {
                    _onHotkey(id);
                }
                catch
                {
                    // Command exceptions are logged by their own handlers.
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>Registers a hotkey; returns false when the combination is taken.</summary>
        public bool Register(int id, uint modifiers, uint virtualKey)
        {
            return RegisterHotKey(_source.Handle, id, modifiers, virtualKey);
        }

        public void Dispose()
        {
            if (_source != null && !_source.IsDisposed)
            {
                // UnregisterHotKey per id is handled by the OS when the window dies,
                // but be explicit for all ids we own (1..9).
                for (var id = 1; id < 10; id++)
                    UnregisterHotKey(_source.Handle, id);
                _source.RemoveHook(WndProc);
                _source.Dispose();
            }
        }
    }
}
