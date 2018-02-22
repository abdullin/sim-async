using System;
using System.Threading.Tasks;

namespace SimAsync {
    public class Actor {
        readonly IEnv _env;

        readonly int NextActor;
        readonly int ThisActor;

        public Actor(int nextActor, int thisActor, IEnv env) {
            NextActor = nextActor;
            ThisActor = thisActor;
            _env = env;
        }

        async Task Deposit(DepositAmount depositAmount) {
            var dac = await _env.GetAccountAmount(ThisActor);
            await _env.PutAccountAmount(ThisActor, dac + depositAmount.Amount);
            // after depositing, we send message to ourself to transfer
            // the amount to the next actor in ring
            await RetryPolicy.SendWithBackOff(_env, ThisActor, new SendAmount(depositAmount.Amount, NextActor));
        }

        async Task Send(SendAmount msg) {
            var current = await _env.GetAccountAmount(ThisActor);
            if (msg.Amount > current) {
                throw new InvalidOperationException($"Account {ThisActor} has insufficient amount to withdraw");
            }

            await _env.PutAccountAmount(ThisActor, current - msg.Amount);
            await RetryPolicy.SendWithBackOff(_env, msg.Target, new DepositAmount(msg.Amount, ThisActor));
        }

        public async Task DispatchMessage(object message) {
            _env.Debug($"A{ThisActor:0000} {message}");

            await RetryPolicy.HandleWithRetry(() => DispatchMessageInner(message));
        }

        Task DispatchMessageInner(object message) {
            switch (message) {
                case SendAmount ts:
                    return Send(ts);
                case DepositAmount da:
                    return Deposit(da);
                default:
                    throw new InvalidOperationException("Unknown message");
            }
        }
    }
}