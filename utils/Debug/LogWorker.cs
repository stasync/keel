using Keel.Utils.Threading;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Keel.Utils.Debug
{
    public sealed class LogWorker : Worker, ILogOutput
    {
        private enum LogType
        {
            Info,
            Warning,
            Error,
            Exception
        }

        private readonly struct ScheduledLog
        {
            public readonly string Message;
            public readonly LogType Type;
            public readonly ConsoleColor Color;
            public readonly string ThreadInfo;

            public ScheduledLog(string message, LogType type, ConsoleColor color, string threadInfo)
            {
                Message = message;
                Type = type;
                Color = color;
                ThreadInfo = threadInfo;
            }
        }

        public event Action Stopped = delegate { };

        private const int FLUSH_INTERVAL_MS = 1000; // flush at least every second
        private const int FLUSH_BUFFER_SIZE = 64 * 1024; // flush when buffer reaches ~64KB

        private readonly ConcurrentQueue<ScheduledLog> _logQueue = new();
        private readonly StreamWriter _fileWriter;
        private readonly StringBuilder _fileBuffer = new();
        private readonly StringBuilder _logQueueBuffer = new();
        private DateTime _lastFlushTime = DateTime.MinValue;

        private LogWorker(string outputRelativePath) : base(millisecondsTimeStep: 10, workerName: null, monitor: false)
        {
            if (!string.IsNullOrWhiteSpace(outputRelativePath))
            {
                try
                {
                    // Ensure logs directory exists under the application base directory.
                    var fullLogOutputPath = Path.Combine(DomainUtils.DomainDirectory, outputRelativePath);
                    Directory.CreateDirectory(fullLogOutputPath);

                    // Use a per-process log file name to avoid clashes when multiple instances run.
                    var currentProcess = Process.GetCurrentProcess();
                    var logPath = Path.Combine(fullLogOutputPath, $"app_{currentProcess.Id}.log");

                    // FileStream with a large buffer and async option; wrap in BufferedStream and StreamWriter for efficient writes.
                    var fileStream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, bufferSize: ushort.MaxValue, useAsync: true);
                    var buffered = new BufferedStream(fileStream, bufferSize: ushort.MaxValue);
                    _fileWriter = new StreamWriter(buffered, Encoding.UTF8) { AutoFlush = false };

                    _lastFlushTime = DateTime.UtcNow;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{GetType().FullName}: unable to initialize log file output due to exception:");
                    Console.WriteLine(e);
                    // If file logging cannot be initialized, fall back to console-only logging.
                    // _fileWriter remains null and file logging will be skipped.
                    // We intentionally swallow exceptions here to keep logging robust.
                    _fileWriter = null;
                }
            }
        }

        public static LogWorker BuildAndStart(WorkerScope scope, string outputRelativePath = null)
        {
            var newWorker = new LogWorker(outputRelativePath);
            newWorker.Start(scope);
            return newWorker;
        }

        public void Error(string msg) =>
            ScheduleLog(msg, LogType.Error, ConsoleColor.DarkRed);

        public void Exception(Exception e) =>
            ScheduleLog(msg: e.ToString(), LogType.Exception, ConsoleColor.Red);

        public void Exception(string msg) =>
            ScheduleLog(msg, LogType.Exception, ConsoleColor.Red);

        public void Info(string msg) =>
            ScheduleLog(msg, LogType.Info, ConsoleColor.Gray);

        public void Warning(string msg) =>
            ScheduleLog(msg, LogType.Warning, ConsoleColor.DarkYellow);

        private void ScheduleLog(string msg, LogType type, ConsoleColor color)
        {
            var currentThread = System.Threading.Thread.CurrentThread;
            var currentThreadName = currentThread.Name;
            if (string.IsNullOrWhiteSpace(currentThreadName))
                currentThreadName = "Thread";

            currentThreadName = $"{currentThreadName}-#{currentThread.ManagedThreadId}";
            _logQueue.Enqueue(new ScheduledLog(message: msg, type, color, currentThreadName));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            FlushLogQueue();

            // If the file writer is available, and we have data - append and possibly flush.
            // Null indicates that file logging is disabled.
            if (_fileWriter != null)
            {
                try
                {
                    if (_logQueueBuffer.Length > 0)
                        _fileBuffer.Append(_logQueueBuffer);

                    var utcNow = DateTime.UtcNow;
                    var timeSinceLastFlush = (utcNow - _lastFlushTime).TotalMilliseconds;

                    if (_fileBuffer.Length >= FLUSH_BUFFER_SIZE || timeSinceLastFlush >= FLUSH_INTERVAL_MS)
                    {
                        _fileWriter.Write(_fileBuffer.ToString());
                        _fileWriter.Flush();
                        _fileBuffer.Clear();

                        _lastFlushTime = utcNow;
                    }
                }
                catch (Exception ex)
                {
                    // If file writing fails, report to console and stop using file writer to avoid repeated failures.
                    var originalForegroundColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{GetType().FullName}: failed to write logs to file: {ex.Message}");
                    Console.ForegroundColor = originalForegroundColor;

                    try
                    {
                        _fileWriter.Dispose();
                    }
                    catch
                    {
                        // Swallow exceptions during dispose.
                    }
                }
            }
        }

        private void FlushLogQueue()
        {
            _logQueueBuffer.Clear();
            while (_logQueue.TryDequeue(out var logData))
            {
                // Console output.
                var originalForegroundColor = Console.ForegroundColor;
                Console.ForegroundColor = logData.Color;
                Console.WriteLine($"[{logData.ThreadInfo}] {logData.Message}");
                Console.ForegroundColor = originalForegroundColor;

                // Append timestamped line to local buffer for file output.
                _logQueueBuffer.Append('[').Append(logData.ThreadInfo).Append("] ");
                _logQueueBuffer.Append('[').Append(DateTime.UtcNow.ToString(format: "o")).Append("] ");
                _logQueueBuffer.Append('[').Append(logData.Type).Append("] ");
                _logQueueBuffer.Append(logData.Message).Append('\n');
            }
        }

        protected override void OnStop()
        {
            base.OnStop();

            try
            {
                if (_fileWriter == null)
                    return;

                FlushLogQueue();
                if (_logQueueBuffer.Length > 0)
                    _fileBuffer.Append(_logQueueBuffer);

                // Write any remaining buffer.
                if (_fileBuffer.Length > 0)
                    _fileWriter.Write(_fileBuffer.ToString());

                _fileWriter.Flush();
                _fileWriter.Dispose();
            }
            catch (Exception)
            {
                // Swallow exceptions during shutdown.
            }
            finally
            {
                Stopped();
            }
        }
    }
}