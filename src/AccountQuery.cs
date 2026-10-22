using System;
using System.Threading.Tasks;

namespace TetoDesk {
    public sealed class AccountQueryResult {
        public string Backend;
        public BalanceSnapshot Balance;
        public QuotaSnapshot Quota;
    }
    // Both the pet and tray use this route, including loading, errors and
    // provider changes while an account request is still pending.
    internal sealed class AccountQuery : IDisposable {
        private readonly Controller controller;
        private readonly PetWindow pet;
        private readonly Action<string> caption;
        private readonly Func<Task<AccountQueryResult>> read;
        private string backend;
        private bool disposed;
        internal AccountQuery(Controller controller, PetWindow pet, Action<string> caption, Func<Task<AccountQueryResult>> read = null) {
            this.controller = controller; this.pet = pet; this.caption = caption;
            this.read = read ?? controller.ReadCurrentAccountAsync;
            backend = controller.Backend; controller.Changed += Refresh; Refresh();
        }
        private void Refresh() {
            if (disposed) return;
            if (backend != controller.Backend) { backend = controller.Backend; pet.DismissQuotaInfo(); }
            string label = controller.HasAvailableBackend ? controller.AccountQueryLabel : "定位对话客户端…";
            pet.QueryMenuText = label;
            if (caption != null) caption(label);
        }
        internal async Task RunAsync() {
            if (disposed) return;
            if (!controller.HasAvailableBackend) return;
            string requested = controller.Backend; bool balance = requested == "deepseek";
            int ticket = pet.BeginQuotaInfo(balance);
            try {
                var result = await read();
                if (disposed || controller.Backend != requested || result.Backend != requested) return;
                if (balance) pet.ShowBalanceResult(ticket, result.Balance);
                else pet.ShowQuotaResult(ticket, result.Quota);
            } catch (Exception e) {
                if (disposed || controller.Backend != requested) return;
                if (balance) pet.ShowBalanceError(ticket, e.Message);
                else pet.ShowQuotaError(ticket);
            }
        }
        public void Dispose() { if (disposed) return; disposed = true; controller.Changed -= Refresh; pet.DismissQuotaInfo(); }
    }
}
