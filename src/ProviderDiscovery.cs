using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace TetoDesk {
    internal sealed class DiscoverySystem {
        internal string User = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        internal string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        internal string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        internal string Programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        internal string Programs32 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        internal Func<string, string> EnvironmentValue = Environment.GetEnvironmentVariable;
        internal Func<string, bool> Exists = File.Exists;
        internal Func<string, string[]> Directories = delegate(string path) { try { return Directory.GetDirectories(path); } catch { return new string[0]; } };
        internal Func<string, DateTime> Modified = delegate(string path) { try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; } };
        internal Func<string, string[]> Registered = RegistryPaths;
        private static string[] RegistryPaths(string provider) {
            var paths = new List<string>();
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) {
                try {
                    using (var root = RegistryKey.OpenBaseKey(hive, view)) {
                        string exe = provider == "codex" ? "codex.exe" : "DeepSeek Harness.exe";
                        using (var app = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + exe)) {
                            if (app != null) paths.Add(Convert.ToString(app.GetValue(null)).Trim('"'));
                        }
                        if (provider != "deepseek") continue;
                        using (var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall")) {
                            if (uninstall == null) continue;
                            foreach (string name in uninstall.GetSubKeyNames()) using (var item = uninstall.OpenSubKey(name)) {
                                if (item == null) continue;
                                string title = Convert.ToString(item.GetValue("DisplayName"));
                                if (title.IndexOf("DeepSeek Harness", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                string location = Convert.ToString(item.GetValue("InstallLocation")).Trim('"');
                                if (location.Length > 0) paths.Add(Path.Combine(location, exe));
                                string icon = Convert.ToString(item.GetValue("DisplayIcon")).Trim();
                                if (icon.StartsWith("\"")) { int end = icon.IndexOf('"', 1); if (end > 1) icon = icon.Substring(1, end - 1); }
                                else if (icon.LastIndexOf(',') > 0) icon = icon.Substring(0, icon.LastIndexOf(','));
                                paths.Add(icon);
                            }
                        }
                    }
                } catch { /* Registry visibility varies between machines. */ }
            }
            return paths.ToArray();
        }
    }
    public static class ProviderDiscovery {
        private static void AddNpmCandidates(List<string> candidates, string packages) {
            foreach (string arch in new[] { "x64", "arm64" }) {
                string triple = arch == "x64" ? "x86_64-pc-windows-msvc" : "aarch64-pc-windows-msvc";
                foreach (string prefix in new[] { Path.Combine(packages, "codex", "node_modules", "@openai"), packages })
                    candidates.Add(Path.Combine(prefix, "codex-win32-" + arch, "vendor", triple, "bin", "codex.exe"));
                candidates.Add(Path.Combine(packages, "codex", "vendor", triple, "bin", "codex.exe"));
            }
        }
        public static bool IsExecutable(string path) { return IsExecutable(path, new DiscoverySystem()); }
        internal static bool IsExecutable(string path, DiscoverySystem system) {
            try { return !String.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && system.Exists(path); } catch { return false; }
        }
        internal static bool IsHarness(string path, DiscoverySystem system) {
            return IsExecutable(path, system) && system.Exists(Path.Combine(Path.GetDirectoryName(path), "resources", "app.asar"));
        }
        public static string Find(string provider, string configured) { return Find(provider, configured, new DiscoverySystem()); }
        internal static string Find(string provider, string configured, DiscoverySystem system) {
            if (provider != "codex" && provider != "deepseek") throw new ArgumentException("Unknown provider.");
            var candidates = new List<string>();
            candidates.Add(configured);
            candidates.Add(system.EnvironmentValue(provider == "codex" ? "TETO_CODEX_EXE" : "TETO_HARNESS_EXE"));
            string exe = provider == "codex" ? "codex.exe" : "DeepSeek Harness.exe";
            foreach (string segment in (system.EnvironmentValue("PATH") ?? "").Split(';')) {
                if (!String.IsNullOrWhiteSpace(segment)) try {
                    string directory = segment.Trim().Trim('"'); candidates.Add(Path.Combine(directory, exe));
                    if (provider == "codex") AddNpmCandidates(candidates, Path.Combine(directory, "node_modules", "@openai"));
                } catch { }
            }
            if (provider == "codex") {
                candidates.Add(Path.Combine(system.User, ".local", "bin", exe));
                candidates.Add(Path.Combine(system.Local, "Microsoft", "WinGet", "Links", exe));
                AddNpmCandidates(candidates, Path.Combine(system.Roaming, "npm", "node_modules", "@openai"));
                foreach (string product in new[] { "Codex", "ChatGPT" }) {
                    string bundled = Path.Combine(system.Local, "OpenAI", product, "bin");
                    candidates.Add(Path.Combine(bundled, exe));
                    string[] versions = system.Directories(bundled);
                    Array.Sort(versions, delegate(string a, string b) { return system.Modified(Path.Combine(b, exe)).CompareTo(system.Modified(Path.Combine(a, exe))); });
                    foreach (string version in versions) candidates.Add(Path.Combine(version, exe));
                }
            } else {
                foreach (string root in new[] { Path.Combine(system.Local, "Programs"), system.Programs, system.Programs32 })
                    foreach (string name in new[] { "DeepSeek Harness", "deepseek-harness" }) candidates.Add(Path.Combine(root, name, exe));
            }
            candidates.AddRange(system.Registered(provider));
            foreach (string path in candidates) {
                if (provider == "deepseek" ? IsHarness(path, system) : IsExecutable(path, system)) return Path.GetFullPath(path);
            }
            return "";
        }
        public static string Classify(string path) {
            string name = Path.GetFileName(path);
            if (name.IndexOf("codex", StringComparison.OrdinalIgnoreCase) >= 0 && IsExecutable(path)) return "codex";
            if (name.IndexOf("deepseek", StringComparison.OrdinalIgnoreCase) >= 0 && IsHarness(path, new DiscoverySystem())) return "deepseek";
            throw new IOException("请选择 Codex CLI 的 codex.exe，或保留 resources 目录的 DeepSeek Harness.exe。");
        }
        internal static object ReadSettings(string path) { try { return File.Exists(path) ? J.Parse(File.ReadAllText(path)) : null; } catch { return null; } }
        public static string ConfiguredExecutable(string provider) {
            return J.S(ReadSettings(AppPaths.ProviderSettings), provider == "codex" ? "codexExecutable" : "harnessExecutable");
        }
    }
    public sealed class ProviderCatalog {
        private readonly string path;
        private readonly DiscoverySystem system;
        private object settings;
        private readonly Dictionary<string, string> paths = new Dictionary<string, string>();
        public ProviderCatalog(string settingsPath) : this(settingsPath, new DiscoverySystem()) { }
        internal ProviderCatalog(string settingsPath, DiscoverySystem system) { path = settingsPath; this.system = system; Refresh(); }
        public string[] Available { get { var result = new List<string>(); foreach (string id in new[] { "deepseek", "codex" }) if (Contains(id)) result.Add(id); return result.ToArray(); } }
        public bool Contains(string id) { return id != null && paths.ContainsKey(id); }
        public string Executable(string id) { return Contains(id) ? paths[id] : ""; }
        public void Refresh() {
            settings = ProviderDiscovery.ReadSettings(path); paths.Clear();
            foreach (string id in new[] { "deepseek", "codex" }) {
                string found = ProviderDiscovery.Find(id, J.S(settings, id == "codex" ? "codexExecutable" : "harnessExecutable"), system);
                if (found.Length > 0) paths[id] = found;
            }
        }
        public string Preferred(string previous) {
            if (Contains(previous)) return previous;
            string saved = J.S(settings, "backend"); if (Contains(saved)) return saved;
            return Available.Length == 0 ? "" : Available[0];
        }
        public void SaveSelection(string backend) {
            settings = J.O("backend", backend, "codexExecutable", J.S(settings, "codexExecutable"), "harnessExecutable", J.S(settings, "harnessExecutable"));
            J.Save(path, settings);
        }
        public void Configure(string executable) {
            string provider = ProviderDiscovery.Classify(executable);
            settings = J.O("backend", J.S(settings, "backend"), "codexExecutable", provider == "codex" ? Path.GetFullPath(executable) : J.S(settings, "codexExecutable"), "harnessExecutable", provider == "deepseek" ? Path.GetFullPath(executable) : J.S(settings, "harnessExecutable"));
            J.Save(path, settings); Refresh();
        }
    }
}
