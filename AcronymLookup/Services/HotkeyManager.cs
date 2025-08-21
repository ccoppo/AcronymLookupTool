using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading; 
using AcronymLookup.Utilities; 

namespace AcronymLookup.Services
{

    /// <summary>
    /// Manages global hotkeys for the application 
    /// SECURITY: Uses minimal Windows API calls, no admin rights required
    /// </summary>
    public class HotkeyManager : IDisposable
    {
        #region Windows API Constants 

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_LOOKUP = 9000; //Unique ID for lookup hotkey

        private const int VK_L = 0x4C; //Virtual key code for L

        #endregion

        #region Windows API Imports 

        /// <summary>
        /// Registers a global hotkey with Windows
        /// SECURITY: Standard Windows API, no elevation required
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="id"></param>
        /// <param name="fsModifiers"></param>
        /// <param name="vk"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        /// <summary>
        /// Unregisters a global  hotkey
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotkey(IntPtr hWnd, int id);

        #endregion

        #region Modifier Key Flags 

        [Flags]
        public enum ModifierKeys : uint
        {
            None = 0,
            Alt = 1,
            Control = 2,
            Shift = 4,
            Win = 8
        }

        #endregion

        #region Events 

        /// <summary> 
        /// Fired when Ctrl+Alt+L is pressed anywhere in Windows 
        /// </summary>
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

        #endregion

        #region Private Fields 

        private readonly HwndSource _hwndSource;
        private bool _isRegistered;
        private bool _disposed;

        #endregion

        #region Constructor and Destructor 

        /// <summary>
        /// Creates a new hotkey manager using WPF's message handling 
        /// </summary>
        public HotkeyManager()
        {
            var helper = new WindowInteropHelper(new System.Windows.Window());
            _hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
            _hwndSource.AddHook(WndProc);

            _isRegistered = false;
            _disposed = false;
        }

        /// <summary> 
        /// Finalizer to ensure cleanup 
        /// </summary>
        ~HotkeyManager()
        {
            Dispose(false);
        }

        #endregion

        #region Public Methods 

        /// <summary>
        /// Registers the Ctrl + Alt + L hotkey globally 
        /// Security: Only registers our specific hotkey
        /// </summary>
        /// <returns>True if regisration succeeded, false if hotkey is already in use</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public bool RegisterHotkey()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HotkeyManager));

            if (_isRegistered)
            {
                Logger.Log("Hotkey Already Registered!!!");
                return true;
            }

            try
            {
                //Register Ctrl+Alt+L
                bool success = RegisterHotKey(
                    _hwndSource.Handle,
                    HOTKEY_ID_LOOKUP,
                    (uint)(ModifierKeys.Control | ModifierKeys.Alt),
                    VK_L //'L' key
                );

                if (success)
                {
                    _isRegistered = true;
                    Logger.Log("Hotkey registered: Ctrl+Alt+L");
                    Logger.Log("    Press Ctrl+Alt+L anywhere to trigger lookup!");
                    return true;
                }
                else
                {
                    Logger.Log("Failed to register Ctrl+Alt+L");
                    Logger.Log("    This Hotkey may already be in use by another application");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error registering hotkey: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unregisters the hotkey
        /// </summary>
        public void UnregisterHotkey()
        {
            if (!_isRegistered || _disposed)
                return;

            try
            {
                bool success = UnregisterHotkey(_hwndSource.Handle, HOTKEY_ID_LOOKUP);
                if (success)
                {
                    _isRegistered = false;
                    Logger.Log("Hotkey unregistered!");
                } else
                {
                    Logger.Log("Failed to unregister hotkey");
                }
            } catch (Exception ex)
            {
                Logger.Log($"Error unregistering hotkey: {ex.Message}");
            }
        }

        public bool IsRegistered => _isRegistered && !_disposed;

        #endregion

        #region Message Handling

        /// <summary>
        /// Processes Windows messages for our hotkey
        /// Security: Only processes WM_HOTKEY messages, ignores everything else
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                //get hotkey info 
                int hotkeyId = wParam.ToInt32();
                int modifiers = (int)lParam & 0xFFFF;
                int virtualKey = ((int)lParam >> 16) & 0xFFFF;

                //Only respond to our specific hotkey 
                if (hotkeyId == HOTKEY_ID_LOOKUP)
                {
                    Logger.Log("!!! Ctrl+Alt+L pressed!");

                    var args = new HotkeyPressedEventArgs(hotkeyId, modifiers, virtualKey);
                    HotkeyPressed?.Invoke(this, args);

                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        #endregion

        #region IDisposable Implementation 

        /// <summary>
        /// Disposes the hotkey manager and cleans up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    //Dispose managed resources 
                    UnregisterHotkey();
                    _hwndSource?.RemoveHook(WndProc);
                    _hwndSource?.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Event arguments for hotkey press events
    /// </summary>
    public class HotkeyPressedEventArgs : EventArgs
    {
        public int HotkeyId { get; }
        public int Modifiers { get; }
        public int VirtualKey { get; }

        public HotkeyPressedEventArgs(int hotkeyId, int modifiers, int virtualKey)
        {
            HotkeyId = hotkeyId;
            Modifiers = modifiers;
            VirtualKey = virtualKey;
        }

        public override string ToString()
        {
            return $"Hotkey {HotkeyId}: Modifiers={Modifiers}, Key={VirtualKey}";
        }
    }

    #endregion

}
