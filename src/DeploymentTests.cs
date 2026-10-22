using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TetoDesk {
    public static partial class TestProgram {
        private static DiscoverySystem IsolatedSystem(string fixture, string searchPath, Func<string, string[]> registered) {
            return new DiscoverySystem {
                User = Path.Combine(fixture, "用户"), Local = Path.Combine(fixture, "Local"), Roaming = Path.Combine(fixture, "Roaming"),
                Programs = Path.Combine(fixture, "Programs"), Programs32 = Path.Combine(fixture, "Programs32"),
                EnvironmentValue = key => key == "PATH" ? searchPath : null,
                Registered = registered ?? (key => new string[0])
            };
        }
        private static void FakeExecutable(string path, bool harness) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, new byte[] { 77, 90 });
            if (harness) { string resources = Path.Combine(Path.GetDirectoryName(path), "resources"); Directory.CreateDirectory(resources); File.WriteAllText(Path.Combine(resources, "app.asar"), "fixture only; never execute"); }
        }
        private static void DeploymentChecks() {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
            string run = Path.Combine(root, "run-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(run);
            try {
                foreach (string scenario in new[] { "codex-only", "deepseek-only", "both" }) {
                    string fixture = Path.Combine(run, scenario), bin = Path.Combine(fixture, "客户端 空格");
                    string codex = Path.Combine(bin, "codex.exe"), harness = Path.Combine(bin, "DeepSeek Harness.exe");
                    if (scenario != "deepseek-only") FakeExecutable(codex, false);
                    if (scenario != "codex-only") FakeExecutable(harness, true);
                    var system = IsolatedSystem(fixture, "\"" + bin + "\"", null);
                    string settings = Path.Combine(fixture, "Data", "providers.json");
                    var providers = new ProviderCatalog(settings, system);
                    Assert(providers.Available.Length == (scenario == "both" ? 2 : 1), "Discovery lists exactly installed clients: " + scenario);
                    using (var c = new Controller(Path.Combine(fixture, "Data", "session.json"), Path.Combine(fixture, "Tasks"), a => a(), providers)) {
                        string expected = scenario == "codex-only" ? "codex" : "deepseek";
                        Assert(c.Backend == expected && c.HasAvailableBackend, "First launch chooses an available client: " + scenario);
                        var pet = new PetWindow(new Sprites()); var chat = new ChatWindow(c, pet); string tray = ""; int queries = 0;
                        var quota = QuotaSnapshot.Read(P("rateLimits", P("primary", P("usedPercent", 25, "windowDurationMins", 300, "resetsAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()+900), "secondary", P("usedPercent", 35, "windowDurationMins", 10080, "resetsAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()+86400))));
                        var balance = BalanceSnapshot.Read(P("status", "ready", "value", new[] { P("currency", "CNY", "balance", "12.345") }));
                        using (var query = new AccountQuery(c, pet, label => tray = label, () => {
                            queries++; return Task.FromResult(new AccountQueryResult { Backend = c.Backend, Quota = quota, Balance = balance });
                        })) {
                            var menu = chat.BuildProviderMenu();
                            var choices = menu.Items.OfType<MenuItem>().Where(m => m.IsCheckable).ToArray();
                            Assert(choices.Length == providers.Available.Length && choices.Count(m => m.IsChecked) == 1, "The actual UI menu contains only detected services: " + scenario);
                            if (scenario != "both") {
                                string missing = scenario == "codex-only" ? "deepseek" : "codex";
                                Assert(!c.SetBackend(missing) && c.Backend == expected, "An unavailable service cannot be selected: " + scenario);
                                Assert(!choices.Any(m => Convert.ToString(m.Header).Contains(missing == "codex" ? "ChatGPT" : "DeepSeek")), "The unavailable provider is absent from CLI choices: " + scenario);
                            }
                            foreach (string id in providers.Available) {
                                c.SetBackend(id); PumpAccount(query.RunAsync());
                                Assert(tray == c.AccountQueryLabel && pet.QueryMenuText == tray, "Pet and tray query captions follow selection: " + scenario + "/" + id);
                                Assert(id == "codex" ? pet.QuotaBubble.FiveRemainingText == "剩余 75%" && pet.QuotaBubble.WeekRemainingText == "剩余 65%" : pet.QuotaBubble.Title.Contains("DeepSeek"), "The query presenter uses the selected provider's quota or wallet: " + scenario + "/" + id);
                                pet.DismissQuotaInfo();
                            }
                            Assert(queries == providers.Available.Length && c.Client == null && !c.Busy, "Fixture checks never start a client, account lookup or model turn: " + scenario);
                        }
                        Assert(!pet.IsVisible && !chat.IsVisible, "Deployment checks show no windows: " + scenario);
                        chat.ExitWindow(); pet.Close();
                    }
                }
                string special = Path.Combine(run, "discovery-cases");
                var fake = IsolatedSystem(special, "", null);
                string npm = Path.Combine(fake.Roaming, "npm", "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64", "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe");
                FakeExecutable(npm, false);
                Assert(ProviderDiscovery.Find("codex", "", fake) == npm, "npm-installed Windows Codex is discovered without a shell wrapper");
                string customBin = Path.Combine(special, "custom-npm-prefix");
                string customNpm = Path.Combine(customBin, "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64", "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe");
                FakeExecutable(customNpm, false);
                Assert(ProviderDiscovery.Find("codex", "", IsolatedSystem(special, customBin, null)) == customNpm, "A custom npm prefix on PATH resolves to the native executable without executing a cmd file");
                string bundled = Path.Combine(fake.Local, "OpenAI", "Codex", "bin", "current-release", "codex.exe"); FakeExecutable(bundled, false);
                fake.Roaming = Path.Combine(special, "empty-roaming");
                Assert(ProviderDiscovery.Find("codex", "", fake) == bundled, "A bundled native Codex executable is discovered using the current user's paths");
                string older = Path.Combine(fake.Local, "OpenAI", "Codex", "bin", "zzz-old-release", "codex.exe"); FakeExecutable(older, false); File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-10));
                Assert(ProviderDiscovery.Find("codex", "", fake) == bundled, "Bundled releases use modification time rather than arbitrary hash-directory ordering");
                string portable = Path.Combine(special, "便携客户端", "DeepSeek Harness.exe"); FakeExecutable(portable, true);
                fake.Registered = id => id == "deepseek" ? new[] { portable } : new string[0];
                Assert(ProviderDiscovery.Find("deepseek", "", fake) == portable, "A registered custom Harness installation is discovered");
                string incomplete = Path.Combine(special, "incomplete", "DeepSeek Harness.exe"); FakeExecutable(incomplete, false);
                Assert(ProviderDiscovery.Find("deepseek", incomplete, fake) == portable, "Incomplete or stale configured Harness paths fall back to a complete installation");
                string data = Path.Combine(special, "Data"), settingsFile = Path.Combine(data, "providers.json");
                J.Save(settingsFile, P("backend", "deepseek", "harnessExecutable", Path.Combine(special, "missing.exe")));
                var catalog = new ProviderCatalog(settingsFile, IsolatedSystem(special, "", id => id == "codex" ? new[] { bundled } : new string[0]));
                using (var c = new Controller(Path.Combine(data, "session.json"), Path.Combine(special, "Tasks"), a => a(), catalog)) {
                    Assert(c.Backend == "codex", "A stale saved provider falls back to the installed provider on a new computer");
                    c.ConfigureExecutable(portable);
                    Assert(c.Backend == "deepseek" && c.AvailableBackends.Length == 2, "Manually locating a portable client adds and selects it");
                    Assert(J.S(ProviderDiscovery.ReadSettings(settingsFile), "harnessExecutable") == portable, "Manual client paths persist in user settings rather than a release template");
                    c.SetBackend("codex"); c.ConfigureExecutable(bundled);
                    var saved = ProviderDiscovery.ReadSettings(settingsFile);
                    Assert(J.S(saved, "codexExecutable") == bundled && J.S(saved, "harnessExecutable") == portable, "Configuring either client preserves the other client's location");
                    c.Status.Busy = true; Assert(!c.SetBackend("deepseek") && !c.RefreshProviders(), "Discovery and service changes cannot interrupt an active task"); c.Status.Busy = false;
                    File.Delete(portable);
                    Assert(c.RefreshProviders() && c.AvailableBackends.SequenceEqual(new[] { "codex" }), "Uninstalled clients disappear after refresh");
                    c.ThreadId = "codex-session"; c.Messages.Add(new ChatEntry("user", "fixture", "kept")); c.Save();
                }
                using (var c = new Controller(Path.Combine(data, "session.json"), Path.Combine(special, "Tasks"), a => a(), new ProviderCatalog(settingsFile, IsolatedSystem(special, "", id => id == "codex" ? new[] { bundled } : new string[0])))) {
                    Assert(c.ThreadId == "codex-session" && c.Messages.Any(e => e.Text == "kept"), "Provider detection preserves the selected provider's conversation");
                }
                string oldHome = Environment.GetEnvironmentVariable("TETO_DESK_HOME");
                try { string home = Path.Combine(root, "isolated-user-state"); Environment.SetEnvironmentVariable("TETO_DESK_HOME", home); Assert(AppPaths.DataDirectory == Path.Combine(home, "Data") && AppPaths.WorkspaceDirectory == Path.Combine(home, "Tasks"), "State and tasks use a writable per-user location independent of the executable directory"); }
                finally { Environment.SetEnvironmentVariable("TETO_DESK_HOME", oldHome); }
                Assert(Application.Current.Windows.Cast<Window>().All(w => !w.IsVisible), "All supported deployment scenarios stay off-screen");
                J.Save(Path.Combine(root, "deployment-properties.json"), P("supported", new[] { "ChatGPT only", "DeepSeek only", "both" }, "windowsShown", false, "mouseInputUsed", false, "accountRequests", 0, "modelTurns", 0, "privateAuthRead", false));
            } finally { SynchronizationContext.SetSynchronizationContext(previousContext); app.Shutdown(); }
        }
    }
}
