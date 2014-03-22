using System;
using System.IO;

namespace BitterFudge.Proguard.Build
{
    public static class OS
    {
        public static bool IsWindows { get; private set; }

        static OS ()
        {
            IsWindows = Path.PathSeparator == '\\';
        }
    }
}

