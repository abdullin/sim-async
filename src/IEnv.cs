using System;
using System.Threading.Tasks;

namespace SimAsync {
    public interface IEnv {
        Task Send(int recipient, object msg);

        Task Delay(TimeSpan sleep);
        void Debug(string arg);

        Task<long> GetAccountAmount(int thisActor);
        Task PutAccountAmount(int thisActor, long l);
    }
}