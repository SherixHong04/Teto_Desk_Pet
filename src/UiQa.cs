namespace TetoDesk {
    // Runs the unchanged app with its existing native-window test flag.
    public static class UiQaProgram {
        [System.STAThread] public static void Main() { Program.Main(new string[] { "--qa" }); }
    }
}
