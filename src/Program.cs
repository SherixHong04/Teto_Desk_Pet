using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace TetoDesk {
    public static class Program {
        [STAThread]
        public static void Main(string[] args) {
            bool created;
            string instanceName = Array.IndexOf(args, "--qa") >= 0 ? "Local\\TetoDesk-QA-v01" : "Local\\TetoDesk-Local-v01";
            using (var instance = new Mutex(true, instanceName, out created)) {
                if (!created) { MessageBox.Show("Teto Desk 已经在运行。请双击托盘图标，或按 Ctrl+Alt+T 打开任务面板。", "Teto Desk"); return; }
                try { Run(args); } catch (Exception error) { LogError(error); MessageBox.Show("启动失败：" + error.Message, "Teto Desk"); }
            }
        }
        private static void Run(string[] args) {
            string root = AppDomain.CurrentDomain.BaseDirectory, data = AppPaths.DataDirectory, workspace = AppPaths.WorkspaceDirectory;
            Directory.CreateDirectory(data); Directory.CreateDirectory(workspace);
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var providers = new ProviderCatalog(Path.Combine(data, "providers.json"));
            if (providers.Available.Length == 0) {
                MessageBox.Show("请先定位已安装的 Codex CLI 或 DeepSeek Harness。ChatGPT 通道使用 Codex CLI 的 ChatGPT 登录。", "选择对话客户端");
                var locate = new Microsoft.Win32.OpenFileDialog { Title = "选择 codex.exe 或 DeepSeek Harness.exe", Filter = "支持的客户端|codex*.exe;*DeepSeek*.exe|应用程序|*.exe", CheckFileExists = true };
                if (locate.ShowDialog() != true) { app.Shutdown(); return; }
                providers.Configure(locate.FileName);
            }
            string chancePath = Path.Combine(data, "easter-eggs.json");
            var chanceRandom = new Random(); var chance = DoubleClickChance.Load(chancePath, chanceRandom.Next);
            chance.Changed = delegate { try { chance.Save(chancePath); } catch (Exception error) { LogError(error); } };
            var assets = new Sprites(); var pet = new PetWindow(assets, chance);
            if (Array.IndexOf(args, "--qa") >= 0) pet.ShowInTaskbar = true;
            object settings = null; string settingsPath = Path.Combine(data, "settings.json");
            try { if (File.Exists(settingsPath)) settings = J.Parse(File.ReadAllText(settingsPath)); } catch { }
            double scale, x, y;
            pet.SetScale(Double.TryParse(J.S(settings, "scale"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scale) ? scale : 1.0);
            pet.Left = Double.TryParse(J.S(settings, "left"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out x) ? x : SystemParameters.WorkArea.Right - pet.Width - 28;
            pet.Top = Double.TryParse(J.S(settings, "top"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out y) ? y : SystemParameters.WorkArea.Bottom - pet.Height - 20;
            var controller = new Controller(Path.Combine(data, "session.json"), workspace, delegate(Action action) { app.Dispatcher.BeginInvoke(action); }, providers);
            var chat = new ChatWindow(controller, pet);
            var icon = new Forms.NotifyIcon { Icon = Sprites.Icon(), Text = "Teto Desk · 点击下方气泡输入任务", Visible = true };
            var trayMenu = new Forms.ContextMenuStrip();
            var queryItem = new Forms.ToolStripMenuItem(controller.AccountQueryLabel);
            var accountQuery = new AccountQuery(controller, pet, delegate(string label) { queryItem.Text = label; });
            Action open = delegate { pet.Show(); pet.KeepVisible(); chat.ShowPanel(); };
            Action queryQuota = async delegate {
                if (!controller.HasAvailableBackend) { open(); return; }
                pet.Show(); pet.KeepVisible(); await accountQuery.RunAsync();
            };
            Action exit = delegate {
                if (controller.Busy && MessageBox.Show(chat, "有任务正在执行。退出会停止当前任务，是否退出？", "Teto Desk", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                accountQuery.Dispose(); controller.Dispose(); chat.ExitWindow(); pet.Close(); icon.Visible = false; icon.Dispose(); app.Shutdown();
            };
            queryItem.Click += delegate { queryQuota(); };
            trayMenu.Items.Add("打开任务面板", null, delegate { open(); }); trayMenu.Items.Add(queryItem); trayMenu.Items.Add("显示桌宠", null, delegate { pet.Show(); pet.KeepVisible(); }); trayMenu.Items.Add("退出", null, delegate { exit(); });
            icon.ContextMenuStrip = trayMenu; icon.DoubleClick += delegate { open(); };
            pet.OpenChat = open; pet.Exit = exit; pet.NewChat = delegate { controller.NewChat(controller.Workspace); open(); }; pet.HidePet = delegate { chat.HidePanel(); pet.Hide(); };
            pet.QueryQuota = queryQuota;
            pet.PositionChanged = delegate { try { J.Save(settingsPath, J.O("left", pet.Left, "top", pet.Top, "scale", pet.PetScale)); } catch { } };
            pet.Loaded += async delegate {
                pet.KeepVisible(); pet.PositionChanged();
                try { await controller.ConnectAsync(); } catch { /* First send presents the actionable error in the conversation. */ }
            };
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) {
                LogError(e.Exception); e.Handled = true; controller.Status.End(PetState.Failed); chat.Refresh(); MessageBox.Show(chat, "出现问题：" + e.Exception.Message, "Teto Desk");
            };
            app.Exit += delegate { accountQuery.Dispose(); controller.Dispose(); icon.Dispose(); };
            app.Run(pet);
        }
        private static void LogError(Exception error) {
            try { string data = AppPaths.DataDirectory; Directory.CreateDirectory(data); File.AppendAllText(Path.Combine(data, "errors.log"), DateTime.Now.ToString("s") + " " + error.ToString() + Environment.NewLine); } catch { }
        }
    }
}
