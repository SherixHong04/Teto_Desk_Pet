using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TetoDesk {
    public sealed class ChatEntry {
        public string Role, Id, Text;
        public readonly List<ChatAttachment> Attachments = new List<ChatAttachment>();
        public ChatEntry(string role, string id, string text) { Role = role; Id = id; Text = text; }
    }
    public sealed class Controller : IDisposable {
        public readonly TaskState Status = new TaskState();
        public readonly List<ChatEntry> Messages = new List<ChatEntry>();
        public readonly List<CodexModel> Models = new List<CodexModel>();
        public event Action Changed;
        public event Action<RpcEvent> ApprovalRequested;
        public event Action<string> Activity;
        public IConversationClient Client { get; private set; }
        public string Backend { get; private set; }
        public ProviderCatalog Providers { get; private set; }
        public bool HasAvailableBackend { get { return Providers == null || Providers.Contains(Backend); } }
        public string[] AvailableBackends { get { return Providers == null ? new[] { "deepseek", "codex" } : Providers.Available; } }
        public string AccountQueryLabel { get { return Backend == "deepseek" ? "查询账户余额" : "查询剩余额度"; } }
        public bool Connecting { get; private set; }
        public string Workspace, ThreadId = "", AccountLabel = "使用 ChatGPT 登录", Model = "", Effort = "";
        public bool Busy { get { return submitting || Status.Busy; } }
        public string PreparationLabel { get; private set; }
        public bool HasCustomWorkspace { get { return !String.Equals(Path.GetFullPath(Workspace).TrimEnd('\\', '/'), Path.GetFullPath(defaultWorkspace).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); } }
        private readonly Action<Action> dispatch;
        private string storePath;
        private readonly string codexStorePath, defaultWorkspace;
        private System.Threading.CancellationTokenSource submission;
        private bool submitting, stopRequested, disposed;
        private Task connection;
        private readonly Dictionary<string, RpcEvent> approvals = new Dictionary<string, RpcEvent>();
        public Controller(string path, string workspace, Action<Action> dispatcher, ProviderCatalog providers = null) {
            codexStorePath = storePath = path; defaultWorkspace = Workspace = workspace; dispatch = dispatcher; Backend = "codex";
            Providers = providers;
            LoadSession();
            if (Providers != null) {
                string selected = Providers.Preferred("");
                if (selected.Length == 0) throw new IOException("未定位到支持的对话客户端，请先选择已安装的 Codex CLI 或 DeepSeek Harness。");
                if (selected != Backend) SetBackend(selected);
                else { AccountLabel = "ChatGPT · 待连接"; Providers.SaveSelection(Backend); }
            }
        }
        private void LoadSession() {
            ThreadId = Model = Effort = ""; Workspace = defaultWorkspace; Messages.Clear(); Models.Clear();
            try {
                if (File.Exists(storePath)) {
                    object saved = J.Parse(File.ReadAllText(storePath));
                    string cwd = J.S(saved, "workspace"); if (Directory.Exists(cwd)) Workspace = cwd;
                    ThreadId = J.S(saved, "threadId");
                    Model = J.S(saved, "model");
                    Effort = J.S(saved, "effort");
                    foreach (object value in J.A(J.Get(saved, "messages"))) {
                        var entry = new ChatEntry(J.S(value, "role"), J.S(value, "id"), J.S(value, "text"));
                        foreach (object file in J.A(J.Get(value, "attachments"))) entry.Attachments.Add(ChatAttachment.Read(file));
                        Messages.Add(entry);
                    }
                }
            } catch { ThreadId = ""; Messages.Clear(); }
        }
        public bool SetBackend(string backend) {
            if (Busy || Connecting || (backend != "codex" && backend != "deepseek")) return false;
            if (Providers != null && !Providers.Contains(backend)) return false;
            if (Backend == backend) return true;
            Save(); if (Client != null) Client.Dispose(); Client = null; connection = null;
            Backend = backend; storePath = backend == "codex" ? codexStorePath : Path.Combine(Path.GetDirectoryName(codexStorePath), "session-deepseek.json");
            LoadSession(); Status.ThreadId = Status.TurnId = ""; Status.End(PetState.Idle); ResolveAll();
            AccountLabel = backend == "codex" ? "ChatGPT · 点击切换" : "DeepSeek · 点击切换";
            string preferences = Path.Combine(Path.GetDirectoryName(codexStorePath), "providers.json");
            object previous = null; try { if (File.Exists(preferences)) previous = J.Parse(File.ReadAllText(preferences)); } catch { }
            if (Providers != null) Providers.SaveSelection(backend);
            else J.Save(preferences, J.O("backend", backend, "harnessExecutable", J.S(previous, "harnessExecutable"), "codexExecutable", J.S(previous, "codexExecutable")));
            Notify(); return true;
        }
        public bool RefreshProviders() {
            if (Busy || Connecting || Providers == null) return false;
            Providers.Refresh();
            if (!Providers.Contains(Backend)) {
                string replacement = Providers.Preferred("");
                if (replacement.Length > 0) return SetBackend(replacement);
                AccountLabel = "请重新定位客户端";
            }
            Notify(); return HasAvailableBackend;
        }
        public void ConfigureExecutable(string path) {
            if (Busy || Connecting) throw new InvalidOperationException("任务或连接完成后才能更换客户端。");
            if (Providers == null) throw new InvalidOperationException("当前宿主未启用本机客户端配置。");
            string selected = ProviderDiscovery.Classify(path); Providers.Configure(path);
            if (Backend != selected) { SetBackend(selected); return; }
            if (Client != null) Client.Dispose(); Client = null; connection = null; Models.Clear();
            AccountLabel = selected == "codex" ? "ChatGPT · 待连接" : "DeepSeek · 待连接"; Notify();
        }
        public void SetHarnessExecutable(string path) {
            if (Busy || Connecting) return;
            if (Providers != null) { ConfigureExecutable(path); return; }
            if (!File.Exists(path) || !File.Exists(Path.Combine(Path.GetDirectoryName(path), "resources", "app.asar"))) throw new IOException("请选择完整安装目录内的 DeepSeek Harness.exe。");
            Environment.SetEnvironmentVariable("TETO_HARNESS_EXE", path);
            J.Save(Path.Combine(Path.GetDirectoryName(codexStorePath), "providers.json"), J.O("backend", Backend, "harnessExecutable", path));
            if (Backend == "deepseek" && Client != null) { Client.Dispose(); Client = null; connection = null; Models.Clear(); }
            Notify();
        }
        public async Task<BalanceSnapshot> ReadBalanceAsync() {
            if (!HasAvailableBackend || Backend != "deepseek") throw new InvalidOperationException("当前未选择可用的 DeepSeek Harness。");
            object result = await HarnessRuntime.AuxiliaryAsync("balance", Workspace, Path.Combine(Path.GetDirectoryName(codexStorePath), "Bridge"), null, default(System.Threading.CancellationToken), Providers == null ? null : Providers.Executable("deepseek"));
            return BalanceSnapshot.Read(result);
        }
        public async Task<AccountQueryResult> ReadCurrentAccountAsync() {
            if (disposed) throw new ObjectDisposedException("Controller");
            string requested = Backend;
            var result = new AccountQueryResult { Backend = requested };
            if (requested == "deepseek") result.Balance = await ReadBalanceAsync();
            else result.Quota = await ReadQuotaAsync();
            if (Backend != requested) throw new OperationCanceledException("对话服务已切换，本次账户查询已取消。");
            return result;
        }
        public void NewChat(string workspace) {
            if (Busy) return;
            Workspace = workspace; ThreadId = ""; Messages.Clear(); Status.ThreadId = ""; Status.TurnId = ""; Status.End(PetState.Idle); Save(); Notify();
        }
        public bool ResetWorkspace() {
            if (disposed || Busy || Connecting || !HasCustomWorkspace) return false;
            Directory.CreateDirectory(defaultWorkspace); NewChat(defaultWorkspace); return true;
        }
        public Task ConnectAsync() {
            if (!HasAvailableBackend) return Task.FromException(new IOException("所选客户端已不可用，请在服务菜单重新检测或定位客户端。"));
            if (connection != null && !connection.IsCompleted) return connection;
            if (Client != null && Client.Connected) return Task.FromResult(0);
            Connecting = true; connection = ConnectCoreAsync(); return connection;
        }
        public bool SetModel(string id) {
            if (Busy) return false;
            var selected = Models.Find(m => m.Id == id); if (selected == null) return false;
            Model = selected.Id; Effort = ModelCatalog.SelectEffort(selected, Effort); Save(); Notify(); return true;
        }
        public bool SetEffort(string id) {
            if (Busy || Models.Count == 0) return false;
            var selected = ModelCatalog.Select(Models, Model);
            if (!selected.Efforts.Exists(e => e.Id == id)) return false;
            Effort = id; Save(); Notify(); return true;
        }
        public async Task<QuotaSnapshot> ReadQuotaAsync() {
            if (disposed) throw new ObjectDisposedException("Controller");
            if (Backend != "codex") throw new InvalidOperationException("当前服务使用账户余额查询。");
            await ConnectAsync();
            if (Backend != "codex") throw new OperationCanceledException("对话服务已切换，本次额度查询已取消。");
            // Account metadata only: do not create a thread, start a turn,
            // clear messages, interrupt work, or consume an earned reset.
            object response = await Client.RequestAsync("account/rateLimits/read", J.O(), 15000);
            return QuotaSnapshot.Read(response);
        }
        private async Task ConnectCoreAsync() {
            try {
            if (Client != null) Client.Dispose();
            string executable = Providers == null ? null : Providers.Executable(Backend);
            Client = Backend == "deepseek" ? (IConversationClient)new DeepSeekClient(Path.Combine(Path.GetDirectoryName(storePath), "Bridge"), executable) : new CodexClient(executable);
            Client.Event += OnEvent;
            Client.Disconnected += delegate(object sender, EventArgs args) {
                dispatch(delegate {
                    if (disposed || sender != Client) return;
                    AccountLabel = "连接已断开";
                    if (Busy) { Status.End(stopRequested ? PetState.Interrupted : PetState.Failed); submitting = false; ResolveAll(); AddNotice(stopRequested ? "任务已停止。" : "连接中断，下次发送时会重新连接。"); }
                    Notify();
                });
            };
            await Client.ConnectAsync(Workspace);
            AccountLabel = Client.AccountLabel;
            Models.Clear(); Models.AddRange(Client.Models);
            var selected = ModelCatalog.Select(Models, Model);
            Model = selected.Id; Effort = ModelCatalog.SelectEffort(selected, Effort); Save(); Notify();
            } finally { Connecting = false; Notify(); }
        }
        public Task<bool> SendAsync(string text) { return SendAsync(text, new List<ChatAttachment>()); }
        public async Task<bool> SendAsync(string text, IList<ChatAttachment> attachments) {
            if (!HasAvailableBackend) return false;
            if (Busy || disposed || (String.IsNullOrWhiteSpace(text) && attachments.Count == 0)) return false;
            var sources = new List<ChatAttachment>(attachments);
            bool accepted = false;
            if (submission != null) submission.Dispose();
            submission = new System.Threading.CancellationTokenSource();
            stopRequested = false; submitting = true; Status.End(PetState.Connecting); Status.Busy = true;
            Notify();
            try {
                AttachmentInput.Validate(sources, null);
                Directory.CreateDirectory(Workspace);
                await ConnectAsync();
                if (stopRequested || disposed) { Status.End(PetState.Interrupted); return false; }
                await Client.VerifyWorkspaceToolsAsync(Workspace);
                if (stopRequested || disposed) { Status.End(PetState.Interrupted); return false; }
                CodexModel selected = ModelCatalog.Select(Client.Models, Model);
                Model = selected.Id; Effort = ModelCatalog.SelectEffort(selected, Effort);
                string chosenEffort = Effort;
                AttachmentInput.Validate(sources, selected);
                var files = await Task.Run(() => AttachmentInput.Snapshot(sources, Path.Combine(Path.GetDirectoryName(storePath), "Attachments")));
                if (Backend == "deepseek") {
                    foreach (var file in files) if (DeepSeekInput.IsAudio(file.Path)) {
                        PreparationLabel = "正在本地转写语音…"; Status.State = PetState.Working; Notify();
                        file.ExtractedText = await AudioInput.TranscribeAsync(file.Path, Workspace, Path.Combine(Path.GetDirectoryName(storePath), "Bridge"), submission.Token);
                        file.ReadHint = "已通过本地 SenseVoice 转写语音";
                    }
                }
                PreparationLabel = "";
                if (stopRequested || disposed) { Status.End(PetState.Interrupted); return false; }
                var message = new ChatEntry("user", Guid.NewGuid().ToString("N"), String.IsNullOrWhiteSpace(text) ? "请查看所附文件。" : text);
                message.Attachments.AddRange(files); Messages.Add(message); accepted = true; Notify(); Save();
                var threadParameters = J.O("cwd", Workspace, "model", Model, "sandbox", "workspace-write", "approvalPolicy", "on-request", "approvalsReviewer", "user");
                if (chosenEffort.Length > 0) threadParameters["config"] = J.O("model_reasoning_effort", chosenEffort);
                object started;
                if (ThreadId.Length > 0) {
                    try {
                        threadParameters["threadId"] = ThreadId;
                        started = await Client.RequestAsync("thread/resume", threadParameters, 60000);
                    } catch (Exception e) { throw new InvalidOperationException("恢复会话失败。可以点击“新会话”后重试。" + e.Message); }
                } else {
                    started = await Client.RequestAsync("thread/start", threadParameters, 60000);
                    ThreadId = J.S(J.Get(started, "thread"), "id");
                }
                // Old resumed-thread metadata can still name the legacy model.
                // The explicit turn override below is the model actually used.
                Model = selected.Id;
                if (stopRequested || disposed) { Status.End(PetState.Interrupted); return accepted; }
                Status.Begin(ThreadId); Notify(); Save();
                var turnParameters = J.O("threadId", ThreadId, "model", selected.Id, "input", Backend == "deepseek" ? DeepSeekInput.Build(text, files) : AttachmentInput.Build(text, files));
                if (chosenEffort.Length > 0) turnParameters["effort"] = chosenEffort;
                object turn = await Client.RequestAsync("turn/start", turnParameters, 60000);
                if (Status.Busy && Status.TurnId.Length == 0) Status.TurnId = J.S(J.Get(turn, "turn"), "id");
                if (stopRequested && Status.Busy) await InterruptAsync();
            } catch (Exception e) {
                Status.End(stopRequested ? PetState.Interrupted : PetState.Failed);
                AddNotice(stopRequested ? "任务已停止。" : e.Message);
                if (Client != null) Client.Dispose();
                AccountLabel = "下次发送时重新连接";
            } finally { PreparationLabel = ""; submitting = false; Save(); Notify(); }
            return accepted;
        }
        public async Task StopAsync() {
            if (!Busy) return;
            stopRequested = true;
            if (submission != null) submission.Cancel();
            if (Status.TurnId.Length > 0 && Client != null && Client.Connected) {
                try { await InterruptAsync(); } catch (Exception e) { AddNotice("停止请求未成功，连接已结束。" + e.Message); Client.Dispose(); Status.End(PetState.Interrupted); submitting = false; }
            } else {
                if (Client != null) Client.Dispose(); Status.End(PetState.Interrupted);
            }
            ResolveAll(); Save(); Notify();
        }
        private async Task InterruptAsync() { await Client.RequestAsync("turn/interrupt", J.O("threadId", ThreadId, "turnId", Status.TurnId), 15000); }
        private void OnEvent(object sender, RpcEvent e) {
            dispatch(delegate {
                if (disposed || sender != Client) return;
                if (e.Id != null) { ReceiveRequest(e); return; }
                if (!Status.Belongs(e.Params)) return;
                // Ignore late item events from a completed turn, and all other conversations.
                if (!Status.Busy && e.Method != "turn/completed") return;
                Status.Apply(e.Method, e.Params);
                if (e.Method == "item/agentMessage/delta") {
                    string id = J.S(e.Params, "itemId"); ChatEntry message = Find(id);
                    if (message == null) { message = new ChatEntry("assistant", id, ""); Messages.Add(message); }
                    message.Text += J.S(e.Params, "delta");
                } else if (e.Method == "item/completed") {
                    object item = J.Get(e.Params, "item");
                    if (J.S(item, "type") == "agentMessage") {
                        string id = J.S(item, "id"); ChatEntry message = Find(id);
                        if (message == null) Messages.Add(new ChatEntry("assistant", id, J.S(item, "text"))); else message.Text = J.S(item, "text");
                    }
                } else if (e.Method == "error") {
                    string message = J.S(J.Get(e.Params, "error"), "message");
                    AddNotice(J.B(e.Params, "willRetry") ? "连接暂时波动，Codex 正在重试。" : message);
                } else if (e.Method == "serverRequest/resolved") {
                    string id = J.Str(J.Get(e.Params, "requestId")); approvals.Remove(id); Status.Approval(id, false);
                } else if (e.Method == "turn/completed") {
                    submitting = false;
                    object turn = J.Get(e.Params, "turn");
                    if (J.S(turn, "status") == "failed") AddNotice(J.S(J.Get(turn, "error"), "message"));
                    else if (J.S(turn, "status") == "interrupted") AddNotice("任务已停止，已产生的回复和文件会保留。");
                    ResolveAll(); Save();
                }
                var activity = Activity; if (activity != null) activity(e.Method + " → " + TaskState.Label(Status.State));
                Notify();
            });
        }
        private ChatEntry Find(string id) { foreach (var entry in Messages) if (entry.Role == "assistant" && entry.Id == id) return entry; return null; }
        private void ReceiveRequest(RpcEvent e) {
            if (!Status.Belongs(e.Params) || !Status.Busy) { Client.RejectUnsupported(e.Id); return; }
            if (e.Method == "item/commandExecution/requestApproval" || e.Method == "item/fileChange/requestApproval" || e.Method == "item/permissions/requestApproval" || e.Method == "item/tool/requestUserInput") {
                string key = J.Str(e.Id); approvals[key] = e; Status.Approval(key, true);
                var handler = ApprovalRequested; if (handler != null) handler(e); Notify();
            } else {
                Client.RejectUnsupported(e.Id); AddNotice("此工具请求暂未接入：" + e.Method); Notify();
            }
        }
        public void Answer(RpcEvent request, object result) {
            string key = J.Str(request.Id);
            if (!approvals.ContainsKey(key) || Client == null || !Client.Connected) return;
            try { Client.Respond(request.Id, result); approvals.Remove(key); Status.Approval(key, false); }
            catch (Exception e) { AddNotice(e.Message); }
            Notify();
        }
        public bool IsPending(RpcEvent request) { return approvals.ContainsKey(J.Str(request.Id)); }
        private void ResolveAll() { approvals.Clear(); }
        private void AddNotice(string text) {
            if (String.IsNullOrWhiteSpace(text)) return;
            if (Messages.Count > 0 && Messages[Messages.Count - 1].Role == "notice" && Messages[Messages.Count - 1].Text == text) return;
            Messages.Add(new ChatEntry("notice", Guid.NewGuid().ToString("N"), text));
        }
        private void Notify() { var changed = Changed; if (changed != null) changed(); }
        public void Save() {
            try {
                var messages = new List<object>(); foreach (var e in Messages) {
                    var files = new List<object>(); foreach (var file in e.Attachments) files.Add(file.Json());
                    messages.Add(J.O("role", e.Role, "id", e.Id, "text", e.Text, "attachments", files));
                }
                J.Save(storePath, J.O("workspace", Workspace, "threadId", ThreadId, "model", Model, "effort", Effort, "messages", messages));
            } catch { }
        }
        public void Dispose() { disposed = true; if (submission != null) submission.Cancel(); Save(); if (Client != null) Client.Dispose(); }
    }
}
