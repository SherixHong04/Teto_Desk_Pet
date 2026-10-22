using System;
using System.Collections.Generic;

namespace TetoDesk {
    public sealed class CodexEffort {
        public string Id, Description;
    }
    public sealed class CodexModel {
        public string Id, Name, Effort;
        public bool IsDefault, SupportsImages;
        public readonly List<CodexEffort> Efforts = new List<CodexEffort>();
    }
    public static class ModelCatalog {
        public static List<CodexModel> Read(object response) {
            var models = new List<CodexModel>();
            foreach (object entry in J.A(J.Get(response, "data"))) {
                if (J.B(entry, "hidden")) continue;
                string id = J.S(entry, "model"); if (id.Length == 0) continue;
                object[] modalities = J.A(J.Get(entry, "inputModalities"));
                bool text = modalities.Length == 0, images = modalities.Length == 0;
                foreach (object modality in modalities) { text |= J.Str(modality) == "text"; images |= J.Str(modality) == "image"; }
                if (!text) continue;
                var model = new CodexModel { Id = id, Name = J.S(entry, "displayName"), Effort = J.S(entry, "defaultReasoningEffort"), IsDefault = J.B(entry, "isDefault"), SupportsImages = images };
                if (model.Name.Length == 0) model.Name = id;
                foreach (object option in J.A(J.Get(entry, "supportedReasoningEfforts"))) {
                    string effort = J.S(option, "reasoningEffort");
                    if (effort.Length > 0 && !model.Efforts.Exists(e => e.Id == effort)) model.Efforts.Add(new CodexEffort { Id = effort, Description = J.S(option, "description") });
                }
                // Older catalogs may only advertise the default effort.
                if (model.Efforts.Count == 0 && model.Effort.Length > 0) model.Efforts.Add(new CodexEffort { Id = model.Effort, Description = "模型默认强度" });
                models.Add(model);
            }
            return models;
        }
        public static CodexModel Select(IList<CodexModel> models, string preferred) {
            foreach (var model in models) if (model.Id == preferred) return model;
            foreach (var model in models) if (model.IsDefault) return model;
            if (models.Count > 0) return models[0];
            throw new InvalidOperationException("本机 Codex 未返回可用于文字任务的模型。请检查 ChatGPT 登录状态并更新 Codex CLI 后重试。");
        }
        public static string SelectEffort(CodexModel model, string preferred) {
            foreach (var effort in model.Efforts) if (effort.Id == preferred) return effort.Id;
            foreach (var effort in model.Efforts) if (effort.Id == model.Effort) return effort.Id;
            return model.Efforts.Count > 0 ? model.Efforts[0].Id : "";
        }
        public static string EffortLabel(string id) {
            switch (id) {
                case "off": case "none": return "不思考";
                case "minimal": return "极低";
                case "low": return "低";
                case "medium": return "中";
                case "high": return "高";
                case "xhigh": return "极高";
                case "max": return "最大";
                case "ultra": return "超高";
                default: return id.Length == 0 ? "默认" : id;
            }
        }
    }
}
