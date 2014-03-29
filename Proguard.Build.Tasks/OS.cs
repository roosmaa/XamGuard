using System;

namespace BitterFudge.Proguard.Build
{
    public static class OS
    {
        public static bool IsWindows { get; private set; }

        static OS ()
        {
            switch (Environment.OSVersion.Platform) {
            case PlatformID.Unix:
            case PlatformID.MacOSX:
                IsWindows = false;
                break;
            default:
                IsWindows = true;
                break;
            }
        }
    }
}
