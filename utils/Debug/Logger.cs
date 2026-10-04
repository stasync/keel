using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Keel.Utils.Debug
{
    public static class Logger
    {
        public static event Action<LogLevel, string> LogReceivedThreaded = delegate { };

        public static LogLevel LogLevel { get; private set; } = LogLevel.All;

        private static readonly object s_locker = new();
        private static ILogOutput s_baseOutput = new DummyOutput();

        public static void SetCustomOutput(ILogOutput output)
        {
            lock (s_locker)
                s_baseOutput = output ?? throw new ArgumentNullException(nameof(output));
        }

        public static void SetLogLevel(LogLevel logLevel)
        {
            lock (s_locker)
                LogLevel = logLevel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogInfo(string msg) =>
            ValidateAndOutput(LogLevel.All, s_baseOutput.Info, msg);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string msg) =>
            ValidateAndOutput(LogLevel.Warning, s_baseOutput.Warning, msg);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(string msg) =>
            ValidateAndOutput(LogLevel.Error, s_baseOutput.Error, msg);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception e) =>
            ValidateAndOutput(LogLevel.Exception, s_baseOutput.Exception, message: e.ToString());

        private static void ValidateAndOutput(LogLevel level, Action<string> printer, string message)
        {
            lock (s_locker)
            {
                if (LogLevel <= level)
                    printer(message);

                LogReceivedThreaded(level, message);
            }
        }

        /// <summary>
        /// Default implementation - no logs.
        /// </summary>
        private sealed class DummyOutput : ILogOutput
        {
            public void Error(string msg) { }
            public void Exception(Exception e) { }
            public void Exception(string msg) { }
            public void Info(string msg) { }
            public void Warning(string msg) { }
        }

        /// <summary>
        /// An output that implements queue.
        /// The queue could be flushed over to any other <see cref="ILogOutput"/> via <see cref="FlushTo"/>.
        /// </summary>
        public sealed class QueueOutput : ILogOutput
        {
            private readonly ConcurrentQueue<LogEntry> _logQueue = new();

            private struct LogEntry
            {
                internal readonly LogType LogType;
                internal readonly string Message;

                internal LogEntry(string message, LogType logType)
                {
                    Message = message;
                    LogType = logType;
                }
            }

            private enum LogType
            {
                Error,
                Exception,
                Warning,
                Info
            }

            public void FlushTo(ILogOutput otherOutput)
            {
                while (_logQueue.TryDequeue(out var logEntry))
                {
                    var message = $"[DEFERRED] {logEntry.Message}";
                    switch (logEntry.LogType)
                    {
                        case LogType.Error:
                            otherOutput.Error(message);
                            break;
                        case LogType.Exception:
                            otherOutput.Exception(message);
                            break;
                        case LogType.Warning:
                            otherOutput.Warning(message);
                            break;
                        case LogType.Info:
                            otherOutput.Info(message);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            public void Error(string msg) =>
                _logQueue.Enqueue(new LogEntry(msg, LogType.Error));

            public void Exception(Exception e) =>
                _logQueue.Enqueue(new LogEntry(message: e.ToString(), LogType.Exception));

            public void Exception(string msg) =>
                _logQueue.Enqueue(new LogEntry(msg, LogType.Exception));

            public void Info(string msg) =>
                _logQueue.Enqueue(new LogEntry(msg, LogType.Info));

            public void Warning(string msg) =>
                _logQueue.Enqueue(new LogEntry(msg, LogType.Warning));
        }
    }
}