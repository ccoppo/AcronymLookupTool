using AcronymLookup.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace AcronymLookup.Services
{
    internal class ClipboardHandler : IDisposable 
    {
        #region Windows API Constants and Imports 
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        #endregion

        #region Events and Private Fields

        public event EventHandler<TextCapturedEventArgs>? TextCaptured;

        private readonly HwndSource _hwndSource;
        private bool _isMonitoring;
        private bool _disposed;
        #endregion

        #region Constructor & Destructor 
        public ClipboardHandler()
        {
            //Create a hidden WPF window souce for receiving clipboard messages
            var helper = new WindowInteropHelper(new System.Windows.Window());
            _hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
            //_hwndSource.AddHook(WndProc);

            _isMonitoring = false;
            _disposed = false;
        }

        ~ClipboardHandler()
        {
            Dispose(false);
        }

        #endregion

        #region Process Clipboard 

        public void ProcessClipboardText()
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
                Logger.Log("Validation check 1: FAILED IsNullOrWhiteSpace");
                return false;
            }

            //Clean the text 
            string cleaned = text.Trim();

            //Reasonable length for an abbreviation (1-50 chars) 
            if (cleaned.Length < 1 || cleaned.Length > 50)
                return false;


            if (cleaned.Contains('\n') || cleaned.Contains('\r'))
                return false;


            //should not be all spaces or special characters 
            if (string.IsNullOrWhiteSpace(cleaned.Replace(".", "").Replace("-", "").Replace("_", "")))
                return false;


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
                cleaned = cleaned.Replace("  ", " ");
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
            if (!_disposed)
            {
                if (disposing)
                {
                    //Dispose managed resources 
                    //StopMonitoring();
                    //_hwndSource?.RemoveHook(WndProc);
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
