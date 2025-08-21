using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace AcronymLookup.Utilities
{
    public static class Logger
    {
        public static void Log(string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string member = "")
        {
            Console.WriteLine($"[{Path.GetFileName(file)}:{line} - {member}] {message}");
        }
    }
}