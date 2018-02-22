using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace SimRing {
    public sealed class Sim : IEnv {

        uint _rand;
        long _time;
        long _lastDebug;
        long _steps;
        Exception _halt;
        long _networkOutageTill;

        readonly SortedList<long, object> _future = new SortedList<long, object>();
        readonly Dictionary<int, long> _db = new Dictionary<int, long>();


        public decimal MessageCopyProbability = 0;
        public decimal MessageLossProbability = 0;
        public decimal MessageDelayProbability = 0;
        public decimal NetworkFailureProbability = 0;
        public decimal NetworkOutageProbability = 0;
        public decimal StorageFailureProbability = 0;
        public decimal StorageFreezeProbability = 0;
        public decimal ActorFreezeProbability = 0;
        public uint Seed = 0;
        public TimeSpan MaxExecutionTime = TimeSpan.FromSeconds(1);
        public bool PrintDebug;

        static void Print(string arg, decimal value) {
            if (value > 0) {
                Console.WriteLine("  {0,-30} = {1}", arg, value);
            }
        }

        public void Debug(string message) {
            if (PrintDebug) {
                var diff = _time - _lastDebug;
                _lastDebug = _time;
                Console.WriteLine("+ {0:0000} ms: {1}", TimeSpan.FromTicks(diff).TotalMilliseconds, message);
            }
        }

        uint NextUint() {
            _rand ^= _rand << 21;
            _rand ^= _rand >> 35;
            _rand ^= _rand << 4;
            return _rand;
        }

        uint NextUint(uint max) {
            return NextUint() % max;
        }


        bool Happens(decimal value, string name) {
            if (value == 0) {
                return false;
            }

            var result = NextUint(10000);
            if (result >= (value * 10000)) {
                return false;
            }

            Debug(name.Replace("Probability", ""));
            return true;
        }

        public void Schedule(TimeSpan offset, object message) {
            _steps++;
            var pos = _time + offset.Ticks;

            while (_future.ContainsKey(pos)) {
                pos++;
            }

            _future.Add(pos, message);
        }

        public void Run(List<Actor> actors) {

            var seed = Seed;
            if (seed == 0) {
                seed = (uint) new Random().Next();
            }

            _rand = seed;
            _halt = null;
            var scheduler = new SimScheduler(this);
            var factory = new TaskFactory(scheduler);

            var watch = Stopwatch.StartNew();
            var reason = "none";
            try {
                var step = 0;
                while (true) {
                    step++;
                    if (watch.Elapsed > MaxExecutionTime) {
                        reason = "done";
                        break;
                    }

                    var hasFuture = TryGetFuture(out var o);
                    if (!hasFuture) {
                        reason = "died";
                        break;
                    }

                    switch (o) {
                        // high-level message for now
                        case DeliverMessage dm:

                            factory.StartNew(() => {
                                actors[dm.Recipient]
                                    .DispatchMessage(dm.Body)
                                    .ContinueWith(t => {
                                        if (t.Exception != null) {
                                            _halt = t.Exception.InnerException;
                                        }
                                    });
                            });
                            break;
                        case Task t:
                            scheduler.Execute(t);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                    if (_halt != null) {
                        reason = "halt";
                        break;
                    }
                }

            } catch (Exception ex) {
                reason = "fatal";
                _halt = ex;
                Console.WriteLine("Fatal: " + ex);
            }

            watch.Stop();

            var softTime = TimeSpan.FromTicks(_time);
            var factor = softTime.TotalHours / watch.Elapsed.TotalHours;

            Console.WriteLine("Simulation parameters:");
            Console.WriteLine("  {0,-30} = {1}", "Rand seed", seed);
            Print(nameof(MessageCopyProbability), MessageCopyProbability);
            Print(nameof(MessageDelayProbability), MessageDelayProbability);
            Print(nameof(NetworkFailureProbability), NetworkFailureProbability);
            Print(nameof(MessageLossProbability), MessageLossProbability);
            Print(nameof(StorageFailureProbability), StorageFailureProbability);
            Print(nameof(ActorFreezeProbability), ActorFreezeProbability);
            Print(nameof(NetworkOutageProbability), NetworkOutageProbability);
            Console.WriteLine($"Result: {reason.ToUpper()}");

            if (_halt != null) {
                Console.WriteLine(_halt);
            }

            Console.WriteLine($"Simulated {softTime.TotalHours:F1} hours in {_steps} steps.");
            Console.WriteLine($"Took {watch.Elapsed.TotalSeconds:F1} seconds of real time (x{factor:F0} speed-up)");
        }

        long _freezeId;

        async Task ActorFreeze() {
            if (Happens(ActorFreezeProbability, nameof(ActorFreezeProbability))) {
                var freeze = _freezeId++;
                Debug($"Freeze {freeze} start");
                await Delay(TimeSpan.FromSeconds(5));
                Debug($"Freeze {freeze} over");
            }
        }

        public Task Delay(TimeSpan ts) {
            var task = new FutureTask(ts);
            task.Start();
            return task;
        }

        Task IntranetRoundTrips(int count) {
            // roundrip is 150ms
            var ms = count * 10;
            return Delay(TimeSpan.FromMilliseconds(ms + NextUint((uint) ms)));
        }



        public async Task Send(int recipient, object message) {
            await ActorFreeze();
            // failure could be while sending or while getting back

            await IntranetRoundTrips(1);

            if (Happens(MessageDelayProbability, nameof(MessageDelayProbability))) {
                await IntranetRoundTrips(5);
            }

            if (_time < _networkOutageTill) {
                throw new IOException("network outage");
            }

            if (Happens(NetworkOutageProbability, nameof(NetworkOutageProbability))) {
                _networkOutageTill = _time + TimeSpan.FromSeconds(20 + NextUint(60)).Ticks;
            }


            bool networkFailure = Happens(NetworkFailureProbability, nameof(NetworkFailureProbability));

            bool delivered = NextUint(2) == 0;
            if (!networkFailure || delivered) {
                var delayMs = 5 + NextUint(17);

                Schedule(TimeSpan.FromMilliseconds(delayMs), new DeliverMessage(recipient, message));
            }

            if (Happens(MessageCopyProbability, nameof(MessageCopyProbability))) {
                Send(recipient, message);
            }

            if (networkFailure) {
                throw new IOException("network error");
            }
        }

        bool TryGetFuture(out object o) {
            if (_halt != null) {
                o = null;
                return false;
            }

            if (_future.Count == 0) {
                o = null;
                return false;
            }

            var time = _future.Keys[0];
            if (time > _time) {
                _time = time;
            }

            o = _future.Values[0];
            _future.RemoveAt(0);
            return true;
        }


        public async Task<long> GetAccountAmount(int key) {
            await ActorFreeze();
            await StorageFreeze();

            if (_db.TryGetValue(key, out var value)) {
                return value;
            }

            return 0;
        }

        async Task StorageFreeze() {
            if (Happens(StorageFreezeProbability, nameof(StorageFreezeProbability))) {
                await Delay(TimeSpan.FromMilliseconds(NextUint(10000)));
            }
        }

        public async Task PutAccountAmount(int key, long value) {
            await ActorFreeze();
            await StorageFreeze();
            _db[key] = value;
        }
    }
}