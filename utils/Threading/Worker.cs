using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Keel.Utils.Threading
{
    public abstract class Worker
    {
        public readonly struct ReadOnlyConcurrentWorkerCollection : IReadOnlyCollection<Worker>
        {
            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _workers.Count;
            }

            private readonly ConcurrentDictionary<uint, Worker> _workers;

            internal ReadOnlyConcurrentWorkerCollection(ConcurrentDictionary<uint, Worker> workers) =>
                _workers = workers;

            public IEnumerator<Worker> GetEnumerator() => _workers.Values.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public static ReadOnlyConcurrentWorkerCollection All
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(s_allWorkers);
        }

        private static readonly ConcurrentDictionary<uint, Worker> s_allWorkers = new();
        private static readonly UidProvider s_uidProvider = new();

        public readonly string Name;
        public readonly uint Uid;
        public readonly bool Monitor;

        private readonly object _locker = new();
        private readonly Stopwatch _stopWatch = new();
        private readonly int _millisecondsTimeStep;

        private bool _started;
        private bool _interruptRequest;
        private bool _interrupted;
        private Thread _thread;

        private DateTime _lastLoopUpdateTime;

        protected Worker(int millisecondsTimeStep, string workerName, bool monitor)
        {
            Name = string.IsNullOrWhiteSpace(workerName) ? GetType().FullName : workerName;
            Uid = s_uidProvider.Next();

            _millisecondsTimeStep = millisecondsTimeStep > 0 ? millisecondsTimeStep : 1;
            Monitor = monitor;
        }

        public void Start(WorkerScope scope = WorkerScope.CurrentThread)
        {
            lock (_locker)
            {
                if (_started)
                    throw new InvalidOperationException();

                _started = true;
            }

            if (scope != WorkerScope.CurrentThread)
            {
                _thread = new Thread(UpdateLoop)
                {
                    Name = Name,
                    IsBackground = scope == WorkerScope.NewBackgroundThread
                };
                _thread.Start();
            }
            else
                UpdateLoop();
        }

        public TimeSpan ElapsedSinceLastUpdate()
        {
            lock (_locker)
                return DateTime.UtcNow - _lastLoopUpdateTime;
        }

        public void Stop()
        {
            lock (_locker)
            {
                if (_interrupted)
                    throw new InvalidOperationException("Already interrupted.");

                _interruptRequest = true;
            }
        }

        private void UpdateLoop()
        {
            try
            {
                lock (_locker)
                    _lastLoopUpdateTime = DateTime.UtcNow;

                s_allWorkers.TryAdd(Uid, value: this);
                OnStart();

                while (true)
                {
                    lock (_locker)
                    {
                        _lastLoopUpdateTime = DateTime.UtcNow;
                        if (_interruptRequest)
                            break;
                    }

                    _stopWatch.Restart();
                    OnUpdate();
                    _stopWatch.Stop();

                    if (Monitor && _stopWatch.ElapsedMilliseconds > _millisecondsTimeStep)
                        Debug.Logger.LogWarning($"[{GetType().FullName} - {Uid}] Update call took longer '{_stopWatch.ElapsedMilliseconds}ms' than expected '{_millisecondsTimeStep}ms'.");

                    Thread.Sleep(millisecondsTimeout: _millisecondsTimeStep);
                }

                lock (_locker)
                {
                    _interruptRequest = false;
                    _interrupted = true;
                }
            }
            catch (Exception e)
            {
                Debug.Logger.LogException(e);
            }
            finally
            {
                _thread = null;
                s_allWorkers.TryRemove(Uid, out _);
                OnStop();
            }
        }

        protected virtual void OnStart() { }
        protected virtual void OnUpdate() { }
        protected virtual void OnStop() { }
    }
}