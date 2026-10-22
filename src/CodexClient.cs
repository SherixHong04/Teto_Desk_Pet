using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace TetoDesk {
    public sealed class RpcEvent : EventArgs {
        public string Method; public object Params, Id;
        public RpcEvent(string method, object p, object id) { Method = method; Params = p; Id = id; }
    }
    public sealed class CodexClient : IConversationClient {
        public event EventHandler<RpcEvent> Event;
        public event EventHandler Disconnected;
        private Process process;
        private StreamWriter input;
        private ProcessJob job;
        private int sequence;
        private readonly object gate = new object(), writeGate = new object();
        private readonly Dictionary<string, TaskCompletionSource<object>> pending = new Dictionary<string, TaskCompletionSource<object>>();
        private bool disposed, intentional;
        private string lastDiagnostic = "";
        private string verifiedWorkspace = "";
        public string AccountLabel { get; private set; }
        public string Executable { get; private set; }
        private readonly List<CodexModel> models = new List<CodexModel>();
        private readonly string configuredExecutable;
        public CodexClient(string executable = null) { configuredExecutable = executable; }
        public List<CodexModel> Models { get { return models; } }
        public bool Connected { get { return process != null && !process.HasExited && !disposed; } }
        public static string FindExecutable() {
            string found = ProviderDiscovery.Find("codex", ProviderDiscovery.ConfiguredExecutable("codex"));
            if (found.Length > 0) return found;
            throw new InvalidOperationException("找不到 Codex CLI。请先安装并使用 ChatGPT 登录，或设置 TETO_CODEX_EXE 为 codex.exe 的完整路径。");
        }
        public async Task ConnectAsync(string workingDirectory) {
            if (Connected) return;
            disposed = false; intentional = false; verifiedWorkspace = "";
            Executable = configuredExecutable ?? FindExecutable();
            // MXC avoids legacy setup refresh touching a desktop runtime that
            // may already be in use. Unsupported devices keep the configured
            // legacy sandbox; managed permissions and approvals still apply.
            var info = new ProcessStartInfo(Executable, "app-server --listen stdio:// -c model_provider=\"openai\" -c features.prefer_mxc=true") {
                WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false)
            };
            // The app uses the saved ChatGPT login only, never an inherited API key.
            foreach (string key in new[] { "OPENAI_API_KEY", "CODEX_API_KEY", "CODEX_ACCESS_TOKEN" }) info.EnvironmentVariables.Remove(key);
            process = new Process { StartInfo = info, EnableRaisingEvents = true };
            process.Start();
            input = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
            job = new ProcessJob(); job.Attach(process);
            Task.Run((Func<Task>)ReadLoop);
            Task.Run((Func<Task>)ReadDiagnostics);
            try {
                await RequestAsync("initialize", J.O("clientInfo", J.O("name", "teto_desk_local", "title", "Teto Desk", "version", "0.1.18")), 30000);
                Send(J.O("method", "initialized", "params", J.O()));
                object accountResult = await RequestAsync("account/read", J.O("refreshToken", false), 15000);
                object account = J.Get(accountResult, "account");
                if (J.S(account, "type") != "chatgpt") throw new InvalidOperationException("当前 Codex 没有使用 ChatGPT 账号登录。本程序仅使用 ChatGPT 登录，请在终端运行 codex login 后重试。");
                AccountLabel = "ChatGPT · " + (J.S(account, "planType").Length > 0 ? J.S(account, "planType") : "已登录");
                string cursor = "";
                Models.Clear();
                for (int page = 0; page < 10; page++) {
                    var parameters = J.O("limit", 100, "includeHidden", false);
                    if (cursor.Length > 0) parameters["cursor"] = cursor;
                    object list = await RequestAsync("model/list", parameters, 30000);
                    Models.AddRange(ModelCatalog.Read(list));
                    string next = J.S(list, "nextCursor");
                    if (next.Length == 0 || next == cursor) break;
                    cursor = next;
                }
                ModelCatalog.Select(Models, "");
            } catch { Dispose(); throw; }
        }
        public async Task VerifyWorkspaceToolsAsync(string workingDirectory) {
            if (!Connected) throw new InvalidOperationException("Codex 尚未连接，请重新连接后检查本地文件工具。");
            string workspace = Path.GetFullPath(workingDirectory);
            if (String.Equals(verifiedWorkspace, workspace, StringComparison.OrdinalIgnoreCase)) return;
            string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            try {
                // Exercise the actual command tool without inference or file
                // changes. Verify each selected workspace once per connection.
                object result = await RequestAsync("command/exec", J.O(
                    "command", new[] { shell, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "if (Test-Path -LiteralPath (Get-Location).Path -PathType Container) { [Console]::Write('TETO_LOCAL_TOOLS_READY') } else { exit 1 }" },
                    "cwd", workspace, "sandboxPolicy", J.O("type", "workspaceWrite", "writableRoots", new[] { workspace }, "networkAccess", false),
                    "timeoutMs", 15000, "outputBytesCap", 4096), 30000);
                if (J.Str(J.Get(result, "exitCode")) != "0" || J.S(result, "stdout").Trim() != "TETO_LOCAL_TOOLS_READY")
                    throw new InvalidOperationException(J.S(result, "stderr").Length > 0 ? J.S(result, "stderr") : "本地命令启动检查未通过。");
                verifiedWorkspace = workspace;
            } catch (Exception e) {
                string detail = e.Message; if (detail.Length > 1200) detail = detail.Substring(0, 1200);
                throw new InvalidOperationException("本地文件工具暂时不可用，消息尚未发送。请确认所选工作文件夹可访问，并检查 Codex CLI 和 Windows 沙箱配置。\n" + detail, e);
            }
        }
        private async Task ReadLoop() {
            try {
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync()) != null) {
                    object message;
                    try { message = J.Parse(line); } catch { continue; }
                    string method = J.S(message, "method"); object id = J.Get(message, "id");
                    if (method.Length > 0) {
                        var handler = Event; if (handler != null) handler(this, new RpcEvent(method, J.Get(message, "params"), id));
                    } else if (id != null) {
                        TaskCompletionSource<object> completion = null;
                        lock (gate) { string key = J.Str(id); if (pending.TryGetValue(key, out completion)) pending.Remove(key); }
                        if (completion != null) {
                            object error = J.Get(message, "error");
                            if (error != null) completion.TrySetException(new InvalidOperationException(J.S(error, "message")));
                            else completion.TrySetResult(J.Get(message, "result"));
                        }
                    }
                }
            } catch (Exception e) { if (!intentional) lastDiagnostic = e.Message; }
            finally {
                FailPending("Codex 连接已关闭。" + (lastDiagnostic.Length > 0 ? " " + lastDiagnostic : ""));
                if (!intentional) { var handler = Disconnected; if (handler != null) handler(this, EventArgs.Empty); }
            }
        }
        private async Task ReadDiagnostics() {
            try { string line; while ((line = await process.StandardError.ReadLineAsync()) != null) { if (line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0) lastDiagnostic = line.Length > 400 ? line.Substring(0, 400) : line; } } catch { }
        }
        public async Task<object> RequestAsync(string method, object p, int timeoutMs) {
            int id = Interlocked.Increment(ref sequence);
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate) pending[id.ToString()] = completion;
            try {
                Send(J.O("id", id, "method", method, "params", p));
                if (await Task.WhenAny(completion.Task, Task.Delay(timeoutMs)) != completion.Task) throw new TimeoutException("Codex 连接超时，请重试。");
                return await completion.Task;
            } finally { lock (gate) pending.Remove(id.ToString()); }
        }
        public void Respond(object id, object result) { Send(J.O("id", id, "result", result)); }
        public void RejectUnsupported(object id) { Send(J.O("id", id, "error", J.O("code", -32601, "message", "This local client does not support this request."))); }
        private void Send(object message) {
            lock (writeGate) {
                if (!Connected) throw new InvalidOperationException("Codex 尚未连接。");
                input.WriteLine(J.Serialize(message)); input.Flush();
            }
        }
        private void FailPending(string message) {
            lock (gate) { foreach (var p in pending.Values) p.TrySetException(new InvalidOperationException(message)); pending.Clear(); }
        }
        public void Dispose() {
            intentional = true; disposed = true; FailPending("Codex 连接已结束。");
            try { if (job != null) { job.Dispose(); job = null; } } catch { }
            try { if (process != null) { if (!process.HasExited) process.Kill(); process.Dispose(); process = null; } } catch { }
        }
    }
    // Close the private app-server and its child processes together when the pet exits.
    internal sealed class ProcessJob : IDisposable {
        private IntPtr handle;
        public ProcessJob() {
            handle = CreateJobObject(IntPtr.Zero, null); if (handle == IntPtr.Zero) return;
            var info = new ExtendedLimit(); info.BasicLimitInformation.LimitFlags = 0x2000;
            int length = Marshal.SizeOf(typeof(ExtendedLimit)); IntPtr ptr = Marshal.AllocHGlobal(length);
            try { Marshal.StructureToPtr(info, ptr, false); if (!SetInformationJobObject(handle, 9, ptr, (uint)length)) Dispose(); } finally { Marshal.FreeHGlobal(ptr); }
        }
        public void Attach(Process process) { if (handle != IntPtr.Zero && !AssignProcessToJobObject(handle, process.Handle)) Dispose(); }
        public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimit { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimit { public BasicLimit BasicLimitInformation; public IoCounters IoInfo; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);
        [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
