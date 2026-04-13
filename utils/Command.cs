using Core.Utils.Debug;
using System;
using System.Diagnostics;

namespace Core.Utils
{
    public static class Command
    {
        public static string ExecuteProcessList() =>
            ExecuteAndWaitForResult(DomainUtils.Platform == EnvironmentPlatform.Unix ? "ps aux" : "tasklist");

        public static void StartProcessFromCurrentDirectory(string processName, params string[] args) =>
            StartProcessFromCurrentDirectory(processName, processName, args);

        public static void StartProcessFromCurrentDirectory(string processName, string title, params string[] args) =>
            StartProcess(DomainUtils.DomainDirectory, processName, title, args);

        public static void StartProcess(string processPath, string processName, params string[] args) =>
            StartProcess(processPath, processName, processName, args);

        /// <summary>
        /// TODO: check file extension in <param name="processPath"></param>
        /// </summary>
        public static void StartProcess(string processPath, string processName, string title, params string[] args)
        {
            var arguments = string.Join(" ", args);
            var fullProcessPath = $"{processPath}{processName}";

            var cmd = DomainUtils.Platform == EnvironmentPlatform.Unix ?
                $"cd / && screen -dmS {title} dotnet {fullProcessPath}.dll {arguments}" :
                $"start \"{title}\" {fullProcessPath}.exe {arguments}";

            Execute(cmd);
        }

        public static void Execute(string cmd) => ExecuteInternal(cmd);

        public static string ExecuteAndWaitForResult(string cmd)
        {
            var process = ExecuteInternal(cmd);
            var result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

        private static Process ExecuteInternal(string cmd)
        {
            var startInfo = GetPlatformBasedStartInfo(cmd);
            if (startInfo == null)
                throw new InvalidOperationException();

            Logger.LogWarning($"execute: {startInfo.FileName} {startInfo.Arguments}");
            return Process.Start(startInfo);
        }

        private static ProcessStartInfo GetPlatformBasedStartInfo(string cmd) =>
            DomainUtils.Platform switch
            {
                EnvironmentPlatform.Windows => WindowsProcessInfo(cmd),
                EnvironmentPlatform.Unix => UnixProcessInfo(cmd),
                _ => null,
            };

        private static ProcessStartInfo WindowsProcessInfo(string cmd) =>
            new()
            {
                FileName = "cmd.exe",
                Arguments = $"/c {cmd}",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

        private static ProcessStartInfo UnixProcessInfo(string cmd) =>
            new()
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{cmd.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
    }
}