using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TetoDesk {
    public static class HarnessRuntime {
        public static string FindExecutable() {
            string found = ProviderDiscovery.Find("deepseek", ProviderDiscovery.ConfiguredExecutable("deepseek"));
            if (found.Length > 0) return found;
            throw new FileNotFoundException("找不到 DeepSeek Harness，请点击账号菜单选择 DeepSeek Harness.exe。");
        }
        public static string Quote(string value) {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) { if (c == '\\') { slashes++; continue; } if (c == '"') result.Append('\\', slashes * 2 + 1); else result.Append('\\', slashes); result.Append(c); slashes = 0; }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
        public static ProcessStartInfo StartInfo(string executable, string workingDirectory, string patch) {
            string carrier = Path.Combine(Path.GetDirectoryName(executable), "resources", "app.asar", "dsh", "node_modules", "@deepseek-ai", "dsh-desktop-host", "lib", "cli.js");
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(executable), "resources", "app.asar"))) throw new IOException("所选文件不是完整的 DeepSeek Harness 安装，请保留 resources 目录。");
            var info = new ProcessStartInfo(executable, "--expose-internals " + Quote(carrier) + " --profile acp --patch " + Quote(patch)) {
                WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false)
            };
            info.EnvironmentVariables["ELECTRON_RUN_AS_NODE"] = "1";
            // Account mode must never silently use an inherited API key.
            foreach (string key in new[] { "DEEPSEEK_API_KEY", "OPENAI_API_KEY", "CODEX_API_KEY" }) info.EnvironmentVariables.Remove(key);
            return info;
        }
        public static async Task<object> AuxiliaryAsync(string mode, string workingDirectory, string dataDirectory, string file, CancellationToken token = default(CancellationToken), string executable = null) {
            Directory.CreateDirectory(dataDirectory);
            string helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bridge", "harness-aux.mjs");
            if (!File.Exists(helper)) throw new FileNotFoundException("缺少 Harness 连接文件，请完整解压程序的 Bridge 文件夹。");
            string yaml = "- id: acp\n  disabled: true\n- insert:\n    - id: teto-aux\n      name: " + J.Serialize(new Uri(helper).AbsoluteUri) + "\n";
            bool audio = mode == "audio" || mode == "audio-prepare";
            if (audio) yaml += "- insert:\n    - id: speech-to-text\n      name: '@deepseek-ai/dsh-experimental-speech-to-text'\n      config:\n        defaultProvider: sensevoice-local\n    - id: speech-to-text-sensevoice\n      name: '@deepseek-ai/dsh-experimental-speech-to-text-sensevoice'\n      config:\n        dataRoot: !!js process.env.TETO_VOICE_CACHE\n        maxAudioBytes: 20971520\n- id: teto-aux\n  inject: [deepseekAccount, llm, speechToText]\n";
            string patch = Path.Combine(dataDirectory, "harness-aux-" + Guid.NewGuid().ToString("N") + ".yml");
            File.WriteAllText(patch, yaml, new UTF8Encoding(false));
            try {
                var info = StartInfo(executable ?? FindExecutable(), workingDirectory, patch);
                info.EnvironmentVariables["TETO_AUX_MODE"] = mode;
                info.EnvironmentVariables["TETO_AUDIO_FILE"] = file ?? "";
                info.EnvironmentVariables["TETO_VOICE_CACHE"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "speech-to-text", "sensevoice");
                using (var process = new Process { StartInfo = info }) using (var job = new ProcessJob()) {
                    process.Start(); job.Attach(process);
                    Task<string> output = process.StandardOutput.ReadToEndAsync(); Task<string> errors = process.StandardError.ReadToEndAsync();
                    int timeout = audio ? 12 * 60 * 1000 : 45000;
                    var finished = Task.Run(() => process.WaitForExit(timeout));
                    await Task.WhenAny(finished, Task.Delay(Timeout.Infinite, token));
                    if (token.IsCancellationRequested) { process.Kill(); token.ThrowIfCancellationRequested(); }
                    bool ended = await finished;
                    if (!ended) { process.Kill(); throw new TimeoutException(audio ? "本地语音识别准备或转写超时，请稍后重试。" : "Harness 账户查询超时。"); }
                    string raw = (await output).Trim(); await errors;
                    if (raw.Length == 0) throw new IOException("Harness 辅助服务启动失败，请检查安装版本与网络。");
                    object response = null;
                    foreach (string line in raw.Split('\n')) {
                        string candidate = line.Trim();
                        if (!candidate.StartsWith("{\"result\":", StringComparison.Ordinal) && !candidate.StartsWith("{\"error\":", StringComparison.Ordinal)) continue;
                        try { response = J.Parse(candidate); } catch { }
                    }
                    if (response == null) throw new IOException("Harness 未返回有效结果，请检查插件与登录状态。");
                    if (J.Get(response, "error") != null) throw new IOException(J.S(response, "error"));
                    return J.Get(response, "result");
                }
            } finally { try { File.Delete(patch); } catch { } }
        }
    }
    public sealed class DeepSeekClient : IConversationClient {
        public event EventHandler<RpcEvent> Event;
        public event EventHandler Disconnected;
        private readonly string dataDirectory;
        private readonly string configuredExecutable;
        private Process process; private ProcessJob job; private StreamWriter input;
        private readonly object gate = new object(), writeGate = new object();
        private readonly Dictionary<int, TaskCompletionSource<object>> pending = new Dictionary<int, TaskCompletionSource<object>>();
        private readonly Dictionary<string, object> permissionIds = new Dictionary<string, object>();
        private readonly Dictionary<string, object> toolCalls = new Dictionary<string, object>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private int sequence; private bool disposed;
        private string loadedSession = "", loadedWorkspace = "", activeTurn = "", activeSession = "", replyId = "";
        private bool cancelRequested, imageEnabled;
        private readonly StringBuilder reply = new StringBuilder();
        private readonly List<CodexModel> models = new List<CodexModel>();
        public List<CodexModel> Models { get { return models; } }
        public string AccountLabel { get { return "DeepSeek · 账号登录"; } }
        public string Executable { get; private set; }
        public bool Connected { get { return !disposed && process != null && !process.HasExited; } }
        public DeepSeekClient(string directory, string executable = null) { dataDirectory = directory; configuredExecutable = executable; }
        public async Task ConnectAsync(string workingDirectory) {
            if (Connected) return; disposed = false;
            Executable = configuredExecutable ?? HarnessRuntime.FindExecutable();
            string patch = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bridge", "deepseek-acp.patch.yml");
            if (!File.Exists(patch)) throw new FileNotFoundException("缺少 Bridge 连接配置，请完整解压程序。");
            process = new Process { StartInfo = HarnessRuntime.StartInfo(Executable, workingDirectory, patch) };
            process.Start(); job = new ProcessJob(); job.Attach(process);
            input = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true };
            Task.Run((Func<Task>)ReadLoop); Task.Run(async delegate { try { while (await process.StandardError.ReadLineAsync() != null) { } } catch { } });
            try {
                object init = await NativeRequest("initialize", J.O("protocolVersion", 1, "clientCapabilities", J.O(), "clientInfo", J.O("name", "teto-desk", "version", "0.1.18")), 60000);
                imageEnabled = J.B(J.Get(J.Get(init, "agentCapabilities"), "promptCapabilities"), "image");
                object catalog = await HarnessRuntime.AuxiliaryAsync("catalog", workingDirectory, dataDirectory, null, lifetime.Token);
                models.Clear(); models.AddRange(ReadModels(catalog, imageEnabled));
                if (models.Count == 0) throw new IOException("Harness 没有返回账号模型，请先在 DeepSeek Harness 中登录账号。");
            } catch { Dispose(); throw; }
        }
        internal static List<CodexModel> ReadModels(object catalog, bool images) {
            var result = new List<CodexModel>();
            foreach (object entry in J.A(catalog)) {
                string id = J.S(entry, "id"); if (id.Length == 0) continue;
                bool supportsImage = false; foreach (object modality in J.A(J.Get(entry, "inputModalities"))) supportsImage |= J.Str(modality) == "image";
                var model = new CodexModel { Id = J.Serialize(new[] { "deepseek-account", id }), Name = J.S(entry, "name"), SupportsImages = images && supportsImage, IsDefault = id == "deepseek-flash" };
                if (model.Name.Length == 0) model.Name = id;
                object reasoning = J.Get(entry, "reasoning"); model.Effort = J.S(reasoning, "defaultEffort");
                foreach (object effort in J.A(J.Get(reasoning, "efforts"))) model.Efforts.Add(new CodexEffort { Id = J.S(effort, "id"), Description = J.S(effort, "description") });
                result.Add(model);
            }
            return result;
        }
        public Task VerifyWorkspaceToolsAsync(string workingDirectory) {
            if (!Connected || !Directory.Exists(workingDirectory)) throw new IOException("Harness 未连接或工作文件夹无法访问，消息尚未发送。");
            return Task.FromResult(0);
        }
        public async Task<object> RequestAsync(string method, object p, int timeoutMs) {
            if (method == "thread/start" || method == "thread/resume") {
                string session = J.S(p, "threadId"), cwd = J.S(p, "cwd"); object response;
                if (session.Length > 0 && session == loadedSession && String.Equals(cwd, loadedWorkspace, StringComparison.OrdinalIgnoreCase)) return J.O("thread", J.O("id", session));
                if (loadedSession.Length > 0) { await NativeRequest("session/close", J.O("sessionId", loadedSession), 30000); loadedSession = ""; }
                if (session.Length == 0) { response = await NativeRequest("session/new", J.O("cwd", cwd, "mcpServers", new object[0]), timeoutMs); session = J.S(response, "sessionId"); }
                else response = await NativeRequest("session/resume", J.O("sessionId", session, "cwd", cwd, "mcpServers", new object[0]), timeoutMs);
                loadedSession = session; loadedWorkspace = cwd;
                return J.O("thread", J.O("id", session));
            }
            if (method == "turn/start") {
                if (activeTurn.Length > 0) throw new InvalidOperationException("Harness 已有任务正在执行。");
                string session = J.S(p, "threadId"), model = J.S(p, "model"), effort = J.S(p, "effort");
                object[] route = J.A(J.Parse(model));
                if (route.Length != 2 || J.Str(route[0]) != "deepseek-account") throw new IOException("本通道仅允许 DeepSeek Account 账号模型，不会回退到 API Key。");
                await NativeRequest("session/set_config_option", J.O("sessionId", session, "configId", "model", "value", model), 30000);
                if (effort.Length > 0) await NativeRequest("session/set_config_option", J.O("sessionId", session, "configId", "reasoning_effort", "value", effort), 30000);
                activeTurn = Guid.NewGuid().ToString("N"); activeSession = session; cancelRequested = false; reply.Clear(); toolCalls.Clear(); replyId = Guid.NewGuid().ToString("N");
                string turn = activeTurn;
                Emit("turn/started", J.O("threadId", session, "turn", J.O("id", turn)));
                EmitItem("item/started", "thinking-" + turn, "reasoning");
                Task.Run(() => RunPrompt(session, turn, J.Get(p, "input")));
                return J.O("turn", J.O("id", turn));
            }
            if (method == "turn/interrupt") { cancelRequested = true; await NativeRequest("session/cancel", J.O("sessionId", J.S(p, "threadId")), timeoutMs); return J.O(); }
            throw new NotSupportedException("Harness 不支持此客户端操作：" + method);
        }
        private async Task RunPrompt(string session, string turn, object prompt) {
            string status = "completed", error = "";
            try { object result = await NativeRequest("session/prompt", J.O("sessionId", session, "prompt", prompt), 24 * 60 * 60 * 1000); string stop = J.S(result, "stopReason"); if (cancelRequested || stop == "cancelled") status = "interrupted"; else if (stop != "end_turn") { status = "failed"; error = "Harness 任务未完整完成：" + stop; } }
            catch (Exception e) { status = cancelRequested ? "interrupted" : "failed"; error = e.Message; }
            if (disposed || activeTurn != turn) return;
            if (reply.Length > 0) Emit("item/completed", J.O("threadId", session, "turnId", turn, "item", J.O("id", replyId, "type", "agentMessage", "text", reply.ToString())));
            activeTurn = ""; lock (gate) permissionIds.Clear();
            Emit("turn/completed", J.O("threadId", session, "turn", J.O("id", turn, "status", status, "error", J.O("message", error))));
        }
        private async Task<object> NativeRequest(string method, object p, int timeout) {
            int id = Interlocked.Increment(ref sequence); var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate) pending[id] = completion;
            try {
                Send(J.O("jsonrpc", "2.0", "id", id, "method", method, "params", p));
                if (await Task.WhenAny(completion.Task, Task.Delay(timeout)) != completion.Task) throw new TimeoutException("Harness 请求超时：" + method);
                return await completion.Task;
            } finally { lock (gate) pending.Remove(id); }
        }
        private async Task ReadLoop() {
            try {
                string line; while ((line = await process.StandardOutput.ReadLineAsync()) != null) {
                    if (!line.TrimStart().StartsWith("{", StringComparison.Ordinal)) continue;
                    object frame; try { frame = J.Parse(line); } catch { continue; }
                    if (J.S(frame, "jsonrpc") != "2.0") continue;
                    string method = J.S(frame, "method");
                    if (method.Length > 0) { Receive(method, J.Get(frame, "params"), J.Get(frame, "id")); continue; }
                    int id = J.I(frame, "id", -1); TaskCompletionSource<object> completion;
                    lock (gate) pending.TryGetValue(id, out completion);
                    if (completion != null) { object error = J.Get(frame, "error"); if (error != null) completion.TrySetException(new IOException(J.S(error, "message"))); else completion.TrySetResult(J.Get(frame, "result")); }
                }
            } catch (Exception e) { FailPending(e.Message); }
            finally { FailPending("Harness 连接已关闭。"); if (!disposed) { var handler = Disconnected; if (handler != null) handler(this, EventArgs.Empty); } }
        }
        private void Receive(string method, object p, object id) {
            if (id != null) {
                if (method == "session/request_permission" && J.S(p, "sessionId") == activeSession && activeTurn.Length > 0) {
                    string key = "harness-" + J.Str(id); lock (gate) permissionIds[key] = id;
                    object call = J.Get(p, "toolCall");
                    object details; if (toolCalls.TryGetValue(J.S(call, "toolCallId"), out details)) call = details;
                    Emit("item/commandExecution/requestApproval", J.O("threadId", activeSession, "turnId", activeTurn, "command", J.S(call, "title") + "\n" + J.Serialize(J.Get(call, "rawInput")), "reason", "DeepSeek Harness 请求本次操作权限", "options", J.Get(p, "options")), key);
                } else Send(J.O("jsonrpc", "2.0", "id", id, "error", J.O("code", -32601, "message", "Unsupported request")));
                return;
            }
            if (method != "session/update" || activeTurn.Length == 0 || J.S(p, "sessionId") != activeSession) return;
            object update = J.Get(p, "update"); string kind = J.S(update, "sessionUpdate");
            if (kind == "agent_thought_chunk") Emit("item/reasoning/summaryTextDelta", J.O("threadId", activeSession, "turnId", activeTurn, "itemId", "thinking-" + activeTurn));
            else if (kind == "agent_message_chunk") {
                object content = J.Get(update, "content");
                if (J.S(content, "type") == "text") { string text = J.S(content, "text"); reply.Append(text); Emit("item/agentMessage/delta", J.O("threadId", activeSession, "turnId", activeTurn, "itemId", replyId, "delta", text)); }
            } else if (kind == "tool_call" || kind == "tool_call_update") {
                string tool = J.S(update, "toolCallId");
                if (kind == "tool_call") toolCalls[tool] = update;
                string status = J.S(update, "status");
                if (kind == "tool_call" || status == "completed" || status == "failed") EmitItem(kind == "tool_call" ? "item/started" : "item/completed", tool, "dynamicToolCall");
            }
        }
        private void EmitItem(string method, string id, string type) { Emit(method, J.O("threadId", activeSession, "turnId", activeTurn, "item", J.O("id", id, "type", type))); }
        private void Emit(string method, object p, object id = null) { var handler = Event; if (handler != null) handler(this, new RpcEvent(method, p, id)); }
        public void Respond(object id, object result) {
            object original; string key = J.Str(id); lock (gate) { if (!permissionIds.TryGetValue(key, out original)) return; permissionIds.Remove(key); }
            // ACP's built-in permission vocabulary is one-shot only.
            string choice = J.S(result, "decision") == "accept" ? "allow-once" : "reject-once";
            Send(J.O("jsonrpc", "2.0", "id", original, "result", J.O("outcome", J.O("outcome", "selected", "optionId", choice))));
        }
        public void RejectUnsupported(object id) { Respond(id, J.O("decision", "decline")); }
        private void Send(object frame) { lock (writeGate) { if (!Connected) throw new IOException("Harness 未连接。"); input.WriteLine(J.Serialize(frame)); } }
        private void FailPending(string message) { lock (gate) foreach (var item in pending.Values) item.TrySetException(new IOException(message)); }
        public void Dispose() {
            if (disposed) return; disposed = true; lifetime.Cancel(); FailPending("Harness 连接已结束。");
            try { if (input != null) input.Close(); } catch { }
            try { if (process != null && !process.HasExited) process.WaitForExit(1500); } catch { }
            try { if (job != null) job.Dispose(); } catch { }
            try { if (process != null) { if (!process.HasExited) process.Kill(); process.Dispose(); } } catch { }
        }
    }
}
