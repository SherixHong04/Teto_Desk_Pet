using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TetoDesk {
    public interface IConversationClient : IDisposable {
        event EventHandler<RpcEvent> Event;
        event EventHandler Disconnected;
        bool Connected { get; }
        string AccountLabel { get; }
        string Executable { get; }
        List<CodexModel> Models { get; }
        Task ConnectAsync(string workingDirectory);
        Task VerifyWorkspaceToolsAsync(string workingDirectory);
        Task<object> RequestAsync(string method, object parameters, int timeoutMs);
        void Respond(object id, object result);
        void RejectUnsupported(object id);
    }
}
