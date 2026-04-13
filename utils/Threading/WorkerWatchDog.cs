using System;
using System.Diagnostics;
using System.IO;

namespace Core.Utils.Threading
{
    public sealed class WorkerWatchDog : Worker
    {
        private readonly float _secondsTimeStep;
        private readonly string _logWritePath;

        public WorkerWatchDog(float secondsTimeStep) : base(millisecondsTimeStep: (int)(secondsTimeStep * 1000), workerName: null, monitor: false)
        {
            _secondsTimeStep = secondsTimeStep;

            var currentProcess = Process.GetCurrentProcess();
            var outputDirectory = Path.Combine(DomainUtils.DomainDirectory, "logs");
            Directory.CreateDirectory(outputDirectory);

            _logWritePath = Path.Combine(outputDirectory, $"{currentProcess.Id}_watch_dog_log.txt");
        }

        protected override void OnStart()
        {
            base.OnStart();
            WriteLogToFile("*** WATCH DOG STARTUP ***");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            foreach (var worker in All)
            {
                if (!worker.Monitor)
                    continue;

                var elapsed = worker.ElapsedSinceLastUpdate();
                if (elapsed.TotalSeconds > _secondsTimeStep)
                    WriteLogToFile($"WORKER WARNING: Worker '{worker.Name}' has potential dead lock, elapsed {elapsed.TotalMilliseconds}ms");
            }
        }

        private void WriteLogToFile(string stringToWrite)
        {
            stringToWrite = $"[{DateTime.UtcNow}] {stringToWrite}";

            var originalForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(stringToWrite);
            Console.ForegroundColor = originalForegroundColor;

            using var writer = File.AppendText(_logWritePath);
            writer.WriteLine(stringToWrite);
        }
    }
}