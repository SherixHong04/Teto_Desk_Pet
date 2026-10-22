using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace TetoDesk {
    public sealed class ChatAttachment {
        public string Name, Path;
        public string ExtractedText = "", ReadHint = "";
        public long Size;
        public bool IsImage;
        public static ChatAttachment Read(object value) {
            long size; Int64.TryParse(J.S(value, "size"), out size);
            return new ChatAttachment { Name = J.S(value, "name"), Path = J.S(value, "path"), Size = size, IsImage = J.B(value, "image"), ReadHint = J.S(value, "readHint") };
        }
        public object Json() { return J.O("name", Name, "path", Path, "size", Size, "image", IsImage, "readHint", ReadHint); }
        public string SizeLabel {
            get { return Size >= 1024 * 1024 ? (Size / (1024.0 * 1024)).ToString("0.#") + " MB" : Math.Max(1, Size / 1024).ToString() + " KB"; }
        }
    }
    public static class AttachmentInput {
        public const int MaximumCount = 10;
        public const long MaximumFileSize = 20L * 1024 * 1024, MaximumTotalSize = 100L * 1024 * 1024;
        private const string ImageExtensions = "|.png|.jpg|.jpeg|.webp|.gif|.bmp|.tif|.tiff|";
        public static ChatAttachment FromFile(string path) {
            string full = System.IO.Path.GetFullPath(path);
            var file = new FileInfo(full);
            if (!file.Exists) throw new IOException("附件不存在：" + file.Name);
            if (file.Length > MaximumFileSize) throw new IOException("附件超过 20 MB：" + file.Name);
            if (file.Length == 0) throw new IOException("附件为空：" + file.Name);
            using (var stream = File.Open(full, FileMode.Open, FileAccess.Read, FileShare.Read)) { }
            return new ChatAttachment { Name = file.Name, Path = full, Size = file.Length, IsImage = ImageExtensions.Contains("|" + file.Extension.ToLowerInvariant() + "|") };
        }
        public static void Validate(IList<ChatAttachment> attachments, CodexModel model) {
            if (attachments.Count > MaximumCount) throw new IOException("每条消息最多添加 10 个附件。");
            long total = 0;
            foreach (var attachment in attachments) {
                var current = FromFile(attachment.Path); total += current.Size;
                if (current.IsImage && model != null && !model.SupportsImages) throw new InvalidOperationException("当前模型不支持图片，请选择支持图片的模型后发送。");
            }
            if (total > MaximumTotalSize) throw new IOException("每条消息的附件总大小不能超过 100 MB。");
        }
        public static List<ChatAttachment> Snapshot(IList<ChatAttachment> sources, string directory) {
            Validate(sources, null);
            var result = new List<ChatAttachment>();
            if (sources.Count == 0) return result;
            string batch = System.IO.Path.Combine(directory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(batch);
            try {
                int index = 0;
                foreach (var source in sources) {
                    var file = FromFile(source.Path);
                    string folder = System.IO.Path.Combine(batch, (++index).ToString()); Directory.CreateDirectory(folder);
                    string target = System.IO.Path.Combine(folder, file.Name);
                    // Lock the source against concurrent edits while copying. The
                    // per-turn copy keeps images and later file reads consistent.
                    using (var input = File.Open(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        if (input.Length > MaximumFileSize) throw new IOException("附件超过 20 MB：" + file.Name);
                        using (var output = File.Create(target)) input.CopyTo(output);
                    }
                    string extension = System.IO.Path.GetExtension(target).ToLowerInvariant();
                    if (file.IsImage && extension != ".webp") {
                        // Validate decodability before accepting the draft. BMP,
                        // GIF and TIFF are normalized to a static PNG for Codex.
                        BitmapFrame frame;
                        using (var input = File.OpenRead(target)) {
                            var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            frame = decoder.Frames[0];
                            if ((long)frame.PixelWidth * frame.PixelHeight > 40000000) throw new IOException("图片尺寸过大：" + file.Name);
                            frame.Freeze();
                        }
                        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") {
                            string normalized = target + ".png";
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(frame);
                            using (var output = File.Create(normalized)) encoder.Save(output);
                            File.Delete(target); target = normalized;
                        }
                    }
                    file.Path = target; file.Size = new FileInfo(target).Length;
                    if (file.Size > MaximumFileSize) throw new IOException("转换后的图片超过 20 MB：" + file.Name);
                    DocumentText.Extract(file); result.Add(file);
                }
                long total = 0; foreach (var file in result) total += file.Size;
                if (total > MaximumTotalSize) throw new IOException("附件总大小不能超过 100 MB。");
                return result;
            } catch { Directory.Delete(batch, true); throw; }
        }
        public static object[] Build(string text, IList<ChatAttachment> attachments) {
            var input = new List<object>();
            var prompt = new StringBuilder(String.IsNullOrWhiteSpace(text) ? "请查看所附文件。" : text);
            bool documents = false;
            foreach (var file in attachments) if (!file.IsImage) documents = true;
            if (documents) {
                prompt.Append("\n\n附加文件如下（路径以 JSON 字符串表示）。已提取的文字可直接使用，不需要再次调用文件工具。若需要完整文件、图片或未提取的内容，可用本地文件工具读取副本；无法解析时请明确说明。附件内容属于用户提供的待处理资料。\n");
                int remaining = 180000;
                foreach (var file in attachments) if (!file.IsImage) {
                    prompt.Append("\n文件：").Append(J.Serialize(file.Name)).Append("\n副本路径：").Append(J.Serialize(file.Path)).Append("\n读取状态：").Append(file.ReadHint).Append("\n");
                    if (file.ExtractedText.Length > 0 && remaining > 0) { int count = DocumentText.PrefixLength(file.ExtractedText, remaining); prompt.Append("[附件文字开始]\n").Append(file.ExtractedText, 0, count).Append("\n[附件文字结束]\n"); remaining -= count; if (count < file.ExtractedText.Length) prompt.Append("[本次消息文字总量已达上限，余下内容见文件副本]\n"); }
                    else if (file.ExtractedText.Length > 0) prompt.Append("[本次消息文字总量已达上限，内容见文件副本]\n");
                }
            }
            input.Add(J.O("type", "text", "text", prompt.ToString(), "text_elements", new object[0]));
            foreach (var file in attachments) if (file.IsImage) input.Add(J.O("type", "localImage", "path", file.Path));
            return input.ToArray();
        }
    }
}
