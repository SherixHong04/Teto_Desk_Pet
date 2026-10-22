using System;
using System.Collections.Generic;

namespace TetoDesk {
    public enum PetState { Idle, Connecting, Processing, Thinking, Working, Replying, Waiting, Completed, Interrupted, Failed }
    public sealed class TaskState {
        public string ThreadId = "", TurnId = "";
        public bool Busy;
        public PetState State = PetState.Idle;
        private readonly Dictionary<string, string> active = new Dictionary<string, string>();
        private readonly HashSet<string> approvals = new HashSet<string>();
        private bool statusWaiting;
        public bool Belongs(object p) {
            string thread = J.S(p, "threadId"), turn = J.S(p, "turnId");
            if (ThreadId.Length == 0 || (thread.Length > 0 && thread != ThreadId)) return false;
            if (TurnId.Length > 0 && turn.Length > 0 && turn != TurnId) return false;
            return true;
        }
        public void Begin(string thread) { ThreadId = thread; TurnId = ""; Busy = true; active.Clear(); approvals.Clear(); statusWaiting = false; State = PetState.Processing; }
        public void End(PetState state) { Busy = false; active.Clear(); approvals.Clear(); statusWaiting = false; State = state; }
        public void Approval(string id, bool pending) { if (pending) approvals.Add(id); else approvals.Remove(id); Recompute(); }
        public bool Apply(string method, object p) {
            if (!Belongs(p)) return false;
            var before = State;
            if (method == "turn/started") {
                var turn = J.Get(p, "turn");
                if (!Busy) return false;
                TurnId = J.S(turn, "id");
                State = PetState.Processing;
            } else if (method == "turn/completed") {
                var turn = J.Get(p, "turn");
                if (TurnId.Length > 0 && J.S(turn, "id") != TurnId) return false;
                string final = J.S(turn, "status");
                End(final == "completed" ? PetState.Completed : final == "interrupted" ? PetState.Interrupted : PetState.Failed);
            } else if (method == "thread/status/changed" && Busy) {
                statusWaiting = false;
                foreach (object flag in J.A(J.Get(J.Get(p, "status"), "activeFlags"))) {
                    string name = J.Str(flag); if (name == "waitingOnApproval" || name == "waitingOnUserInput") statusWaiting = true;
                }
                Recompute();
            } else if (Busy && (method == "item/started" || method == "item/completed")) {
                var item = J.Get(p, "item"); string id = J.S(item, "id");
                if (method == "item/started") active[id] = J.S(item, "type"); else active.Remove(id);
                Recompute();
            } else if (Busy && method.StartsWith("item/reasoning/", StringComparison.Ordinal)) {
                active[J.S(p, "itemId")] = "reasoning"; Recompute();
            } else if (Busy && method == "item/agentMessage/delta") {
                active[J.S(p, "itemId")] = "agentMessage"; Recompute();
            } else if (method == "error" && Busy && !J.B(p, "willRetry")) { End(PetState.Failed); }
            return before != State;
        }
        private void Recompute() {
            if (!Busy) return;
            if (approvals.Count > 0 || statusWaiting) { State = PetState.Waiting; return; }
            bool reasoning = false, reply = false;
            foreach (string type in active.Values) {
                if (type == "commandExecution" || type == "mcpToolCall" || type == "fileChange" || type == "webSearch" || type == "dynamicToolCall" || type == "collabAgentToolCall" || type == "imageGeneration" || type == "hook") { State = PetState.Working; return; }
                reasoning |= type == "reasoning"; reply |= type == "agentMessage";
            }
            State = reply ? PetState.Replying : reasoning ? PetState.Thinking : PetState.Processing;
        }
        public static string Label(PetState state) {
            switch (state) {
                case PetState.Connecting: return "连接中";
                case PetState.Processing: return "处理中";
                case PetState.Thinking: return "思考中";
                case PetState.Working: return "执行中";
                case PetState.Replying: return "回复中";
                case PetState.Waiting: return "等你确认";
                case PetState.Completed: return "已完成";
                case PetState.Interrupted: return "已停止";
                case PetState.Failed: return "遇到问题";
                default: return "待机";
            }
        }
        public static string Animation(PetState state) {
            switch (state) {
                case PetState.Connecting: case PetState.Waiting: return "waiting";
                case PetState.Processing: case PetState.Thinking: case PetState.Working: return "running";
                case PetState.Replying: return "review";
                case PetState.Completed: return "waving";
                case PetState.Failed: return "failed";
                default: return "idle";
            }
        }
    }
}
