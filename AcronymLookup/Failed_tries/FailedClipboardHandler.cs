using AcronymLookup.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AcronymLookup.Failed_tries
{
    /// <summary>
    /// Safely handles clipboard operations for getting selected text
    /// SECURITY FOCUS: Protects user's clipboard data and prevents data loss 
    /// WORKFLOW FOCUS: Non-disruptive to user's normal clipboard usage
    /// </summary>
    public class FailedClipboardHandler : IDisposable
    {

        #region Windows API imports and constraints
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        //Keyboard imput constants 
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43; 

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(nint hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(nint hwnd);

        [DllImport("user32.dll")]
        private static extern nint SendMessage(nint hWnd, int Msg, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet=CharSet.Auto)]
        private static extern nint GetForegroundWindow();

        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);



        //keyboard input API 
        [DllImport("user32.dll")]
        private static extern void keybd_event(int bVk, int bScan, int dwFlags, int dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public INPUTUNION union; 
        }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT keyboard; 
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public nint dwExtraInfo; 
        }

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cpSize); 
        #endregion


        #region Events and Private Fields
        public event EventHandler<TextCapturedEventArgs>? TextCaptured;

        private readonly HwndSource _hwndSource;
        private bool _isMonitoring;
        private bool _disposed;
        private bool _waitingForCopy;
        private readonly object _lockObject = new object();

        #endregion

        #region Constructor & Destructor 
        public FailedClipboardHandler()
        {
            //Create a hidden WPF window souce for receiving clipboard messages
            var helper = new WindowInteropHelper(new Window());
            _hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
            _hwndSource.AddHook(WndProc);

            _isMonitoring = false;
            _disposed = false;
            _waitingForCopy = false; 
        }

        ~FailedClipboardHandler()
        {
            Dispose(false);
        }

        #endregion

        #region Public Methods 

        public void CaptureSelectedText()
        {
            nint hWnd = GetForegroundWindow();
            try
            {
                Logger.Log("Starting clipboard capture...");

                if (StartMonitoring())
                {
                    lock (_lockObject)
                    {
                        _waitingForCopy = true;
                    }

                    

                    //Send Ctrl+C to copy selected text 
                    Logger.Log("Sending ctrl+C...");
                    SendCtrlCAlternative();

                    Logger.Log("Waiting for clipboard change");
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to start clipboard monitoring");
                //fallback
                FallbackCaptureMethod(hWnd);
            }
        }

        public bool StartMonitoring()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FailedClipboardHandler));

            if (_isMonitoring)
            {
                Logger.Log("Clipboard monitoring already active!");
                return true;
            }

            try
            {
                bool success = AddClipboardFormatListener(_hwndSource.Handle);

                if (success)
                {
                    _isMonitoring = true;
                    Logger.Log("Clipboard monitoring started!");
                    return true;
                }
                else
                {
                    Logger.Log("Failed to start clipboard monitoring");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error starting clipboard monitoring");
                return false;
            }

        }

        public void StopMonitoring()
        {
            if(!_isMonitoring || _disposed)
            {
                return; 
            }

            try
            {
                bool success = RemoveClipboardFormatListener(_hwndSource.Handle); 
                if (success)
                {
                    _isMonitoring = false;
                    Logger.Log("Clipboard monitoring stopped"); 
                } else
                {
                    Logger.Log("Failed to stop clipboard monitoring"); 
                }
            }catch (Exception ex)
            {
                Logger.Log($"Error stopping clipboard monitoring: {ex.Message}"); 
            }
        }

        public bool isMonitoring => _isMonitoring && !_disposed;

        #endregion

        #region Message Handling 
        private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if(msg == WM_CLIPBOARDUPDATE)
            {
                Logger.Log("Clipboard Changed!"); 

                lock (_lockObject)
                {
                    if (_waitingForCopy)
                    {
                        _waitingForCopy = false; 
                        //process the clipboard change on separate thread to avoid blocking 
                        Thread.Sleep(50); 
                        ProcessClipboardChange();

                        handled = true; 
                    }
                }
            }

            return nint.Zero; 
        }

        private void ProcessClipboardChange()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string clipboardText = Clipboard.GetText();

                    if (!string.IsNullOrWhiteSpace(clipboardText))
                    {
                        string cleanedText = CleanSelectedText(clipboardText);

                        if (IsValidSelectedText(cleanedText))
                        {
                            var args = new TextCapturedEventArgs(cleanedText);
                            TextCaptured?.Invoke(this, args);
                        }
                        else
                        {
                            Logger.Log($"Invalid text capture: '{cleanedText}'");
                            var args = new TextCapturedEventArgs(null, "Selected text not valid for acronym lookup");
                            TextCaptured?.Invoke(this, args);
                        }
                    }
                    else
                    {
                        Logger.Log("Clipboard contains empty text");
                        var args = new TextCapturedEventArgs(null, "No text was selected");
                        TextCaptured?.Invoke(this, args);
                    }
                }
                else
                {
                    Logger.Log("Clipboard doesnt contain text");
                    var args = new TextCapturedEventArgs(null, "Clipboard doesnt contain text");
                    TextCaptured?.Invoke(this, args);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error processing clipboard {ex.Message}");
                var args = new TextCapturedEventArgs(null, $"Error: {ex.Message}");
                TextCaptured?.Invoke(this, args);
            }
        }
        #endregion


        #region FALLBACK 
        private void FallbackCaptureMethod(nint hWnd)
        {
            try
            {
                Logger.Log("Using fallback method with sleep");
                SendCtrlC(hWnd);
                Thread.Sleep(1000);
                if (Clipboard.ContainsText())
                {
                    string selectedText = Clipboard.GetText();

                    if (!string.IsNullOrWhiteSpace(selectedText))
                    {
                        string cleanedText = CleanSelectedText(selectedText);
                        Logger.Log($"Fallback captured: '{cleanedText}'");

                        var args = new TextCapturedEventArgs(cleanedText);
                        TextCaptured?.Invoke(this, args);
                    }
                    else
                    {
                        var args = new TextCapturedEventArgs(null, "No text was selected");
                        TextCaptured?.Invoke(this, args);
                    }
                }
                else
                {
                    var args = new TextCapturedEventArgs(null, "Nothing selected");
                    TextCaptured?.Invoke(this, args);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Fallback method failed: {ex.Message}");
                var args = new TextCapturedEventArgs(null, $"Error: {ex.Message}");
                TextCaptured?.Invoke(this, args);
            }
        }
        #endregion

        #region SEND CTRL C

        /// <summary>
        /// Sends Ctrl+C key combination to copy selected text
        /// Security: Uses standard Windows input APIs
        /// </summary>
        private static void SendCtrlC(nint hWnd)
        {
            try
            {

                SetForegroundWindow(hWnd); 
                //method 1: Try using Input simulator 

                //import necessary Windows API functions 
                const int KEYEVENTF_KEYUP = 0x0002;
                const int VK_CONTROL = 0x11;
                const int VK_C = 0x43;

                //press ctrl 
                keybd_event(VK_CONTROL, 0, 0, 0);
                //Thread.Sleep(50); //hold a bit 


                //press c
                keybd_event(VK_C, 0, 0, 0);
                //Thread.Sleep(50);


                //Small delay 
                Thread.Sleep(10);

                //Release C
                keybd_event(VK_C, 0, KEYEVENTF_KEYUP, 0);
                //Thread.Sleep(20);

                //Release Ctrl
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
                //Thread.Sleep(50);

                Logger.Log("Sent Ctrl+C");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error sending Ctrl+C: {ex.Message}");
            }
        }

        private static void SendCtrlCWithSendInput()
        {
            try
            {
                Logger.Log("Using sendinput method...");

                INPUT[] inputs = new INPUT[4];

                //press ctrl 
                inputs[0].type = 1;
                inputs[0].union.keyboard.wVk = VK_CONTROL;
                inputs[0].union.keyboard.dwFlags = 0;

                //press ctrl 
                inputs[1].type = 1;
                inputs[1].union.keyboard.wVk = VK_C;
                inputs[1].union.keyboard.dwFlags = 0;

                //press ctrl 
                inputs[2].type = 1;
                inputs[2].union.keyboard.wVk = VK_CONTROL;
                inputs[2].union.keyboard.dwFlags = 2;

                //press ctrl 
                inputs[3].type = 1;
                inputs[3].union.keyboard.wVk = VK_C;
                inputs[3].union.keyboard.dwFlags = 2;

                uint result = SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));
                Logger.Log($"SendInput result: {result}"); 

            }catch (Exception ex)
            {
                Logger.Log($"SendInput error: {ex.Message}"); 
            }
        }

        private static void SendCtrlCAlternative()
        {
            try
            {
                //get the currently focused window 
                nint hwnd = GetForegroundWindow();

                if (hwnd != nint.Zero)
                {
                    //send WM_COPY message directly 
                    const int WM_COPY = 0x0301;
                    //SendMessage(hwnd, WM_COPY, IntPtr.Zero, IntPtr.Zero);
                    //Logger.Log("Sent WM_COPY message to focused window");

                    
                    //try edit menu copy command 
                    const int WM_COMMAND = 0x0111;
                    const int EM_COPY = 0x0301;
                    SendMessage(hwnd, WM_COMMAND, EM_COPY, nint.Zero);
                    Logger.Log("Sent EM_COPY command");
                    

                }
                else
                {
                    Logger.Log("Could not get foreground window");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error sending Windows Messages: {ex.Message}");
            }
        }


        #endregion

        #region Validation Methods

        /// <summary>
        /// validates that the selected text is reasonable for abbreviation lookup 
        /// SECURITY: Prevents processing of suspicious or overly large content
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool IsValidSelectedText(string? text)
        {

            Logger.Log($"Validation Check: Current string {text}");
            if (string.IsNullOrWhiteSpace(text))
            {
                Logger.Log("Validation check 1: IsNullOrWhiteSpace - not passed");
                return false;
            }

            Logger.Log("Validation check 1 -- PASSED : String is not Null or whitespace!!!");

            //Clean the text 
            string cleaned = text.Trim();

            Logger.Log($"String after cleaning: {cleaned}");

            //Reasonable length for an abbreviation (1-50 chars) 
            if (cleaned.Length < 1 || cleaned.Length > 50)
                return false;

            Logger.Log("Validation Check 2 -- PASSED : String is of reasobable length");

            if (cleaned.Contains('\n') || cleaned.Contains('\r'))
                return false;

            Logger.Log("Validation Check 3 -- PASSED : String does not contain weird characters");

            //should not be all spaces or special characters 
            if (string.IsNullOrWhiteSpace(cleaned.Replace(".", "").Replace("-", "").Replace("_", "")))
                return false;

            Logger.Log("Validation Check 4 --PASSED: String does not contain special characters!");

            return true;
        }


        /// <summary>
        /// cleans selected text to prepare it for abbreviation lookup 
        /// SECURITY: removes potentially harmful content
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string CleanSelectedText(string text)
        {
            Logger.Log($"Cleaning String: Current String: {text}");
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            //Basic cleaning 
            string cleaned = text.Trim();

            //remove line breaks and extra whitespace 
            cleaned = cleaned.Replace('\r', ' ').Replace('\n', ' ');
            while (cleaned.Contains("  "))
            {
                cleaned = cleaned.Replace("  "," ");
            }
            return cleaned.Trim();
        }


        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); 
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!_disposed)
            {
                if (disposing)
                {
                    //Dispose managed resources 
                    StopMonitoring();
                    _hwndSource?.RemoveHook(WndProc);
                    _hwndSource?.Dispose(); 
                }
            }
        }

        #endregion

        #region EventArgs 
        public class TextCapturedEventArgs : EventArgs
        {
            public string? CapturedText { get; } 
            public string? ErrorMessage { get; }
            public bool Success => CapturedText != null; 

            public TextCapturedEventArgs(string? capturedText, string? errorMessage = null)
            {
                CapturedText = capturedText;
                ErrorMessage = errorMessage;
            }

            public override string ToString()
            {
                return Success ? $"Captured: '{CapturedText}'" : $"Failed: {ErrorMessage}"; 
            }
        }

        #endregion
    }
}
