using System;
using System.Collections.Generic;

namespace SimAsync {
    class Program {
        static void Main(string[] args) {
            const int n = 1000;

            var sim = new Sim {
                MessageCopyProbability = 0.001M,
                NetworkFailureProbability = 0.0001M,
                MessageDelayProbability = 0.001M,
                StorageFreezeProbability = 0.001M,
                ActorFreezeProbability = 0.0001M,
                StorageFailureProbability = 0.0001M,
                MaxExecutionTime = TimeSpan.FromSeconds(20),
                NetworkOutageProbability = 0.0001M,
                MessageLossProbability = 0.001M,
                // by default we use random seed
                // Seed = 675672838,
                Seed = (uint)new Random().Next(),

                PrintDebug = false
            };
            var actors = new List<Actor>();
            for (var i = 0; i < n; i++) {
                var next = (i + 1) % n;
                actors.Add(new Actor(next, i, sim));
            }

            sim.Schedule(TimeSpan.Zero, new DeliverMessage(0, new DepositAmount(10, -1)));
            sim.Schedule(TimeSpan.FromSeconds(1), new DeliverMessage(500, new DepositAmount(10, -1)));

            sim.Run(actors);
        }
    }
}