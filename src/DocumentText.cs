using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TetoDesk {
    public static class DocumentText {
        public const int MaximumChars = 60000;
        private const string TextExtensions = "|.txt|.md|.csv|.tsv|.json|.xml|.yaml|.yml|.log|.ini|.cfg|.rst|.sql|.html|.htm|.cs|.py|.js|.ts|.tsx|.jsx|.css|.cpp|.c|.h|.java|.ps1|.sh|.ipynb|";
        public static int PrefixLength(string text, int maximum) { int count = Math.Min(text.Length, maximum); return count > 0 && count < text.Length && Char.IsHighSurrogate(text[count - 1]) ? count - 1 : count; }
        public static void Extract(ChatAttachment file) {
            if (file.IsImage) return;
            string extension = Path.GetExtension(file.Path).ToLowerInvariant(), text = "";
            try {
                if (TextExtensions.Contains("|" + extension + "|")) {
                    byte[] bytes;
                    using (var input = File.OpenRead(file.Path)) { bytes = new byte[Math.Min(input.Length, 1024 * 1024)]; int offset = 0, read; while (offset < bytes.Length && (read = input.Read(bytes, offset, bytes.Length - offset)) > 0) offset += read; }
                    if (bytes.Length >= 2 && ((bytes[0] == 255 && bytes[1] == 254) || (bytes[0] == 254 && bytes[1] == 255))) text = (bytes[0] == 255 ? Encoding.Unicode : Encoding.BigEndianUnicode).GetString(bytes, 2, bytes.Length - 2);
                    else {
                        bool decoded = false;
                        for (int trim = 0; trim <= (file.Size > bytes.Length ? 3 : 0); trim++) {
                            try { text = new UTF8Encoding(false, true).GetString(bytes, 0, bytes.Length - trim); decoded = true; break; } catch (DecoderFallbackException) { }
                        }
                        if (!decoded) text = Encoding.GetEncoding("GB18030").GetString(bytes);
                    }
                    if (text.IndexOf('\0') >= 0) { file.ReadHint = "需要文件工具读取"; return; }
                    text = text.TrimStart('\uFEFF');
                    if (file.Size > bytes.Length && text.Length <= MaximumChars) text += "\n[文件较大，以上仅为部分内容]";
                } else if (extension == ".docx" || extension == ".xlsx" || extension == ".pptx") text = Office(file.Path, extension);
                else if (extension == ".pdf") text = Pdf(file.Path);
                if (String.IsNullOrWhiteSpace(text)) { file.ReadHint = extension == ".pdf" ? "未提取到文字，需文件工具或 OCR" : "需要文件工具读取"; return; }
                bool truncated = text.Length > MaximumChars;
                file.ExtractedText = truncated ? text.Substring(0, PrefixLength(text, MaximumChars)) + "\n[文字较多，仅提供前 60000 字符；完整内容见原文件副本]" : text;
                file.ReadHint = truncated ? "已提取部分文字" : "已提取文字";
            } catch { file.ReadHint = "未提取到文字，需要文件工具读取"; }
        }
        private static XDocument Xml(ZipArchiveEntry entry) {
            if (entry == null) return null;
            if (entry.Length > 12 * 1024 * 1024) throw new IOException("Office XML is too large");
            using (var stream = entry.Open()) using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 12 * 1024 * 1024 })) return XDocument.Load(reader);
        }
        private static string Paragraphs(XDocument document) {
            if (document == null) return "";
            var text = new StringBuilder();
            foreach (var paragraph in document.Descendants().Where(e => e.Name.LocalName == "p")) {
                foreach (var run in paragraph.Descendants().Where(e => e.Name.LocalName == "t")) text.Append(run.Value);
                text.AppendLine(); if (text.Length > MaximumChars) break;
            }
            return text.ToString();
        }
        private static int Number(ZipArchiveEntry entry) { int n; string name = Path.GetFileNameWithoutExtension(entry.FullName); return Int32.TryParse(new string(name.Where(Char.IsDigit).ToArray()), out n) ? n : 0; }
        private static string Office(string path, string extension) {
            using (var zip = ZipFile.OpenRead(path)) {
                if (extension == ".docx") return Paragraphs(Xml(zip.GetEntry("word/document.xml")));
                var result = new StringBuilder();
                if (extension == ".pptx") {
                    foreach (var slide in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).OrderBy(Number).Take(500)) {
                        result.AppendLine("[幻灯片 " + Number(slide) + "]"); result.Append(Paragraphs(Xml(slide))); if (result.Length > MaximumChars) break;
                    }
                } else {
                    var shared = new List<string>(); var strings = Xml(zip.GetEntry("xl/sharedStrings.xml"));
                    if (strings != null) foreach (var entry in strings.Descendants().Where(e => e.Name.LocalName == "si")) shared.Add(String.Concat(entry.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value)));
                    foreach (var sheet in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).OrderBy(Number).Take(500)) {
                        result.AppendLine("[工作表 " + Number(sheet) + "]"); var document = Xml(sheet);
                        foreach (var row in document.Descendants().Where(e => e.Name.LocalName == "row")) {
                            foreach (var cell in row.Elements().Where(e => e.Name.LocalName == "c")) {
                                string type = (string)cell.Attribute("t"), value = String.Concat(cell.Descendants().Where(e => e.Name.LocalName == (type == "inlineStr" ? "t" : "v")).Select(e => e.Value)); int index;
                                if (type == "s" && Int32.TryParse(value, out index) && index >= 0 && index < shared.Count) value = shared[index];
                                result.Append((string)cell.Attribute("r")).Append('=').Append(value).Append('\t');
                            }
                            result.AppendLine(); if (result.Length > MaximumChars) break;
                        }
                        if (result.Length > MaximumChars) break;
                    }
                }
                return result.ToString();
            }
        }
        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        private static string Pdf(string path) {
            string runtime = Environment.GetEnvironmentVariable("TETO_PYTHON_EXE");
            if (!ProviderDiscovery.IsExecutable(runtime)) {
                runtime = "";
                foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
                    if (directory.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) >= 0 || String.IsNullOrWhiteSpace(directory)) continue;
                    string candidate; try { candidate = Path.Combine(directory.Trim('"'), "python.exe"); } catch { continue; }
                    if (ProviderDiscovery.IsExecutable(candidate)) { runtime = candidate; break; }
                }
            }
            if (runtime.Length == 0) return "";
            string helper = Path.Combine(Path.GetDirectoryName(path), ".teto-extract-pdf.py"), output = helper + ".json";
            const string code = "import json, sys\nfrom pypdf import PdfReader\nr=PdfReader(sys.argv[1])\nparts=[]\nn=0\nhas_text=False\nfor i,p in enumerate(r.pages):\n s=p.extract_text() or ''\n has_text=has_text or bool(s.strip())\n parts.append('[page %d]\\n%s' % (i+1,s))\n n+=len(s)\n if n>60000: break\nwith open(sys.argv[2], 'w', encoding='utf-8') as f: json.dump({'text': '\\n'.join(parts), 'hasText': has_text},f,ensure_ascii=False)\n";
            File.WriteAllText(helper, code, new UTF8Encoding(false));
            try {
                using (var process = new Process { StartInfo = new ProcessStartInfo(runtime, "-I " + Quote(helper) + " " + Quote(path) + " " + Quote(output)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } }) using (var job = new ProcessJob()) {
                    process.Start(); job.Attach(process); process.StandardOutput.ReadToEndAsync(); process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(20000)) { process.Kill(); return ""; }
                    if (process.ExitCode != 0 || !File.Exists(output)) return "";
                    object value = J.Parse(File.ReadAllText(output)); return J.B(value, "hasText") ? J.S(value, "text") : "";
                }
            } finally { if (File.Exists(helper)) File.Delete(helper); if (File.Exists(output)) File.Delete(output); }
        }
    }
}
