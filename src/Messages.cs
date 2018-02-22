namespace SimAsync {
    public sealed class DepositAmount {
        public readonly long Amount;
        public readonly long FromActor;

        public DepositAmount(long amount, long from) {
            Amount = amount;
            FromActor = from;
        }

        public override string ToString() {
            return $"Deposit {Amount} from A{FromActor}";
        }
    }

    public sealed class SendAmount {
        public readonly long Amount;
        public readonly int Target;

        public SendAmount(long amount, int target) {
            Amount = amount;
            Target = target;
        }

        public override string ToString() {
            return $"Transfer {Amount} to A{Target}";
        }
    }
}