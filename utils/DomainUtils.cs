using System;

namespace Keel.Utils
{
    public enum EnvironmentPlatform
    {
        Unknown,
        Windows,
        Unix
    }

    public static class DomainUtils
    {
        public static readonly EnvironmentPlatform Platform;
        public static readonly string DomainDirectory;

        static DomainUtils()
        {
            Platform = GetCurrentPlatformInternal();
            DomainDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return;

            static EnvironmentPlatform GetCurrentPlatformInternal()
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return EnvironmentPlatform.Windows;

                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                    return EnvironmentPlatform.Unix;

                return EnvironmentPlatform.Unknown;
            }
        }
    }
}