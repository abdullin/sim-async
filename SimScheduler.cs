using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace SimRing {
    public sealed class SimScheduler : TaskScheduler {
        readonly Sim _sim;

        public SimScheduler(Sim sim) {
            _sim = sim;
        }

        public void Execute(Task task) {
            if (!TryExecuteTask(task)) {
                throw new InvalidOperationException("Something went wrong");
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks() {
            throw new NotImplementedException();
        }

        protected override void QueueTask(Task task) {

            switch (task) {
                case FutureTask ft:
                    _sim.Schedule(ft.Ts, ft);
                    break;
                default:
                    _sim.Schedule(TimeSpan.Zero, task);
                    break;
            }
            
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
            return TryExecuteTask(task);
        }
    }

    sealed class FutureTask : Task {
        public readonly TimeSpan Ts;

        public FutureTask(TimeSpan ts) : base(() => {}) {
            Ts = ts;
        }
    }


    public sealed class DeliverMessage {
        public readonly int Recipient;
        public readonly object Body;

        public DeliverMessage(int recipient, object body) {
            Recipient = recipient;
            Body = body;
        }
    }
}