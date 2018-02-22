using System;
using System.Threading.Tasks;

namespace SimAsync {
    public static class RetryPolicy {
        public static async Task SendWithBackOff(IEnv env, int recipient, object msg) {
            var counter = 0;
            while (true) {
                try {
                    await env.Send(recipient, msg);
                    return;
                } catch (Exception ex) {
                    if (counter < 10) {
                        counter++;
                        var sleep = TimeSpan.FromSeconds(Math.Pow(2, counter));
                        env.Debug($"Retrying send on {ex.Message} #{counter} after {sleep.TotalSeconds} seconds");
                        await env.Delay(sleep);
                        continue;
                    }

                    throw;
                }
            }
        }

        public static async Task HandleWithRetry(Func<Task> handler) {
            var counter = 0;

            while (true) {
                try {
                    await handler();
                    return;
                } catch (Exception) {
                    if (counter < 4) {
                        counter++;
                    }

                    throw;
                }
            }
        }
    }
}