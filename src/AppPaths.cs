using System;
using System.IO;

namespace TetoDesk {
    public static class AppPaths {
        public static string StateRoot {
            get {
                string custom = Environment.GetEnvironmentVariable("TETO_DESK_HOME");
                if (!String.IsNullOrWhiteSpace(custom)) return Path.GetFullPath(custom);
                string app = AppDomain.CurrentDomain.BaseDirectory;
                if (File.Exists(Path.Combine(app, "portable.flag"))) return app;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TetoDesk");
            }
        }
        public static string DataDirectory { get { return Path.Combine(StateRoot, "Data"); } }
        public static string WorkspaceDirectory { get { return Path.Combine(StateRoot, "Tasks"); } }
        public static string ProviderSettings { get { return Path.Combine(DataDirectory, "providers.json"); } }
    }
}
