using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;

namespace TetoDesk {
    public static partial class TestProgram {
        private static readonly List<object> checks = new List<object>();
        private static string root;
        private static void Assert(bool condition, string name) {
            checks.Add(J.O("name", name, "pass", condition)); Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
            if (!condition) throw new Exception(name);
        }
        [STAThread] public static int Main(string[] args) {
            root = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.GetFullPath("qa"); Directory.CreateDirectory(root);
            try {
                string mode = args.Length > 0 ? args[0] : "offline";
                if (mode == "deployment") { DeploymentChecks(); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "preview") { Preview(); return 0; }
                if (mode == "harness-prepare") { HarnessPrepare().GetAwaiter().GetResult(); return 0; }
                if (mode == "harness-audio-check") { HarnessAudioCheck().GetAwaiter().GetResult(); return 0; }
                if (mode == "harness-live") { HarnessLive().GetAwaiter().GetResult(); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "harness-logic") { HarnessLogic(); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "panel-ui") { PanelUi(); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "collapse-style") { CollapseStyle(); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "workspace-balance") { WorkspaceBalance(); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "query-timing") { QueryTiming(); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "account-query") { AccountQueryChecks(false); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "account-query-live") { AccountQueryChecks(true); J.Save(Path.Combine(root, mode + "-test.json"), P("ok", true, "checks", checks)); return 0; }
                if (mode == "file-tools") FileToolsLive().GetAwaiter().GetResult(); else if (mode == "live") Live().GetAwaiter().GetResult(); else if (mode == "regression") ModelRegression().GetAwaiter().GetResult(); else if (mode == "quota") QuotaLive().GetAwaiter().GetResult();
                else if (mode == "composer") ComposerLogic(); else if (mode == "catalog") Catalog().GetAwaiter().GetResult(); else if (mode == "composer-live") ComposerLive().GetAwaiter().GetResult(); else if (mode == "composer-inspect") InspectComposer().GetAwaiter().GetResult(); else if (mode == "easter") EasterEggLogic(); else if (mode == "animation") AnimationIntegration(); else if (mode == "hover") HoverLogic(); else if (mode == "interaction") ClickInteractionLogic(); else if (mode == "pet-ui") PetUiLogic(); else if (mode == "reaction-chance") ReactionChanceLogic(); else if (mode == "menu-preview") MenuPreviewLogic(); else if (mode == "offline") Offline(); else throw new ArgumentException("Unknown test mode");
                J.Save(Path.Combine(root, mode + "-test.json"), J.O("ok", true, "checks", checks)); return 0;
            } catch (Exception e) {
                Console.WriteLine(e.ToString()); J.Save(Path.Combine(root, "test-failure.json"), J.O("ok", false, "checks", checks, "error", e.ToString())); return 1;
            }
        }
        private static object P(params object[] pairs) { return J.O(pairs); }
        private static MenuItem ReactionPreviewMenu(PetWindow pet) {
            var menu = pet.BuildMenu();
            foreach (object entry in menu.Items) { var item = entry as MenuItem; if (item != null && Convert.ToString(item.Header) == "查看现有动作") return item; }
            throw new Exception("Missing animation menu");
        }
        private static MenuItem ReactionPreviewItem(PetWindow pet, string header) {
            foreach (object entry in ReactionPreviewMenu(pet).Items) { var item = entry as MenuItem; if (item != null && Convert.ToString(item.Header) == header) return item; }
            return null;
        }
        private static void MenuPreviewLogic() {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var sprites = new Sprites();
            int draws = 0; var chance = new DoubleClickChance(delegate(int min, int max) { draws++; return 99; }); string chosen;
            chance.TryChoose(true, true, out chosen); var pet = new PetWindow(sprites, chance);
            try {
                Assert(pet.DoubleClickEasterEggReady && pet.DizzyEasterEggReady && chance.Percent == 15, "Both approved reactions are available with an existing probability streak");
                Assert(ReactionPreviewItem(pet, "被双击惹恼（彩蛋）").IsEnabled && ReactionPreviewItem(pet, "眩晕后恢复（彩蛋）").IsEnabled, "Both preview choices are enabled while idle");
                pet.SetMenuInteraction(true);
                Assert(!pet.TryStartDoubleClickEasterEgg() && !pet.TryStartDizzyEasterEgg(), "The original reaction entry points reject an open menu, reproducing the old ordering failure");
                Assert(pet.RequestReactionPreview(DoubleClickEasterEgg.Animation), "Selecting angry records a request while the menu is open");
                Pump(40); Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle", "A preview waits for menu closure regardless of dispatcher timing");
                pet.SetMenuInteraction(false); Pump(40);
                Assert(pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == DoubleClickEasterEgg.Animation, "Closing the menu plays the selected angry reaction");
                Assert(chance.Percent == 15 && draws == 1, "The angry menu preview never draws or changes probability");
                pet.SetMenuInteraction(true); pet.RequestReactionPreview(DoubleClickEasterEgg.DizzyAnimation); pet.SetMenuInteraction(false); Pump(40);
                Assert(pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == DoubleClickEasterEgg.DizzyAnimation && !pet.ReactionSpeechActive, "The second menu choice starts dizziness without angry speech");
                Assert(chance.Percent == 15 && draws == 1, "The dizzy menu preview also leaves the existing streak unchanged");
                pet.ApplyDragFeedback("running-left"); pet.FinishDragFeedback();
                Assert(pet.CurrentAnimation == DoubleClickEasterEgg.DizzyAnimation, "Menu-started dizziness retains drag protection");
                Pump(IdleEasterEggSchedule.Duration(sprites.Clips[DoubleClickEasterEgg.DizzyAnimation]) + 500);
                Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle", "A menu-started native dizzy animation plays once and returns to idle");
                pet.RequestReactionPreview(DoubleClickEasterEgg.Animation); Pump(40);
                Assert(pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == DoubleClickEasterEgg.Animation, "Selection also works when WPF has already reported menu closure");
                pet.SetMenuInteraction(true); pet.RequestReactionPreview(DoubleClickEasterEgg.Animation); pet.RequestReactionPreview(DoubleClickEasterEgg.DizzyAnimation); pet.SetMenuInteraction(false); Pump(40);
                Assert(pet.CurrentAnimation == DoubleClickEasterEgg.DizzyAnimation, "Only the latest requested preview survives menu closure");
                pet.SetMenuInteraction(true); pet.RequestReactionPreview(DoubleClickEasterEgg.Animation); pet.SetMenuInteraction(false);
                pet.SetMenuInteraction(true); pet.SetMenuInteraction(false); Pump(40);
                Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle", "Reopening a menu invalidates a stale queued preview");
                pet.SetMenuInteraction(true); pet.SetMenuInteraction(false); Pump(40);
                Assert(!pet.DoubleClickEasterEggPlaying, "Dismissing a menu without choosing an action starts no reaction");
                pet.SetMenuInteraction(true); pet.RequestReactionPreview(DoubleClickEasterEgg.Animation); pet.SetState(PetState.Thinking); pet.SetMenuInteraction(false); Pump(40);
                Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "running" && !ReactionPreviewItem(pet, "眩晕后恢复（彩蛋）").IsEnabled, "Starting a task cancels a pending preview and disables blocked choices");
                pet.SetState(PetState.Idle); pet.SetMenuInteraction(true); pet.RequestReactionPreview(DoubleClickEasterEgg.DizzyAnimation); pet.SetAnimationPaused(true); pet.SetMenuInteraction(false); Pump(40);
                Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle" && !ReactionPreviewItem(pet, "被双击惹恼（彩蛋）").IsEnabled, "Pause cancels a pending preview and displays unavailable choices as disabled"); pet.SetAnimationPaused(false);
                pet.SetMenuInteraction(true); pet.RequestReactionPreview(DoubleClickEasterEgg.Animation); pet.BeginQuotaInfo(); pet.SetMenuInteraction(false); Pump(40);
                Assert(pet.QuotaVisible && !pet.DoubleClickEasterEggPlaying && !ReactionPreviewItem(pet, "眩晕后恢复（彩蛋）").IsEnabled, "Quota queries retain priority over a queued preview"); pet.DismissQuotaInfo();
                var pointerField = typeof(PetWindow).GetField("pointerDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                pet.SetMenuInteraction(true); pet.RequestReactionPreview(DoubleClickEasterEgg.DizzyAnimation); pointerField.SetValue(pet, true); pet.SetMenuInteraction(false); Pump(40);
                Assert(!pet.DoubleClickEasterEggPlaying, "A held-pointer state blocks the deferred reaction without input injection"); pointerField.SetValue(pet, false); Pump(40);
                Assert(!pet.DoubleClickEasterEggPlaying, "A blocked request is discarded rather than unexpectedly playing later");
                Assert(!pet.RequestReactionPreview("idle"), "Only the two reaction names are accepted by the preview queue");
                var dizzy = sprites.Clips[DoubleClickEasterEgg.DizzyAnimation]; sprites.Clips.Remove(DoubleClickEasterEgg.DizzyAnimation);
                Assert(!pet.RequestReactionPreview(DoubleClickEasterEgg.DizzyAnimation) && ReactionPreviewItem(pet, "眩晕后恢复（彩蛋）") == null, "Missing artwork offers no preview and cannot queue a placeholder"); sprites.Clips[dizzy.Name] = dizzy;
                Assert(chance.Percent == 15 && draws == 1 && !pet.IsVisible, "All menu logic remains off-screen and preserves probability without mouse/account calls");
                J.Save(Path.Combine(root, "menu-preview-status.json"), P("menuClosureRequired", true, "bothProductionPreviewsPassed", true, "staleRequestsCancelled", true, "probabilityUnchanged", true, "logicalStateOnly", true, "windowsShown", false, "mouseInputUsed", false, "apiRequests", 0));
            } finally { pet.Close(); app.Shutdown(); }
        }
        private static void ReactionChanceLogic() {
            string chosen; int drawCount = 0, roll = 99, savedCount = 0;
            var chance = new DoubleClickChance(delegate(int min, int max) { if (min != 0 || max != 100) throw new Exception("Wrong random bounds"); drawCount++; return roll; });
            chance.Changed = delegate { savedCount++; };
            Assert(chance.Percent == 5, "Initial dizziness chance is five percent");
            Assert(!chance.TryChoose(false, true, out chosen) && chosen == "" && drawCount == 0 && savedCount == 0 && chance.Percent == 5, "Ineligible input does not draw, play or advance the streak");
            Assert(chance.TryChoose(true, false, out chosen) && chosen == DoubleClickEasterEgg.Animation && chance.Percent == 5 && drawCount == 0 && savedCount == 0, "Missing approved dizzy art retains the normal reaction without spending probability");
            int[] next = { 15, 25, 35, 45, 55, 65, 75, 85, 95, 100 };
            foreach (int expected in next) Assert(chance.TryChoose(true, true, out chosen) && chosen == DoubleClickEasterEgg.Animation && chance.Percent == expected, "An unsuccessful double click raises the next chance to " + expected + "%");
            Assert(chance.TryChoose(true, true, out chosen) && chosen == DoubleClickEasterEgg.DizzyAnimation && chance.Percent == 5 && drawCount == 10 && savedCount == 11, "One hundred percent guarantees dizziness without overflow and resets to five");
            // Exhaust the whole discrete sample space at every reachable chance,
            // instead of relying on a noisy statistical/random-seed assertion.
            foreach (int percent in new[] { 5, 15, 25, 35, 45, 55, 65, 75, 85, 95, 100 }) {
                int successes = 0; bool resultsCorrect = true;
                for (int sample = 0; sample < 100; sample++) {
                    int value = 99; var candidate = new DoubleClickChance(delegate(int min, int max) { return value; });
                    while (candidate.Percent < percent) candidate.TryChoose(true, true, out chosen);
                    value = sample; candidate.TryChoose(true, true, out chosen);
                    bool success = chosen == DoubleClickEasterEgg.DizzyAnimation;
                    if (success) successes++;
                    resultsCorrect &= success == (sample < percent) && candidate.Percent == (success ? 5 : Math.Min(100, percent + 10));
                }
                Assert(resultsCorrect && successes == percent, "All 100 sample values have exact boundaries and next state at " + percent + "%");
            }
            var invalid = new DoubleClickChance(delegate(int min, int max) { return 100; }); bool rejected = false;
            try { invalid.TryChoose(true, true, out chosen); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && invalid.Percent == 5, "An out-of-range draw is rejected without changing state");
            string path = Path.Combine(root, "chance-persistence-fixture.json");
            chance.TryChoose(true, true, out chosen); chance.Save(path);
            var restored = DoubleClickChance.Load(path, delegate(int min, int max) { return 0; });
            Assert(restored.Percent == 15, "Restart restores the next probability from its own data file");
            restored.TryChoose(true, true, out chosen); restored.Save(path);
            Assert(DoubleClickChance.Load(path, delegate(int min, int max) { return 99; }).Percent == 5, "A triggered reset is saved across restarts");
            foreach (int bad in new[] { -1, 0, 4, 6, 96, 105 }) {
                J.Save(path, P("dizzyChancePercent", bad)); Assert(DoubleClickChance.Load(path, delegate(int min, int max) { return 99; }).Percent == 5, "Invalid saved percentage falls back to five: " + bad);
            }
            File.WriteAllText(path, "{broken"); Assert(DoubleClickChance.Load(path, delegate(int min, int max) { return 99; }).Percent == 5, "A damaged saved state does not prevent startup");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var sprites = new Sprites();
            bool productionDizzy = sprites.Clips.ContainsKey(DoubleClickEasterEgg.DizzyAnimation);
            sprites.Clips.Remove(DoubleClickEasterEgg.DizzyAnimation);
            var waving = sprites.Clips["waving"];
            sprites.Clips[DoubleClickEasterEgg.Animation] = new AnimationClip { Name = DoubleClickEasterEgg.Animation, Frames = waving.Frames, Durations = new[] { 300, 300, 300, 300 }, Loop = false, SpeechStartMs = 200, SpeechEndMs = 800 };
            int runtimeDraws = 0, runtimeRoll = 99;
            var runtimeChance = new DoubleClickChance(delegate(int min, int max) { runtimeDraws++; return runtimeRoll; });
            var pet = new PetWindow(sprites, runtimeChance);
            try {
                Assert(pet.TryStartRandomDoubleClickEasterEgg() && pet.CurrentAnimation == DoubleClickEasterEgg.Animation && runtimeDraws == 0 && pet.DizzyChancePercent == 5, "The real pet falls back to angry while new art awaits approval");
                pet.SetState(PetState.Thinking); pet.SetState(PetState.Idle);
                var sheet = new WriteableBitmap(384, 416, 96, 96, PixelFormats.Bgra32, null);
                for (int i = 0; i < 3; i++) sheet.WritePixels(new Int32Rect((i % 2) * 192, (i / 2) * 208, 192, 208), BitmapPixels(waving.Frames[i]), 192 * 4, 0);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(sheet)); byte[] png;
                using (var stream = new MemoryStream()) { encoder.Save(stream); png = stream.ToArray(); }
                var row = (Dictionary<string, object>)P("name", DoubleClickEasterEgg.DizzyAnimation, "row", 0, "frames", 3, "loop", false, "durations", new[] { 300, 400, 500 });
                var config = (Dictionary<string, object>)P("width", 192, "height", 208, "columns", 2, "animations", new object[] { row });
                var fixture = ParseDizzyFixture(png, config);
                Assert(!fixture.Loop && fixture.SpeechStartMs == -1 && fixture.SpeechEndMs == -1 && SameBitmap(fixture.Frames[2], waving.Frames[2]), "The separate dizzy manifest loads exact frames with no angry speech");
                row["name"] = DoubleClickEasterEgg.Animation; RejectDizzyFixture(png, config, "An angry manifest cannot be loaded as dizziness"); row["name"] = DoubleClickEasterEgg.DizzyAnimation;
                row["speechStartMs"] = 0; RejectDizzyFixture(png, config, "Dizziness rejects inherited angry speech boundaries"); row.Remove("speechStartMs");
                row["loop"] = true; RejectDizzyFixture(png, config, "Dizziness cannot repeat indefinitely"); row["loop"] = false;
                row["durations"] = new[] { 20, 20, 20 }; RejectDizzyFixture(png, config, "Dizzy total duration must be at least one second"); row["durations"] = new[] { 300, 400, 500 };
                sprites.Clips[fixture.Name] = fixture; // In memory only; never installed or saved as art.
                Assert(pet.TryStartRandomDoubleClickEasterEgg() && pet.CurrentAnimation == DoubleClickEasterEgg.Animation && pet.DizzyChancePercent == 15 && runtimeDraws == 1, "A failed eligible runtime draw plays angry and increments exactly once");
                Assert(!pet.TryStartRandomDoubleClickEasterEgg() && pet.DizzyChancePercent == 15 && runtimeDraws == 1, "Repeated double clicks during a reaction do not restart it or consume chance");
                Pump(1350); Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle", "The normal branch still ends in idle");
                Assert(pet.TryStartDizzyEasterEgg() && pet.DizzyChancePercent == 15 && runtimeDraws == 1, "Explicit animation previews do not change random chance");
                pet.SetAnimationPaused(true); Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle" && !pet.TryStartRandomDoubleClickEasterEgg() && runtimeDraws == 1, "Pause cancels dizziness and excludes the random attempt"); pet.SetAnimationPaused(false);
                pet.SetState(PetState.Thinking); Assert(!pet.TryStartRandomDoubleClickEasterEgg() && runtimeDraws == 1 && pet.DizzyChancePercent == 15, "Active tasks block the draw and preserve the streak"); pet.SetState(PetState.Idle);
                pet.BeginQuotaInfo(); Assert(!pet.TryStartRandomDoubleClickEasterEgg() && runtimeDraws == 1, "Quota display blocks the draw"); pet.DismissQuotaInfo();
                // Domain booleans only; no mouse handlers or system capture.
                var pointerField = typeof(PetWindow).GetField("pointerDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                pointerField.SetValue(pet, true); Assert(!pet.TryStartRandomDoubleClickEasterEgg() && runtimeDraws == 1, "A held pointer cannot consume random chance"); pointerField.SetValue(pet, false);
                runtimeRoll = 0; pet.SetCharacterHovered(true);
                Assert(pet.TryStartRandomDoubleClickEasterEgg() && pet.CurrentAnimation == DoubleClickEasterEgg.DizzyAnimation && pet.DizzyChancePercent == 5 && runtimeDraws == 2, "A successful double click replaces angry with dizziness and resets immediately");
                Pump(350); int currentFrame = (int)typeof(PetWindow).GetField("frame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(pet);
                pet.ApplyDragFeedback("running-left"); pet.FinishDragFeedback();
                Assert(pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == DoubleClickEasterEgg.DizzyAnimation && currentFrame == (int)typeof(PetWindow).GetField("frame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(pet), "Dizzy playback retains its frame when drag feedback is applied or released");
                Assert(!pet.ReactionSpeechActive && (pet.ReactionBubble == null || !pet.ReactionBubble.IsVisible), "Dizziness never displays the angry phrase");
                Pump(1100); Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle" && runtimeDraws == 2 && pet.DizzyChancePercent == 5, "Dizziness finishes once and returns to idle without a queued hover jump");
                Assert(!pet.IsVisible, "All chance integration checks remain off-screen without mouse or account calls");
                J.Save(Path.Combine(root, "reaction-chance-status.json"), P("codeReady", true, "productionDizzyArtPresent", productionDizzy, "testFixtureOnly", true, "initialChance", 5, "incrementPoints", 10, "maximumChance", 100, "persistedAcrossRestarts", true, "windowsShown", false, "mouseInputUsed", false, "apiRequests", 0));
            } finally { pet.Close(); app.Shutdown(); }
        }
        private static AnimationClip ParseDizzyFixture(byte[] png, object config) {
            using (var sheet = new MemoryStream(png))
            using (var manifest = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(J.Serialize(config)))) return Sprites.ReadDoubleClickClip(sheet, manifest, DoubleClickEasterEgg.DizzyAnimation);
        }
        private static void RejectDizzyFixture(byte[] png, object config, string reason) {
            bool rejected = false; try { ParseDizzyFixture(png, config); } catch (InvalidDataException) { rejected = true; } Assert(rejected, reason);
        }
        private static void HoverLogic() {
            foreach (var state in new[] { PetState.Idle, PetState.Completed, PetState.Interrupted, PetState.Failed }) Assert(HoverInteraction.ShouldJump(true, state, false, false, false, false, false), "Resting state permits hover jumping: " + state);
            bool taskBlocked = true;
            foreach (var state in new[] { PetState.Connecting, PetState.Processing, PetState.Thinking, PetState.Working, PetState.Replying, PetState.Waiting }) taskBlocked &= !HoverInteraction.ShouldJump(true, state, false, false, false, false, false);
            Assert(taskBlocked, "Every active task state retains priority over hover feedback");
            Assert(!HoverInteraction.ShouldJump(false, PetState.Idle, false, false, false, false, false), "Leaving the character disables hover jumping");
            Assert(!HoverInteraction.ShouldJump(true, PetState.Idle, true, false, false, false, false), "Paused animation blocks hover playback");
            Assert(!HoverInteraction.ShouldJump(true, PetState.Idle, false, true, false, false, false), "Pointer press, capture and dragging have priority");
            Assert(!HoverInteraction.ShouldJump(true, PetState.Idle, false, false, true, false, false), "An open menu blocks hover jumping");
            Assert(!HoverInteraction.ShouldJump(true, PetState.Idle, false, false, false, true, false), "Quota feedback has priority over hover jumping");
            Assert(!HoverInteraction.ShouldJump(true, PetState.Idle, false, false, false, false, true), "Manual gestures and task completion cues finish before hover feedback resumes");
            Assert(!HoverInteraction.ShouldJump(true, PetState.Idle, false, false, false, false, false, true), "A consumed hover entry cannot trigger a second jump");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var sprites = new Sprites(); var pet = new PetWindow(sprites);
            var field = typeof(PetWindow).GetField("frame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var imageField = typeof(PetWindow).GetField("figure", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var image = (Image)imageField.GetValue(pet);
            try {
                Assert(image.ToolTip == null && !ToolTipService.GetIsEnabled(image), "The character has no hover instruction tooltip");
                // Only domain booleans change: no mouse event, geometry,
                // capture, visible window, or input injection is used.
                pet.SetCharacterHovered(true);
                Assert(pet.CurrentAnimation == "jumping" && (int)field.GetValue(pet) == 0, "Logical hover presence starts the approved jump at frame zero");
                int starts = 1; string previousAnimation = "jumping"; var frames = new HashSet<int>();
                var sample = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
                sample.Tick += delegate { if (pet.CurrentAnimation == "jumping") { frames.Add((int)field.GetValue(pet)); if (previousAnimation != "jumping") starts++; } previousAnimation = pet.CurrentAnimation; };
                sample.Start(); try { Pump(IdleEasterEggSchedule.Duration(sprites.Clips["jumping"]) * 2 + 250); } finally { sample.Stop(); }
                Assert(pet.CurrentAnimation == "idle" && frames.Count == 5 && starts == 1, "Stationary logical hover plays all five frames once and remains idle");
                pet.SetCharacterHovered(true); Assert(pet.CurrentAnimation == "idle", "Repeated presence notification does not restart a consumed jump");
                pet.SetCharacterHovered(false); pet.SetCharacterHovered(true);
                Assert(pet.CurrentAnimation == "jumping" && (int)field.GetValue(pet) == 0, "Leaving and reentering permits one fresh jump");
                pet.SetCharacterHovered(false);
                Assert(pet.CurrentAnimation == "idle" && (int)field.GetValue(pet) == 0, "Logical hover departure restores idle immediately without a timed tail");
                pet.SetCharacterHovered(true); pet.SetState(PetState.Thinking);
                Assert(pet.CurrentAnimation == "running", "A task starting during hover immediately replaces jump feedback");
                pet.SetCharacterHovered(false);
                Assert(pet.CurrentAnimation == "running", "Hover departure preserves synchronized active task animation");
                pet.SetCharacterHovered(true); pet.SetState(PetState.Idle);
                Assert(pet.CurrentAnimation == "jumping", "Returning from a task resumes hover feedback while logical presence remains");
                pet.SetState(PetState.Completed); pet.SetCharacterHovered(false); pet.SetCharacterHovered(true);
                Assert(pet.CurrentAnimation == "waving", "Hover cannot interrupt a completed-task cue");
                Pump(IdleEasterEggSchedule.Duration(sprites.Clips["waving"]) + 200);
                Assert(pet.CurrentAnimation == "jumping", "Hover resumes after the completed-task cue finishes");
                pet.BeginQuotaInfo(); Assert(pet.CurrentAnimation == "quota-thinking", "Quota query immediately replaces hover jumping");
                pet.DismissQuotaInfo(); Assert(pet.CurrentAnimation == "idle", "Dismissing quota does not repeat a consumed hover jump");
                pet.SetCharacterHovered(false); pet.SetCharacterHovered(true);
                pet.SetAnimationPaused(true); Assert(pet.CurrentAnimation == "idle", "Pausing stops hover feedback on the idle pose");
                pet.SetAnimationPaused(false); Assert(pet.CurrentAnimation == "idle", "Unpausing does not repeat a consumed hover entry");
                pet.Gesture("waving", .3); Assert(pet.CurrentAnimation == "waving", "A manual gesture can temporarily replace hover feedback");
                Pump(600); Assert(pet.CurrentAnimation == "idle", "A manual gesture returns to idle after a consumed hover entry");
                pet.SetCharacterHovered(false); pet.SetAnimationPaused(true); pet.SetCharacterHovered(true); pet.SetAnimationPaused(false);
                Assert(pet.CurrentAnimation == "jumping", "An entry while paused can jump once when playback becomes eligible");
                pet.SetCharacterHovered(false);
                Assert(!pet.IsVisible && pet.CurrentAnimation == "idle" && !pet.IdleEasterEggPlaying, "Hover state checks finish off-screen with idle and no overlapping Easter egg");
                J.Save(Path.Combine(root, "hover-status.json"), P("tooltipRemoved", true, "hoverAnimation", "jumping", "oncePerEntry", true, "afterJumpAnimation", "idle", "leaveAnimation", "idle", "taskAndQuotaPriority", true, "logicalStateOnly", true, "windowsShown", false, "mouseInputUsed", false, "apiRequests", 0));
            } finally { pet.Close(); app.Shutdown(); }
        }
        private static void ClickInteractionLogic() {
            var clicks = new PetClickInteraction(500, new Size(4, 4));
            Assert(!clicks.Begin(new Point(50, 50), 0) && clicks.End(40, true) == PetClickResult.PendingSingle, "A first valid click waits for the double-click interval");
            Assert(clicks.Poll(499) == PetClickResult.None && clicks.Poll(500) == PetClickResult.Single && clicks.Poll(501) == PetClickResult.None, "A single click fires only once after its deadline");
            clicks.Begin(new Point(50, 50), 1000); clicks.End(1040, true);
            Assert(!clicks.Begin(new Point(51, 49), 1300) && clicks.Poll(1500) == PetClickResult.None && clicks.End(1340, true) == PetClickResult.Double, "A nearby quick second press suppresses the single and yields one double click");
            clicks.Begin(new Point(0, 0), 2000); clicks.End(2050, true);
            Assert(clicks.Begin(new Point(0, 0), 2501) && clicks.End(2540, true) == PetClickResult.PendingSingle, "Two slow clicks stay separate even at the same location");
            clicks.Cancel(); clicks.Begin(new Point(0, 0), 3000); clicks.End(3020, true);
            Assert(clicks.Begin(new Point(3, 0), 3200) && clicks.End(3230, true) == PetClickResult.PendingSingle, "Separated click locations do not count as a double click");
            clicks.Cancel(); clicks.Begin(new Point(0, 0), 4000); clicks.End(4020, true); clicks.Begin(new Point(0, 0), 4200);
            Assert(clicks.End(4250, false) == PetClickResult.None && clicks.Poll(5000) == PetClickResult.None, "Dragging or losing capture on the second press cancels both click actions");
            clicks.Begin(new Point(0, 0), 6000); clicks.Cancel();
            Assert(clicks.End(6030, true) == PetClickResult.None, "A canceled first drag cannot arm a later double click");
            clicks.Begin(new Point(0, 0), 7000); clicks.End(7020, true); clicks.Begin(new Point(2, -2), 7500);
            Assert(clicks.End(7520, true) == PetClickResult.Double, "Windows double-click time and rectangle boundaries are inclusive");
            clicks.Begin(new Point(0, 0), 8000); clicks.End(8020, true); clicks.Begin(new Point(0, 0), 8250); clicks.End(8280, true);
            Assert(!clicks.Begin(new Point(0, 0), 8350) && clicks.End(8380, true) == PetClickResult.PendingSingle, "A third click starts a fresh sequence instead of repeating the same double");
            clicks.Cancel(); clicks.Begin(new Point(0, 0), 9000);
            Assert(clicks.End(9800, true) == PetClickResult.PendingSingle && clicks.Poll(9800) == PetClickResult.Single, "A long stationary first press resolves as a single click on release");
            foreach (var state in new[] { PetState.Connecting, PetState.Processing, PetState.Thinking, PetState.Working, PetState.Replying, PetState.Waiting }) Assert(!DoubleClickEasterEgg.Eligible(state, false, false, false, false, false), "Double-click feedback respects task priority: " + state);
            Assert(!DoubleClickEasterEgg.Eligible(PetState.Idle, true, false, false, false, false) && !DoubleClickEasterEgg.Eligible(PetState.Idle, false, true, false, false, false)
                && !DoubleClickEasterEgg.Eligible(PetState.Idle, false, false, true, false, false) && !DoubleClickEasterEgg.Eligible(PetState.Idle, false, false, false, true, false)
                && !DoubleClickEasterEgg.Eligible(PetState.Idle, false, false, false, false, true), "Pause, held pointer, menu, quota and task cues retain priority");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var sprites = new Sprites(); var pet = new PetWindow(sprites);
            bool productionArtPresent = sprites.Clips.ContainsKey(DoubleClickEasterEgg.Animation);
            try {
                Assert(productionArtPresent ? pet.DoubleClickEasterEggReady && pet.TryStartDoubleClickEasterEgg()
                    : !pet.DoubleClickEasterEggReady && !pet.TryStartDoubleClickEasterEgg(), "The reaction is enabled only when its separate approved resource pair is embedded");
                pet.SetState(PetState.Idle);
                DoubleClickManifestLogic(sprites);
                // An in-memory timing fixture uses existing approved frames;
                // it is not saved, embedded, installed, or presented as new art.
                var waving = sprites.Clips["waving"];
                var fixture = new AnimationClip { Name = DoubleClickEasterEgg.Animation, Frames = waving.Frames, Durations = new[] { 200, 400, 200, 300 }, Loop = false, SpeechStartMs = 250, SpeechEndMs = 750 };
                sprites.Clips[fixture.Name] = fixture;
                Assert(pet.DoubleClickEasterEggReady && pet.TryStartDoubleClickEasterEgg() && !pet.ReactionSpeechActive, "The reaction starts once, with speech deferred to its designated segment");
                Pump(330);
                Assert(pet.ReactionSpeechActive && pet.ReactionBubble != null && pet.ReactionBubble.SpeechText == DoubleClickEasterEgg.Phrase, "The exact Japanese phrase appears at an intra-frame speech boundary");
                Assert(pet.ReactionBubble.Height < 154 && pet.ReactionBubble.Width >= 140 && !pet.ReactionBubble.IsVisible, "The reused bubble sizes to speech while off-screen checks show no window");
                Render((FrameworkElement)pet.ReactionBubble.Content, pet.ReactionBubble.Width, pet.ReactionBubble.Height, Path.Combine(root, "double-click-speech-example.png"));
                Pump(510); Assert(!pet.ReactionSpeechActive && pet.DoubleClickEasterEggPlaying, "Speech ends during the recovery segment before the character returns to idle");
                Pump(550); Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle", "The complete reaction plays once and naturally returns to idle");
                pet.SetCharacterHovered(true); Assert(pet.TryStartDoubleClickEasterEgg(), "A double-click reaction can replace hover feedback");
                Pump(1400); Assert(pet.CurrentAnimation == "idle" && !pet.DoubleClickEasterEggPlaying, "A reaction does not cause another jump while the pointer remains over Teto");
                pet.SetCharacterHovered(false); pet.TryStartDoubleClickEasterEgg(); Pump(330); pet.SetState(PetState.Thinking);
                Assert(!pet.DoubleClickEasterEggPlaying && !pet.ReactionSpeechActive && pet.CurrentAnimation == "running" && !pet.TryStartDoubleClickEasterEgg(), "Starting a task cancels the reaction and speech and keeps task animation authoritative");
                pet.SetState(PetState.Idle); pet.TryStartDoubleClickEasterEgg(); int ticket = pet.BeginQuotaInfo();
                Assert(!pet.DoubleClickEasterEggPlaying && !pet.ReactionSpeechActive && pet.QuotaVisible && !pet.TryStartDoubleClickEasterEgg(), "Quota queries replace and block the double-click reaction");
                pet.DismissQuotaInfo(); pet.TryStartDoubleClickEasterEgg(); pet.SetAnimationPaused(true);
                Assert(!pet.DoubleClickEasterEggPlaying && !pet.ReactionSpeechActive && pet.CurrentAnimation == "idle", "Pausing cancels the reaction and clears speech");
                pet.SetAnimationPaused(false); pet.TryStartDoubleClickEasterEgg(); pet.Gesture("waving", .1);
                Assert(!pet.DoubleClickEasterEggPlaying && !pet.ReactionSpeechActive && pet.CurrentAnimation == "waving", "Another manual gesture clears reaction state and its speech");
                var bubble = pet.ReactionBubble; bubble.SetSpeech("短句"); double shortWidth = bubble.Width;
                bubble.SetSpeech(DoubleClickEasterEgg.Phrase); Assert(bubble.Width > shortWidth, "Speech bubble width follows the actual phrase length");
                bubble.SetUnavailable(); Assert(bubble.Width == QuotaBubbleWindow.QuotaWidth && bubble.Height == QuotaBubbleWindow.QuotaHeight, "Switching bubble content back to quota restores its compact quota layout");
                Assert(!pet.IsVisible && !bubble.IsVisible, "Interaction checks never show the pet or its bubble");
                J.Save(Path.Combine(root, "interaction-status.json"), P("doubleClickCodeReady", true, "artApproved", productionArtPresent, "productionArtPresent", productionArtPresent, "testFixtureOnly", true, "singleAndDragExcluded", true, "speech", DoubleClickEasterEgg.Phrase, "speechFadeReused", true, "windowsShown", false, "mouseInputUsed", false, "apiRequests", 0));
            } finally { pet.Close(); app.Shutdown(); }
        }
        private static long UnixSeconds(DateTime value) { return (long)(value.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
        private static QuotaSnapshot QuotaDisplayExample(DateTime now) {
            return QuotaSnapshot.Read(P("rateLimits", P("primary", P("usedPercent", 55, "windowDurationMins", 300, "resetsAt", UnixSeconds(now.AddMinutes(42))), "secondary", P("usedPercent", 9, "windowDurationMins", 10080, "resetsAt", UnixSeconds(now.AddDays(6).AddHours(19))))));
        }
        private static void PetUiLogic() {
            QuotaParsing();
            var now = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc); var example = QuotaDisplayExample(now);
            Assert(example.FiveHours.ResetDisplay(now) == "42 分钟后重置" && example.Week.ResetDisplay(now) == "6 天 19 小时后重置", "Reset timestamps format as compact minute and day/hour countdowns");
            var window = new QuotaWindow { ResetsAt = UnixSeconds(now.AddHours(1).AddMinutes(2)) };
            Assert(window.ResetDisplay(now) == "1 小时 2 分钟后重置", "A same-day reset retains hours and remaining minutes");
            window.ResetsAt = UnixSeconds(now.AddSeconds(1)); Assert(window.ResetDisplay(now) == "1 分钟后重置", "A positive sub-minute reset never displays zero minutes");
            window.ResetsAt = UnixSeconds(now); Assert(window.ResetDisplay(now) == "即将重置", "Elapsed reset times never display a negative countdown");
            window.ResetsAt = null; Assert(window.ResetDisplay(now) == "重置时间暂不可用", "Missing reset information remains unavailable");
            window.ResetsAt = Int64.MaxValue; Assert(window.ResetDisplay(now) == "重置时间暂不可用", "Invalid reset timestamps do not overflow or invent a date");
            Assert(PetDragFeedback.PreserveAnimation(true, false) && PetDragFeedback.PreserveAnimation(false, true) && !PetDragFeedback.PreserveAnimation(false, false), "Reaction and quota feedback retain animation priority during relocation");
            Assert(PetDragFeedback.DismissQuotaOnRelease(true, false, true) && !PetDragFeedback.DismissQuotaOnRelease(true, true, true)
                && !PetDragFeedback.DismissQuotaOnRelease(true, false, false) && !PetDragFeedback.DismissQuotaOnRelease(false, false, true), "Only a completed stationary quota click dismisses it; dragging or capture loss does not");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var sprites = new Sprites(); var pet = new PetWindow(sprites);
            var image = (Image)typeof(PetWindow).GetField("figure", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(pet);
            var surface = (Grid)image.Parent;
            var frameField = typeof(PetWindow).GetField("frame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            try {
                Rect native = PetInteractionRegion.AtScale(1);
                Assert(native == new Rect(35, 3, 120, 202) && pet.CharacterHitArea.Width == native.Width && pet.CharacterHitArea.Height == native.Height && !image.IsHitTestVisible,
                    "The character uses a fixed body rectangle rather than per-frame image hit testing");
                Assert(((SolidColorBrush)pet.CharacterHitArea.Background).Color.A == 1, "The fixed rectangle remains hit-testable in a layered transparent window");
                bool envelope = true;
                foreach (var frame in sprites.Clips["jumping"].Frames) {
                    image.Source = frame; surface.Measure(new Size(192, 208)); surface.Arrange(new Rect(0, 0, 192, 208)); surface.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(192, 208, 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface); byte[] pixels = BitmapPixels(bitmap);
                    for (int y = 3; y < 205; y++) for (int x = 35; x < 155; x++) envelope &= pixels[(y * 192 + x) * 4 + 3] > 0;
                }
                Assert(envelope, "Every pixel inside the fixed body rectangle stays nontransparent through all jump frames");
                pet.SetScale(1.5); Rect scaled = PetInteractionRegion.AtScale(1.5);
                Assert(pet.CharacterHitArea.Width == scaled.Width && pet.CharacterHitArea.Height == scaled.Height && pet.CharacterHitArea.Margin.Left == scaled.Left && pet.CharacterHitArea.Margin.Top == scaled.Top,
                    "Changing pet size scales the same fixed hit rectangle without tying it to a frame");
                pet.SetScale(1); pet.SetCharacterHovered(false); Assert(pet.TryStartDoubleClickEasterEgg(), "The production reaction can start for protected-playback checks");
                Pump(850); int reactionFrame = (int)frameField.GetValue(pet);
                pet.ApplyDragFeedback("running-left"); pet.ApplyDragFeedback("running-right"); pet.FinishDragFeedback();
                Assert(pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == DoubleClickEasterEgg.Animation && (int)frameField.GetValue(pet) == reactionFrame && pet.ReactionSpeechActive,
                    "Relocation feedback and release preserve the reaction frame and speech instead of replacing it with running");
                var area = new Rect(0, 0, 1920, 1040); var position = new Rect(600, 600, 192, 254);
                pet.PlaceInteractionBubbles(area, position); var before = new Point(pet.ReactionBubble.Left, pet.ReactionBubble.Top);
                position.Offset(80, 40); pet.PlaceInteractionBubbles(area, position);
                Assert(pet.ReactionBubble.Left - before.X == 80 && pet.ReactionBubble.Top - before.Y == 40, "Reaction speech follows a relocated pet by the same offset");
                Pump(4000); Assert(!pet.DoubleClickEasterEggPlaying && pet.CurrentAnimation == "idle", "Protected production reaction finishes naturally without restarting or remaining in running");
                int ticket = pet.BeginQuotaInfo(); pet.ApplyDragFeedback("running-left"); pet.FinishDragFeedback();
                Assert(pet.QuotaVisible && pet.CurrentAnimation == "quota-thinking", "Relocation while awaiting a quota response keeps the thinking animation and query active");
                Assert(pet.ShowQuotaResult(ticket, example), "A result can arrive after relocation feedback without being discarded");
                pet.QuotaBubble.SetSnapshot(example, now);
                Assert(pet.QuotaBubble.FiveResetText == "42 分钟后重置" && pet.QuotaBubble.WeekResetText == "6 天 19 小时后重置"
                    && pet.QuotaBubble.FiveRemainingText == "剩余 45%" && pet.QuotaBubble.WeekRemainingText == "剩余 91%"
                    && pet.QuotaBubble.FiveBarValue == 45 && pet.QuotaBubble.WeekBarValue == 91, "Each quota section contains its countdown, remaining percentage and matching bar");
                Assert(pet.QuotaBubble.Width == 260 && pet.QuotaBubble.Height == 184, "Expanded quota details fit a bubble only 16 px wider and 30 px taller");
                Render((FrameworkElement)pet.QuotaBubble.Content, pet.QuotaBubble.Width, pet.QuotaBubble.Height, Path.Combine(root, "quota-details-example.png"));
                pet.QuotaBubble.RefreshCountdown(now.AddMinutes(1)); Assert(pet.QuotaBubble.FiveResetText == "41 分钟后重置", "The reset countdown updates without reading the account again");
                position = new Rect(600, 600, 192, 254); pet.PlaceInteractionBubbles(area, position); before = new Point(pet.QuotaBubble.Left, pet.QuotaBubble.Top);
                position.Offset(-60, 30); pet.PlaceInteractionBubbles(area, position);
                Assert(pet.QuotaBubble.Left - before.X == -60 && pet.QuotaBubble.Top - before.Y == 30, "Quota content follows the relocated pet by the same offset");
                Pump(300); int quotaFrame = (int)frameField.GetValue(pet); pet.ApplyDragFeedback("running-right"); pet.FinishDragFeedback();
                Assert(pet.QuotaVisible && pet.CurrentAnimation == "quota-thinking" && (int)frameField.GetValue(pet) == quotaFrame, "Relocation preserves the displayed quota animation frame and its existing timer");
                pet.SetCharacterHovered(true); Assert(pet.ConsumeQuotaClick() && !pet.QuotaVisible && pet.CurrentAnimation == "idle", "A stationary quota click immediately returns to idle without triggering a queued jump or wave");
                Assert(!pet.ShowQuotaResult(ticket, example), "A clicked-away quota result cannot reopen from a stale response");
                ticket = pet.BeginQuotaInfo(); pet.ShowQuotaResult(ticket, example); pet.ApplyDragFeedback("running-left"); pet.FinishDragFeedback();
                Pump(5350); Assert(!pet.QuotaVisible && pet.CurrentAnimation == "idle", "Relocation does not extend the original five-second quota display");
                pet.QuotaBubble.SetUnavailable(); Assert(pet.QuotaBubble.FiveRemainingText == "剩余 --" && pet.QuotaBubble.FiveBarValue == 0 && pet.QuotaBubble.FiveResetText == "重置时间暂不可用", "Unavailable quota data shows an unfilled track and unknown text, not an invented percentage");
                var bounds = QuotaPlacement.Place(new Rect(-800, -300, 800, 600), new Rect(-192, -254, 192, 254), new Size(260, 184));
                Assert(new Rect(-800, -300, 800, 600).Contains(bounds), "The modestly expanded quota bubble still clamps to a monitor with negative coordinates");
                Assert(!pet.IsVisible && !pet.QuotaBubble.IsVisible && !pet.ReactionBubble.IsVisible, "The complete UI logic checks remain off-screen without mouse input");
                J.Save(Path.Combine(root, "pet-ui-status.json"), P("fixedHoverRectangle", true, "hitAlpha", 1, "reactionDragPreserved", true, "quotaDragPreserved", true, "bubblesFollowPet", true, "quotaClickReturnsIdle", true, "quotaTitleFontSize", 15, "quotaWidth", 260, "quotaHeight", 184, "quotaCountdownAndBars", true, "windowsShown", false, "mouseInputUsed", false, "apiRequests", 0));
            } finally { pet.Close(); app.Shutdown(); }
        }
        private static AnimationClip ParseReactionFixture(byte[] png, object config) {
            using (var sheet = new MemoryStream(png))
            using (var manifest = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(J.Serialize(config)))) return Sprites.ReadDoubleClickClip(sheet, manifest);
        }
        private static void RejectReactionFixture(byte[] png, object config, string reason) {
            bool rejected = false;
            try { ParseReactionFixture(png, config); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, reason);
        }
        private static void DoubleClickManifestLogic(Sprites sprites) {
            var sheet = new WriteableBitmap(384, 416, 96, 96, PixelFormats.Bgra32, null);
            for (int i = 0; i < 3; i++) sheet.WritePixels(new Int32Rect((i % 2) * 192, (i / 2) * 208, 192, 208), BitmapPixels(sprites.Clips["waving"].Frames[i]), 192 * 4, 0);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(sheet)); byte[] png;
            using (var stream = new MemoryStream()) { encoder.Save(stream); png = stream.ToArray(); }
            var row = (Dictionary<string, object>)P("name", DoubleClickEasterEgg.Animation, "row", 0, "frames", 3, "loop", false, "durations", new[] { 300, 400, 500 }, "speechStartMs", 350, "speechEndMs", 950);
            var config = (Dictionary<string, object>)P("width", 192, "height", 208, "columns", 2, "animations", new object[] { row });
            var clip = ParseReactionFixture(png, config);
            Assert(!clip.Loop && clip.Frames.Length == 3 && clip.SpeechStartMs == 350 && clip.SpeechEndMs == 950 && SameBitmap(clip.Frames[0], sprites.Clips["waving"].Frames[0]) && SameBitmap(clip.Frames[2], sprites.Clips["waving"].Frames[2]), "The multi-row manifest loads exact frames and non-looping speech timings");
            row["speechEndMs"] = 1201; RejectReactionFixture(png, config, "Speech extending beyond the animation is rejected"); row["speechEndMs"] = 950;
            row["speechStartMs"] = 950; RejectReactionFixture(png, config, "Empty or reversed speech timing is rejected"); row["speechStartMs"] = 350;
            row["loop"] = true; RejectReactionFixture(png, config, "A repeating double-click reaction is rejected"); row["loop"] = false;
            config["width"] = 190; RejectReactionFixture(png, config, "Character canvas changes are rejected"); config["width"] = 192;
            config["columns"] = 3; RejectReactionFixture(png, config, "A sheet and manifest dimension mismatch is rejected"); config["columns"] = 2;
            row["durations"] = new[] { 300, 400 }; RejectReactionFixture(png, config, "Missing frame timings are rejected");
            row["durations"] = new[] { 19, 400, 800 }; RejectReactionFixture(png, config, "Unusable frame durations are rejected");
        }
        private static BitmapSource ReadBitmap(string path) {
            using (var stream = File.OpenRead(path)) { var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); bitmap.Freeze(); return bitmap; }
        }
        private static byte[] BitmapPixels(BitmapSource bitmap) {
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; converted.CopyPixels(pixels, bitmap.PixelWidth * 4, 0); return pixels;
        }
        private static bool SameBitmap(BitmapSource first, BitmapSource second) {
            if (first.PixelWidth != second.PixelWidth || first.PixelHeight != second.PixelHeight) return false;
            byte[] a = BitmapPixels(first), b = BitmapPixels(second);
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private static void AnimationIntegration() {
            // Reference paths are supplied by the release workflow. This mode
            // never shows a window, sends input, or starts a Codex process.
            object references = J.Parse(File.ReadAllText(Path.Combine(root, "animation-references.json")));
            var sprites = new Sprites(); var left = sprites.Clips["running-left"]; var egg = sprites.Clips["idle-baguette"];
            var reaction = sprites.Clips[DoubleClickEasterEgg.Animation];
            bool leftTiming = left.Frames.Length == 8 && IdleEasterEggSchedule.Duration(left) == 1060;
            for (int i = 0; i < left.Durations.Length; i++) leftTiming &= left.Durations[i] == (i == 7 ? 220 : 120);
            Assert(leftTiming, "Approved left-running repair retains all eight frames and the original 1060 ms timing");
            AssertNativeClip(egg, J.S(references, "baguetteFrames"), J.S(references, "baguetteNative"));
            AssertNativeClip(reaction, J.S(references, "reactionFrames"), J.S(references, "reactionNative"));
            AnimationClip dizzy; bool hasDizzy = sprites.Clips.TryGetValue(DoubleClickEasterEgg.DizzyAnimation, out dizzy);
            if (hasDizzy) {
                AssertNativeClip(dizzy, J.S(references, "dizzyFrames"), J.S(references, "dizzyNative"));
                Assert(dizzy.SpeechStartMs == -1 && dizzy.SpeechEndMs == -1, "The native dizzy animation contains no angry speech timing");
                Assert(SameBitmap(dizzy.Frames[0], sprites.Clips["idle"].Frames[0]) && SameBitmap(dizzy.Frames[dizzy.Frames.Length - 1], sprites.Clips["idle"].Frames[0]), "Dizziness starts and ends at the original idle pose");
            }
            bool leftSourcesMatch = true;
            for (int i = 0; i < left.Frames.Length; i++) leftSourcesMatch &= SameBitmap(left.Frames[i], ReadBitmap(Path.Combine(J.S(references, "leftFrames"), i.ToString("00") + ".png")));
            Assert(leftSourcesMatch, "Every loaded left-running frame matches the approved PNG source");
            var baseline = ReadBitmap(J.S(references, "baselineAtlas"));
            object originalManifest = J.Parse(File.ReadAllText(J.S(references, "baselineManifest")));
            bool otherActionsMatch = true;
            foreach (object row in J.A(J.Get(originalManifest, "animations"))) {
                string name = J.S(row, "name"); if (name == "running-left") continue;
                for (int i = 0; i < J.I(row, "frames", 0); i++) otherActionsMatch &= SameBitmap(sprites.Clips[name].Frames[i], new CroppedBitmap(baseline, new Int32Rect(i * 192, J.I(row, "row", 0) * 208, 192, 208)));
            }
            Assert(otherActionsMatch, "All eight other original animations retain their baseline pixels");
            Assert(SameBitmap(left.Frames[7], new CroppedBitmap(baseline, new Int32Rect(7 * 192, 2 * 208, 192, 208))), "Left-running frame eight remains unchanged");
            Assert(SameBitmap(egg.Frames[0], sprites.Clips["idle"].Frames[0]) && SameBitmap(egg.Frames[egg.Frames.Length - 1], sprites.Clips["idle"].Frames[0]), "Baguette starts and ends at the original idle pose without a pose jump");
            Assert(SameBitmap(reaction.Frames[0], sprites.Clips["idle"].Frames[0]) && SameBitmap(reaction.Frames[reaction.Frames.Length - 1], sprites.Clips["idle"].Frames[0]), "The native reaction starts and ends at the original idle pose");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var pet = new PetWindow(sprites, new DoubleClickChance(delegate(int min, int max) { return 0; }));
            try {
                var baguettePlayback = PlayNativeClip(pet, egg, false);
                var reactionPlayback = PlayNativeClip(pet, reaction, true);
                object dizzyPlayback = hasDizzy ? PlayNativeClip(pet, dizzy, false) : null;
                J.Save(Path.Combine(root, "animation-status.json"), P("approvedArtworkEmbedded", true, "leftFrames", left.Frames.Length, "leftDurationMs", IdleEasterEggSchedule.Duration(left), "baguetteFrames", egg.Frames.Length, "baguetteDurationMs", IdleEasterEggSchedule.Duration(egg), "observedFrames", egg.Frames.Length, "baguettePlayback", baguettePlayback, "reactionFrames", reaction.Frames.Length, "reactionDurationMs", IdleEasterEggSchedule.Duration(reaction), "reactionPlayback", reactionPlayback, "dizzyFrames", hasDizzy ? dizzy.Frames.Length : 0, "dizzyDurationMs", hasDizzy ? IdleEasterEggSchedule.Duration(dizzy) : 0, "dizzyPlayback", dizzyPlayback, "windowsShown", false, "mouseInputUsed", false, "apiRequests", 0));
            } finally { pet.Close(); app.Shutdown(); }
        }
        private static void AssertNativeClip(AnimationClip clip, string framesDirectory, string nativeReport) {
            object native = J.Parse(File.ReadAllText(nativeReport)); object[] durations = J.A(J.Get(native, "durations"));
            bool timing = !clip.Loop && clip.Frames.Length == J.I(native, "frames", 0) && durations.Length == clip.Durations.Length;
            for (int i = 0; i < durations.Length && timing; i++) timing &= Convert.ToInt32(durations[i]) == clip.Durations[i];
            Assert(timing, clip.Name + ": exact frame count and every duration match the latest native Aseprite project");
            bool pixels = true;
            for (int i = 0; i < clip.Frames.Length; i++) pixels &= SameBitmap(clip.Frames[i], ReadBitmap(Path.Combine(framesDirectory, (i + 1).ToString("000") + ".png")));
            Assert(pixels, clip.Name + ": every embedded frame matches the native exported PNG across atlas rows");
            if (clip.Name == DoubleClickEasterEgg.Animation) {
                object metadata = J.Parse(J.S(native, "spriteData"));
                Assert(clip.SpeechStartMs == J.I(metadata, "speechStartMs", -1) && clip.SpeechEndMs == J.I(metadata, "speechEndMs", -1), "Reaction speech timing matches the native project's metadata");
            }
        }
        private static object PlayNativeClip(PetWindow pet, AnimationClip clip, bool hasSpeech) {
            var observed = new List<int>(); var clock = Stopwatch.StartNew(); long finishedAt = -1, finalStartedAt = -1;
            long speechStartedAt = -1, speechEndedAt = -1;
            var field = typeof(PetWindow).GetField("frame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var sample = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            bool dizzyDragChecked = false;
            sample.Tick += delegate {
                if (pet.CurrentAnimation == clip.Name) {
                    int current = (int)field.GetValue(pet);
                    if (observed.Count == 0 || observed[observed.Count - 1] != current) observed.Add(current);
                    if (current == clip.Frames.Length - 1 && finalStartedAt < 0) finalStartedAt = clock.ElapsedMilliseconds;
                    if (clip.Name == DoubleClickEasterEgg.DizzyAnimation && !dizzyDragChecked && clock.ElapsedMilliseconds >= 600) {
                        dizzyDragChecked = true; pet.ApplyDragFeedback("running-left"); pet.ApplyDragFeedback("running-right"); pet.FinishDragFeedback();
                        Assert(pet.CurrentAnimation == clip.Name && (int)field.GetValue(pet) == current && pet.DoubleClickEasterEggPlaying, "The complete production dizzy clip preserves its current frame during relocation feedback");
                        Assert(!pet.TryStartRandomDoubleClickEasterEgg() && pet.DizzyChancePercent == 5, "Repeated triggers during native dizziness neither restart playback nor change probability");
                    }
                } else if (observed.Count > 0 && finishedAt < 0) finishedAt = clock.ElapsedMilliseconds;
                if (hasSpeech && pet.ReactionSpeechActive && speechStartedAt < 0) speechStartedAt = clock.ElapsedMilliseconds;
                if (hasSpeech && !pet.ReactionSpeechActive && speechStartedAt >= 0 && speechEndedAt < 0) speechEndedAt = clock.ElapsedMilliseconds;
            };
            try {
                int duration = IdleEasterEggSchedule.Duration(clip);
                if (hasSpeech) Assert(pet.TryStartDoubleClickEasterEgg(), "The approved production reaction starts off-screen");
                else if (clip.Name == DoubleClickEasterEgg.DizzyAnimation) {
                    pet.SetCharacterHovered(true);
                    Assert(pet.DizzyEasterEggReady && pet.TryStartRandomDoubleClickEasterEgg() && pet.CurrentAnimation == clip.Name && pet.DizzyChancePercent == 5, "The real probability branch selects the embedded native dizzy clip and resets the chance");
                }
                else pet.Gesture(clip.Name, duration / 1000.0 + 1);
                sample.Start(); Pump(duration + 3000); sample.Stop();
                bool ordered = observed.Count == clip.Frames.Length; for (int i = 0; i < observed.Count; i++) ordered &= observed[i] == i;
                Assert(ordered, clip.Name + ": real WPF playback visits every native frame in order once");
                Assert(finishedAt >= duration - 40 && finalStartedAt >= 0 && finishedAt - finalStartedAt >= clip.Durations[clip.Durations.Length - 1] - 35, clip.Name + ": full playback includes the native final-frame hold");
                Assert(pet.CurrentAnimation == "idle" && !pet.IdleEasterEggPlaying && !pet.DoubleClickEasterEggPlaying && !pet.ReactionSpeechActive && !pet.IsVisible, clip.Name + ": playback returns to idle with no replay or lingering speech");
                if (clip.Name == DoubleClickEasterEgg.DizzyAnimation) Assert(dizzyDragChecked && !pet.ReactionSpeechActive && !pet.ReactionBubble.IsVisible && pet.DizzyChancePercent == 5, "Native dizziness completes with no angry bubble, repeated hover jump or chance mutation");
                if (hasSpeech) {
                    Assert(speechStartedAt >= clip.SpeechStartMs - 25 && speechStartedAt <= clip.SpeechStartMs + 160 && speechEndedAt >= clip.SpeechEndMs - 25 && speechEndedAt <= clip.SpeechEndMs + 240, "The native production reaction displays its speech only during the designated segment");
                    Assert(pet.ReactionBubble != null && !pet.ReactionBubble.IsVisible && pet.ReactionBubble.SpeechText == DoubleClickEasterEgg.Phrase, "The approved reaction retains the exact phrase without showing a test window");
                }
                return P("observedFrames", observed.Count, "playbackWallMs", finishedAt, "finalFrameHoldMs", finishedAt - finalStartedAt, "speechStartedAtMs", speechStartedAt, "speechEndedAtMs", speechEndedAt);
            } finally { sample.Stop(); }
        }
        private static void EasterEggLogic() {
            const int length = 6500;
            var minimum = new IdleEasterEggSchedule(delegate(int first, int last) { return first; });
            Assert(!minimum.Poll(0, true, length) && minimum.NextAtMs == 30000, "The first idle Easter egg cannot start before 30 seconds");
            Assert(!minimum.Poll(29999, true, length) && minimum.Poll(30000, true, length) && minimum.Playing, "The earliest allowed deadline starts one animation");
            Assert(!minimum.Poll(30001, true, length) && minimum.NextAtMs == 60000, "Playing does not trigger a second concurrent copy");
            minimum.Complete(36500);
            Assert(!minimum.Playing && minimum.NextAtMs == 60000 && !minimum.Poll(59999, true, length) && minimum.Poll(60000, true, length), "The interval is measured between starts and does not add playback time");
            var maximum = new IdleEasterEggSchedule(delegate(int first, int last) { return last - 1; });
            maximum.Poll(5000, true, length);
            Assert(maximum.NextAtMs == 125000 && !maximum.Poll(124999, true, length) && maximum.Poll(125000, true, length), "The two-minute inclusive maximum is supported");
            maximum.Poll(126000, false, length);
            Assert(!maximum.Playing && !maximum.NextAtMs.HasValue, "A task or user operation cancels the playback and queued deadline");
            Assert(!maximum.Poll(200000, true, length) && maximum.NextAtMs == 320000, "Returning to idle schedules a fresh complete delay, with no catch-up start");
            var random = new Random(28117); var sampled = new IdleEasterEggSchedule(random.Next); var values = new HashSet<int>();
            bool bounded = true;
            for (int i = 0; i < 1000; i++) { sampled.Reset(); sampled.Poll(i * 1000L, true, length); values.Add(sampled.LastDelayMs); bounded &= sampled.LastDelayMs >= 30000 && sampled.LastDelayMs <= 120000; }
            Assert(bounded && values.Count > 900, "Random delays vary while all 1000 samples remain within 30–120 seconds");
            foreach (var state in new[] { PetState.Idle, PetState.Completed, PetState.Interrupted, PetState.Failed }) Assert(IdleEasterEggSchedule.Eligible(state, true, false, false, false, false, true), "Only a resting animation may trigger after terminal state: " + state);
            foreach (var state in new[] { PetState.Connecting, PetState.Processing, PetState.Thinking, PetState.Working, PetState.Replying, PetState.Waiting }) Assert(!IdleEasterEggSchedule.Eligible(state, true, false, false, false, false, true), "Active task state blocks the idle Easter egg: " + state);
            Assert(!IdleEasterEggSchedule.Eligible(PetState.Idle, false, false, false, false, false, true), "Hiding the pet blocks triggers");
            Assert(!IdleEasterEggSchedule.Eligible(PetState.Idle, true, true, false, false, false, true), "Pausing animations blocks triggers");
            Assert(!IdleEasterEggSchedule.Eligible(PetState.Idle, true, false, true, false, false, true), "Dragging, mouse capture and an open context menu block triggers");
            Assert(!IdleEasterEggSchedule.Eligible(PetState.Idle, true, false, false, true, false, true), "Quota display retains priority over idle actions");
            Assert(!IdleEasterEggSchedule.Eligible(PetState.Idle, true, false, false, false, true, true) && !IdleEasterEggSchedule.Eligible(PetState.Idle, true, false, false, false, false, false), "Manual gestures and task cue animations finish before idle scheduling starts");
            var once = new AnimationClip { Name = "fixture", Frames = new BitmapSource[3], Durations = new[] { 300, 400, 500 }, Loop = false }; int frame = 0;
            Assert(!once.AdvanceFrame(ref frame, false) && frame == 1 && !once.AdvanceFrame(ref frame, false) && frame == 2 && once.AdvanceFrame(ref frame, false) && frame == 2, "One-shot playback visits every frame and finishes after the final frame instead of wrapping");
            Assert(IdleEasterEggSchedule.Duration(once) == 1200, "Playback duration comes from the artist's per-frame timings");
            once.Loop = true; frame = 2; Assert(!once.AdvanceFrame(ref frame, false) && frame == 0, "Existing repeating animations retain their loop behavior");
            frame = 2; Assert(once.AdvanceFrame(ref frame, true) && frame == 2, "Completed/failed task cues can play once before returning to idle");
            var slow = new IdleEasterEggSchedule(delegate(int first, int last) { return first; }); slow.Poll(0, true, 45000); slow.Poll(30000, true, 45000);
            Assert(slow.NextAtMs == 76000, "Longer artwork reserves idle transition time while retaining the two-minute limit");
            slow.Complete(200000);
            Assert(!slow.Playing && slow.NextAtMs == 230000, "A suspended application resumes without repeated overdue animations");
            var sprites = new Sprites();
            Assert(sprites.Clips.ContainsKey("idle") && sprites.Clips.ContainsKey("quota-thinking"), "Existing approved idle and quota clips remain available");
            EasterEggWindowLogic();
            J.Save(Path.Combine(root, "easter-status.json"), P("codeReady", true, "approvedArtworkEmbedded", sprites.Clips.ContainsKey("idle-baguette"), "minimumDelayMs", 30000, "maximumDelayMs", 120000, "clock", "Stopwatch", "windowsShown", false, "mouseInputUsed", false, "apiRequests", 0));
        }
        private static void EasterEggWindowLogic() {
            // No window is shown or mouse event dispatched. Approved idle
            // frames stand in for the not-yet-approved artwork in memory only.
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var assets = new Sprites(); var idle = assets.Clips["idle"];
            assets.Clips["idle-baguette"] = new AnimationClip { Name = "idle-baguette", Frames = new[] { idle.Frames[0], idle.Frames[0], idle.Frames[0] }, Durations = new[] { 600, 600, 600 }, Loop = false };
            var pet = new PetWindow(assets);
            try {
                Assert(!pet.IsVisible && pet.IdleEasterEggReady, "Off-screen playback validation shows no window and does not embed a candidate image");
                pet.Gesture("idle-baguette", 2.8); Pump(1100);
                Assert(pet.CurrentAnimation == "idle-baguette", "The WPF idle timer does not prematurely cancel an explicitly previewed one-shot clip");
                Pump(1100);
                Assert(pet.CurrentAnimation == "idle" && !pet.IdleEasterEggPlaying, "Actual WPF frame timing ends the one-shot clip in idle without replaying it");
                pet.Gesture("idle-baguette", 2.8); pet.SetState(PetState.Thinking);
                Assert(pet.CurrentAnimation == "running" && !pet.IdleEasterEggPlaying, "A real task state transition immediately overrides baguette playback");
                pet.SetState(PetState.Idle); pet.Gesture("idle-baguette", 2.8); pet.BeginQuotaInfo();
                Assert(pet.CurrentAnimation == "quota-thinking" && pet.QuotaVisible && !pet.IdleEasterEggPlaying, "Quota state immediately overrides baguette playback without showing a window");
                pet.DismissQuotaInfo(); Assert(pet.CurrentAnimation == "idle", "Dismissing quota restores idle after an interrupted Easter egg");
                pet.SetState(PetState.Completed); Pump(IdleEasterEggSchedule.Duration(assets.Clips["waving"]) + 200);
                Assert(pet.CurrentAnimation == "idle" && pet.CurrentState == PetState.Completed, "A completed-task cue finishes once then rests while retaining the completion status");
            } finally { pet.Close(); app.Shutdown(); }
        }
        private static object ComposerCatalog() {
            return P("data", new object[] {
                P("model", "sample-vision", "displayName", "示例视觉模型", "isDefault", true, "defaultReasoningEffort", "medium", "supportedReasoningEfforts", new object[] { P("reasoningEffort", "low"), P("reasoningEffort", "medium"), P("reasoningEffort", "high") }),
                P("model", "sample-text", "displayName", "示例文字模型", "inputModalities", new object[] { "text" }, "defaultReasoningEffort", "low", "supportedReasoningEfforts", new object[] { P("reasoningEffort", "low"), P("reasoningEffort", "high") }),
                P("model", "sample-hidden", "hidden", true), P("model", "sample-audio", "inputModalities", new object[] { "audio" })
            });
        }
        private static void ImageFixture(string path) {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen()) { drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 160, 160)); drawing.DrawEllipse(Brushes.RoyalBlue, null, new Point(80, 80), 60, 60); }
            var bitmap = new RenderTargetBitmap(160, 160, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
        }
        private static void OfficeFixture(string path, params string[] entries) {
            using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create)) {
                for (int i = 0; i < entries.Length; i += 2) using (var writer = new StreamWriter(zip.CreateEntry(entries[i]).Open(), new System.Text.UTF8Encoding(false))) writer.Write(entries[i + 1]);
            }
        }
        private static void PdfFixture(string path) {
            const string pageText = "BT /F1 14 Tf 20 200 Td (PDF_ATTACHMENT_MARKER) Tj ET\n";
            string[] objects = {
                "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", "<< /Length " + pageText.Length + " >>\nstream\n" + pageText + "endstream"
            };
            var pdf = new System.Text.StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
            for (int i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n"); }
            int xref = pdf.Length; pdf.Append("xref\n0 6\n0000000000 65535 f \n"); foreach (int offset in offsets) pdf.Append(offset.ToString("0000000000")).Append(" 00000 n \n");
            pdf.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n"); File.WriteAllText(path, pdf.ToString(), System.Text.Encoding.ASCII);
        }
        private static void ComposerLogic() {
            var catalog = ModelCatalog.Read(ComposerCatalog());
            Assert(catalog.Count == 2 && catalog[0].SupportsImages && !catalog[1].SupportsImages, "Hidden and audio-only models are excluded; older catalogs retain image support");
            Assert(ModelCatalog.Select(catalog, "sample-text").Id == "sample-text" && ModelCatalog.Select(catalog, "removed-model").Id == "sample-vision", "Saved valid models survive and removed models fall back to the advertised default");
            Assert(ModelCatalog.SelectEffort(catalog[1], "medium") == "low" && ModelCatalog.SelectEffort(catalog[1], "high") == "high", "Switching models retains a supported effort or chooses the new model default");
            string files = Path.Combine(root, "fixtures"), copies = Path.Combine(root, "copies"); Directory.CreateDirectory(files);
            string document = Path.Combine(files, "中文 文件.txt"), image = Path.Combine(files, "图像.png");
            File.WriteAllText(document, "ATTACHMENT_SNAPSHOT_ORIGINAL", new System.Text.UTF8Encoding(false)); ImageFixture(image);
            var sources = new List<ChatAttachment> { AttachmentInput.FromFile(document), AttachmentInput.FromFile(image) };
            bool rejected = false; try { AttachmentInput.Validate(sources, catalog[1]); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "Text-only models reject images before a draft is accepted");
            var snapshot = AttachmentInput.Snapshot(sources, copies);
            File.WriteAllText(document, "CHANGED_AFTER_SELECTION");
            Assert(snapshot[0].Path != document && File.ReadAllText(snapshot[0].Path) == "ATTACHMENT_SNAPSHOT_ORIGINAL", "Accepted attachments are stable copies after the source is edited");
            object[] input = AttachmentInput.Build("检查附件", snapshot);
            Assert(input.Length == 2 && J.S(input[1], "type") == "localImage" && J.S(input[1], "path") == snapshot[1].Path, "Images enter the protocol as native localImage input");
            Assert(J.S(input[0], "text").Contains(J.Serialize(snapshot[0].Path)) && !J.S(input[0], "text").Contains("CHANGED_AFTER_SELECTION"), "Document references point to the snapshot with escaped Unicode/space paths");
            Assert(J.S(input[0], "text").Contains("ATTACHMENT_SNAPSHOT_ORIGINAL"), "Extracted attachment text is included directly, independently of Codex file-tool startup");
            string native = Path.Combine(root, "native-documents-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(native);
            string word = Path.Combine(native, "test.docx"), excel = Path.Combine(native, "test.xlsx"), slides = Path.Combine(native, "test.pptx"), pdf = Path.Combine(native, "test.pdf");
            OfficeFixture(word, "word/document.xml", "<document><body><p><r><t>中文 Word</t></r><r><t> marker</t></r></p></body></document>");
            OfficeFixture(excel, "xl/sharedStrings.xml", "<sst><si><t>Shared Marker</t></si></sst>", "xl/worksheets/sheet1.xml", "<worksheet><sheetData><row><c r='A1' t='s'><v>0</v></c><c r='B1'><v>42</v></c><c r='C1' t='inlineStr'><is><t>内联文字</t></is></c></row></sheetData></worksheet>");
            OfficeFixture(slides, "ppt/slides/slide2.xml", "<slide><p><t>SECOND_SLIDE</t></p></slide>", "ppt/slides/slide1.xml", "<slide><p><t>FIRST_SLIDE</t></p></slide>"); PdfFixture(pdf);
            var nativeCopies = AttachmentInput.Snapshot(new List<ChatAttachment> { AttachmentInput.FromFile(word), AttachmentInput.FromFile(excel), AttachmentInput.FromFile(slides), AttachmentInput.FromFile(pdf) }, copies);
            Assert(nativeCopies[0].ExtractedText.Contains("中文 Word marker") && nativeCopies[1].ExtractedText.Contains("A1=Shared Marker") && nativeCopies[1].ExtractedText.Contains("B1=42") && nativeCopies[1].ExtractedText.Contains("C1=内联文字"), "Word text and Excel shared, inline and numeric cells are extracted without installed Office");
            Assert(nativeCopies[2].ExtractedText.IndexOf("FIRST_SLIDE") < nativeCopies[2].ExtractedText.IndexOf("SECOND_SLIDE"), "PowerPoint text follows numeric slide order");
            Assert(nativeCopies[3].ExtractedText.Contains("PDF_ATTACHMENT_MARKER"), "The local PDF helper extracts selectable PDF text without a Codex tool request");
            string unicode = Path.Combine(native, "unicode.txt"); File.WriteAllText(unicode, new string('a', 59999) + "\uD83D\uDE00" + "tail", new System.Text.UTF8Encoding(false));
            var unicodeCopy = AttachmentInput.Snapshot(new List<ChatAttachment> { AttachmentInput.FromFile(unicode) }, copies)[0];
            Assert(unicodeCopy.ReadHint == "已提取部分文字" && !unicodeCopy.ExtractedText.Contains("\uD83D") && unicodeCopy.ExtractedText.Contains("完整内容"), "Text truncation is labeled and cannot split an emoji surrogate pair");
            Assert(J.S(AttachmentInput.Build("", snapshot)[0], "text").StartsWith("请查看所附文件。"), "Attachment-only messages have a meaningful default instruction");
            string other = Path.Combine(files, "other"); Directory.CreateDirectory(other); string sameName = Path.Combine(other, Path.GetFileName(document)); File.WriteAllText(sameName, "SECOND_FILE");
            var duplicates = AttachmentInput.Snapshot(new List<ChatAttachment> { AttachmentInput.FromFile(document), AttachmentInput.FromFile(sameName) }, copies);
            Assert(duplicates[0].Path != duplicates[1].Path && File.ReadAllText(duplicates[1].Path) == "SECOND_FILE", "Different source files with the same name never overwrite each other");
            string invalid = Path.Combine(files, "invalid.png"); File.WriteAllText(invalid, "not an image"); int batches = Directory.GetDirectories(copies).Length;
            rejected = false; try { AttachmentInput.Snapshot(new List<ChatAttachment> { AttachmentInput.FromFile(invalid) }, copies); } catch { rejected = true; }
            Assert(rejected && Directory.GetDirectories(copies).Length == batches, "Invalid images fail before acceptance and roll back their incomplete copy");
            string large = Path.Combine(files, "oversize.txt"); using (var file = File.Create(large)) file.SetLength(AttachmentInput.MaximumFileSize + 1);
            rejected = false; try { AttachmentInput.FromFile(large); } catch (IOException) { rejected = true; } Assert(rejected, "Oversize attachments are rejected"); File.Delete(large);
            rejected = false; var tooMany = new List<ChatAttachment>(); for (int i = 0; i < 11; i++) tooMany.Add(sources[0]);
            try { AttachmentInput.Validate(tooMany, null); } catch (IOException) { rejected = true; } Assert(rejected, "Attachment count limits are enforced");
            string store = Path.Combine(root, "composer-session.json");
            using (var controller = new Controller(store, root, delegate(Action action) { action(); })) {
                controller.NewChat(root); controller.ThreadId = "existing-thread"; controller.Models.AddRange(catalog);
                Assert(controller.SetModel("sample-vision") && controller.SetEffort("high") && controller.SetModel("sample-text") && controller.Effort == "high" && controller.ThreadId == "existing-thread", "Model changes apply the chosen supported effort without clearing conversation identity");
                controller.Status.Busy = true; Assert(!controller.SetModel("sample-vision") && !controller.SetEffort("low"), "Model and effort changes are blocked during an active turn"); controller.Status.Busy = false;
                Assert(!controller.SetEffort("made-up") && !controller.SetModel("made-up"), "Invalid manually supplied model/effort choices cannot enter settings");
                var entry = new ChatEntry("user", "attachment-message", "检查附件"); entry.Attachments.AddRange(snapshot); controller.Messages.Add(entry); controller.Save();
            }
            using (var restored = new Controller(store, root, delegate(Action action) { action(); })) Assert(restored.Model == "sample-text" && restored.Effort == "high" && restored.Messages[0].Attachments.Count == 2 && restored.Messages[0].Attachments[0].Path == snapshot[0].Path, "Restart preserves model, effort, attachment cards and immutable file references");
            J.Save(Path.Combine(root, "legacy-session.json"), P("workspace", root, "model", "sample-text", "threadId", "old-thread", "messages", new object[] { P("role", "user", "id", "old", "text", "legacy text") }));
            using (var legacy = new Controller(Path.Combine(root, "legacy-session.json"), root, delegate(Action action) { action(); })) Assert(legacy.ThreadId == "old-thread" && legacy.Messages[0].Text == "legacy text" && legacy.Effort == "" && legacy.Messages[0].Attachments.Count == 0, "Old portable session files remain compatible");
        }
        private static async Task Catalog() {
            Directory.CreateDirectory(Path.Combine(root, "Tasks"));
            using (var controller = new Controller(Path.Combine(root, "catalog-session.json"), Path.Combine(root, "Tasks"), delegate(Action action) { action(); })) {
                await controller.ConnectAsync(); var models = new List<object>();
                foreach (var model in controller.Models) { var efforts = new List<object>(); foreach (var effort in model.Efforts) efforts.Add(P("id", effort.Id, "description", effort.Description)); models.Add(P("id", model.Id, "name", model.Name, "defaultEffort", model.Effort, "efforts", efforts, "images", model.SupportsImages)); }
                Assert(models.Count > 0 && controller.AccountLabel.StartsWith("ChatGPT") && controller.ThreadId == "", "Live model catalog is read using ChatGPT login without inference");
                J.Save(Path.Combine(root, "catalog.json"), P("models", models, "model", controller.Model, "effort", controller.Effort, "executable", controller.Client.Executable, "inferenceStarted", false, "mouseInputUsed", false));
                Console.WriteLine("Models: " + String.Join(", ", controller.Models.ConvertAll(m => m.Id)));
            }
        }
        private static async Task ComposerLive() {
            string workspace = Path.Combine(root, "Tasks"); Directory.CreateDirectory(workspace);
            string document = Path.Combine(root, "附件样本.txt"), image = Path.Combine(root, "image.png");
            const string marker = "TETO_ATTACH_K7P"; File.WriteAllText(document, "验证码：" + marker, new System.Text.UTF8Encoding(false)); ImageFixture(image);
            using (var controller = new Controller(Path.Combine(root, "live-composer-session.json"), workspace, delegate(Action action) { action(); })) {
                await controller.ConnectAsync(); controller.NewChat(workspace);
                var model = ModelCatalog.Select(controller.Models, controller.Model); if (!model.SupportsImages) model = controller.Models.Find(m => m.SupportsImages);
                Assert(model != null && controller.SetModel(model.Id), "The live image test selects an advertised image-capable model");
                if (model.Efforts.Count > 0) controller.SetEffort(model.Efforts[0].Id);
                controller.ApprovalRequested += delegate(RpcEvent request) {
                    string command = J.S(request.Params, "command");
                    bool scopedRead = request.Method == "item/commandExecution/requestApproval" && command.Contains(root) && !command.Contains("Remove-Item") && !command.Contains("Set-Content") && !command.Contains("Invoke-WebRequest");
                    controller.Answer(request, J.O("decision", scopedRead ? "accept" : "decline"));
                };
                bool accepted = await controller.SendAsync("这是附件集成测试。请读取所附文字文件中的验证码，并识别所附图片中圆形的主要颜色。只回复“验证码|颜色”，颜色用英文小写，并记住验证码。", new List<ChatAttachment> { AttachmentInput.FromFile(document), AttachmentInput.FromFile(image) });
                await Wait(controller, 180000);
                Assert(accepted && controller.Status.State == PetState.Completed && controller.Messages.Exists(m => m.Role == "assistant" && m.Text.Contains(marker) && m.Text.ToLowerInvariant().Contains("blue")), "A real turn reads the attached document and recognizes the directly attached image");
                string thread = controller.ThreadId, firstModel = controller.Model, firstEffort = controller.Effort;
                object resumed = await controller.Client.RequestAsync("thread/resume", P("threadId", thread), 60000);
                Assert(J.S(resumed, "model") == firstModel && J.S(resumed, "reasoningEffort") == firstEffort, "The server retained the chosen model and effort after inference");
                var nextModel = controller.Models.Find(m => m.Id != firstModel);
                if (nextModel != null) controller.SetModel(nextModel.Id);
                var current = ModelCatalog.Select(controller.Models, controller.Model);
                if (current.Efforts.Exists(e => e.Id == "medium")) controller.SetEffort("medium");
                else if (current.Efforts.Count > 1) controller.SetEffort(current.Efforts[1].Id);
                await controller.SendAsync("不要调用工具。只回复你记住的验证码。"); await Wait(controller, 180000);
                Assert(controller.ThreadId == thread && controller.Status.State == PetState.Completed && controller.Messages[controller.Messages.Count - 1].Text.Contains(marker), "Switching model/effort keeps the existing thread and remembers earlier attachment context");
                resumed = await controller.Client.RequestAsync("thread/resume", P("threadId", thread), 60000);
                Assert(J.S(resumed, "model") == controller.Model && J.S(resumed, "reasoningEffort") == controller.Effort, "The changed model and effort are actually applied at the server");
                J.Save(Path.Combine(root, "live-composer.json"), P("firstModel", firstModel, "firstEffort", firstEffort, "nextModel", controller.Model, "nextEffort", controller.Effort, "attachmentReadPassed", true, "imageReadPassed", true, "historyPreserved", true, "apiKeyUsed", false, "mouseInputUsed", false));
            }
        }
        private static async Task InspectComposer() {
            using (var controller = new Controller(Path.Combine(root, "live-composer-session.json"), Path.Combine(root, "Tasks"), delegate(Action action) { action(); })) {
                await controller.ConnectAsync();
                object read = await controller.Client.RequestAsync("thread/read", P("threadId", controller.ThreadId, "includeTurns", true), 60000);
                foreach (object turn in J.A(J.Get(J.Get(read, "thread"), "turns"))) foreach (object item in J.A(J.Get(turn, "items"))) {
                    string type = J.S(item, "type");
                    if (type == "commandExecution") Console.WriteLine(J.Serialize(P("type", type, "command", J.S(item, "command"), "output", J.S(item, "aggregatedOutput"))));
                }
            }
        }
        private static void Preview() {
            // Static, off-screen rendering only. No window is shown and no
            // mouse event, input injection or account request is performed.
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var bubble = new QuotaBubbleWindow();
            var previewNow = DateTime.UtcNow; bubble.SetSnapshot(QuotaDisplayExample(previewNow), previewNow);
            Render((FrameworkElement)bubble.Content, bubble.Width, bubble.Height, Path.Combine(root, "quota-bubble-example.png"));
            var assets = new Sprites();
            var scene = new Canvas { Width = 460, Height = 460, Background = Theme.Color("#F1F1F1") };
            var figure = new System.Windows.Controls.Image { Source = assets.Clips["quota-thinking"].Frames[0], Width = 192, Height = 208 };
            Canvas.SetLeft(figure, 40); Canvas.SetTop(figure, 215); scene.Children.Add(figure);
            var bubbleView = new System.Windows.Shapes.Rectangle { Width = bubble.Width, Height = bubble.Height, Fill = new VisualBrush((Visual)bubble.Content) };
            var bubblePlace = QuotaPlacement.Place(new Rect(0, 0, scene.Width, scene.Height), new Rect(40, 215, 192, 254), new Size(bubble.Width, bubble.Height));
            Canvas.SetLeft(bubbleView, bubblePlace.Left); Canvas.SetTop(bubbleView, bubblePlace.Top); scene.Children.Add(bubbleView);
            var caption = Theme.Text("布局预览 · 额度数字为示例", 11, Theme.Muted); Canvas.SetLeft(caption, 26); Canvas.SetTop(caption, 435); scene.Children.Add(caption);
            Render(scene, scene.Width, scene.Height, Path.Combine(root, "quota-scene-example.png"));
            bubble.SetUnavailable(); Render((FrameworkElement)bubble.Content, bubble.Width, bubble.Height, Path.Combine(root, "quota-unavailable.png"));
            bubble.Close();
            PreviewPanels(assets);
            J.Save(Path.Combine(root, "preview.json"), P("ok", true, "staticOnly", true, "windowsShown", false, "mouseInputUsed", false, "manualInteractionTesting", "pending user", "quotaValuesAreExamples", true));
            Console.WriteLine("Static panel and quota previews rendered. Mouse interaction testing is left to the user.");
        }
        private static void PreviewPanels(Sprites assets) {
            string workspace = Path.Combine(root, "Tasks"); Directory.CreateDirectory(workspace);
            using (var controller = new Controller(Path.Combine(root, "preview-sample-session.json"), workspace, delegate(Action action) { action(); })) {
                controller.NewChat(workspace);
                controller.AccountLabel = "ChatGPT · 示例账号";
                string catalogPath = Path.Combine(root, "catalog.json");
                if (File.Exists(catalogPath)) {
                    foreach (object entry in J.A(J.Get(J.Parse(File.ReadAllText(catalogPath)), "models"))) {
                        var model = new CodexModel { Id = J.S(entry, "id"), Name = J.S(entry, "name"), Effort = J.S(entry, "defaultEffort"), SupportsImages = J.B(entry, "images") };
                        foreach (object effort in J.A(J.Get(entry, "efforts"))) model.Efforts.Add(new CodexEffort { Id = J.S(effort, "id"), Description = J.S(effort, "description") });
                        controller.Models.Add(model);
                    }
                } else controller.Models.AddRange(ModelCatalog.Read(ComposerCatalog()));
                controller.SetModel(controller.Models[0].Id);
                var pet = new PetWindow(assets); var chat = new ChatWindow(controller, pet);
                double defaultWidth = chat.Width, defaultHeight = chat.Height;
                var surface = (Grid)chat.Content; int handles = 0;
                foreach (UIElement child in surface.Children) if (child is System.Windows.Controls.Primitives.Thumb) handles++;
                Render((FrameworkElement)chat.Content, chat.Width, chat.Height, Path.Combine(root, "panel-default.png"));
                chat.Width = chat.MinWidth; chat.Height = chat.MinHeight;
                Render((FrameworkElement)chat.Content, chat.Width, chat.Height, Path.Combine(root, "panel-minimum.png"));
                Assert(J.B(chat.ComposerProperties(), "toolbarFits"), "Composer controls fit the minimum window width");
                string image = Path.Combine(root, "预览图片.png"), document = Path.Combine(root, "项目说明.txt"); ImageFixture(image); File.WriteAllText(document, "仅用于离屏排版预览。");
                var draft = new List<ChatAttachment> { AttachmentInput.FromFile(image), AttachmentInput.FromFile(document) };
                chat.PreviewDraft("请帮我查看这些附件。", draft); chat.Width = defaultWidth; chat.Height = defaultHeight;
                Render((FrameworkElement)chat.Content, chat.Width, chat.Height, Path.Combine(root, "panel-attachments.png"));
                var defaultComposer = chat.ComposerProperties(); Assert(J.B(defaultComposer, "toolbarFits") && J.B(defaultComposer, "sendingEnabled"), "Attachment draft is sendable and controls fit the default window");
                chat.Width = chat.MinWidth; chat.Height = chat.MinHeight;
                Render((FrameworkElement)chat.Content, chat.Width, chat.Height, Path.Combine(root, "panel-minimum-attachments.png"));
                var minimumComposer = chat.ComposerProperties(); Assert(J.B(minimumComposer, "toolbarFits"), "Attachment cards remain scrollable with the toolbar inside the minimum width");
                controller.Status.Busy = true; chat.Refresh();
                Render((FrameworkElement)chat.Content, chat.Width, chat.Height, Path.Combine(root, "panel-busy.png"));
                Assert(J.B(chat.ComposerProperties(), "stopVisible") && !J.B(chat.ComposerProperties(), "sendingEnabled"), "An active task replaces the send action with stop");
                controller.Status.Busy = false; chat.PreviewDraft("", new List<ChatAttachment>());
                J.Save(Path.Combine(root, "composer-properties.json"), P("default", defaultComposer, "minimum", minimumComposer, "mouseInputUsed", false));
                var sampleUser = new ChatEntry("user", "preview-user", "请帮我整理当前任务的进度，并列出接下来要处理的事项。"); sampleUser.Attachments.Add(draft[1]); controller.Messages.Add(sampleUser);
                controller.Messages.Add(new ChatEntry("assistant", "preview-assistant", "已经整理好当前进度。\n\n你可以拖动窗口的边缘或四角调整大小。对话内容会按新的宽度换行，输入区和发送按钮位于窗口底部。"));
                chat.Refresh(); chat.Width = defaultWidth; chat.Height = defaultHeight;
                Render((FrameworkElement)chat.Content, chat.Width, chat.Height, Path.Combine(root, "panel-conversation.png"));
                chat.Width = 720; chat.Height = 680;
                Render((FrameworkElement)chat.Content, chat.Width, chat.Height, Path.Combine(root, "panel-large.png"));
                J.Save(Path.Combine(root, "panel-properties.json"), P("defaultWidth", defaultWidth, "defaultHeight", defaultHeight, "minWidth", chat.MinWidth, "minHeight", chat.MinHeight, "resizeMode", chat.ResizeMode.ToString(), "resizeHandleCount", handles, "chatTopmost", chat.Topmost, "petTopmost", pet.Topmost, "chatOwnerIsPet", chat.Owner == pet, "windowsShown", chat.IsVisible || pet.IsVisible, "mouseInputUsed", false));
                chat.ExitWindow(); pet.Close();
            }
        }
        private static void Offline() {
            QuotaParsing();
            var motion = new DragMotion(); motion.Begin(new Point(100, 100));
            motion.Move(new Point(125, 100), Matrix.Identity);
            Assert(motion.Animation == "running-right", "Dragging right selects the right-running animation");
            motion.Move(new Point(120, 100), Matrix.Identity);
            Assert(motion.Animation == "running-left" && motion.Offset.X == 20, "Reversing left switches while still right of the press point");
            motion.Move(new Point(123, 100), Matrix.Identity);
            Assert(motion.Animation == "running-right", "Reversing right switches without crossing the press point");
            motion.Begin(new Point(100, 100)); motion.Move(new Point(75, 100), Matrix.Identity); motion.Move(new Point(80, 100), Matrix.Identity);
            Assert(motion.Animation == "running-right" && motion.Offset.X == -20, "Rightward movement on the left side of the press point runs right");
            motion.Move(new Point(80, 120), Matrix.Identity);
            Assert(motion.Animation == "running-right", "Vertical movement does not flip the last running direction");
            motion.Begin(new Point(100, 100)); motion.Move(new Point(100, 120), Matrix.Identity);
            Assert(motion.Dragging && motion.Direction == 0, "Purely vertical dragging does not invent a horizontal direction");
            var catalog = ModelCatalog.Read(P("data", new object[] {
                P("model", "hidden-model", "hidden", true),
                P("model", "visible-secondary", "defaultReasoningEffort", "medium"),
                P("model", "visible-default", "isDefault", true, "defaultReasoningEffort", "low"),
                P("model", "audio-only", "inputModalities", new object[] { "audio" }) }));
            Assert(catalog.Count == 2, "Model selection excludes hidden and non-text models");
            Assert(ModelCatalog.Select(catalog, "unsupported-configured-model").Id == "visible-default", "Unsupported configured models fall back to the advertised default");
            Assert(ModelCatalog.Select(catalog, "visible-secondary").Id == "visible-secondary", "A saved model is preserved when it remains in the catalog");
            var state = new TaskState(); state.Begin("own");
            state.Apply("turn/started", P("threadId", "own", "turn", P("id", "turn1")));
            state.Apply("item/started", P("threadId", "other", "turnId", "turn1", "item", P("id", "r0", "type", "reasoning")));
            Assert(state.State == PetState.Processing, "Other conversations cannot trigger thinking");
            state.Apply("item/started", P("threadId", "own", "turnId", "turn1", "item", P("id", "r1", "type", "reasoning")));
            Assert(state.State == PetState.Thinking && TaskState.Animation(state.State) == "running", "Reasoning drives the existing thinking animation");
            state.Apply("item/started", P("threadId", "own", "turnId", "turn1", "item", P("id", "c1", "type", "commandExecution")));
            state.Approval("approval1", true); Assert(state.State == PetState.Waiting, "Approval supersedes active execution");
            state.Approval("approval1", false); Assert(state.State == PetState.Working, "Answer restores execution instead of leaving a waiting animation");
            state.Apply("item/completed", P("threadId", "own", "turnId", "turn1", "item", P("id", "c1", "type", "commandExecution")));
            Assert(state.State == PetState.Thinking, "Completing a tool restores active reasoning");
            state.Apply("error", P("threadId", "own", "turnId", "turn1", "willRetry", true)); Assert(state.Busy, "Transient retries do not report failure or completion");
            state.Apply("turn/completed", P("threadId", "own", "turn", P("id", "turn1", "status", "interrupted")));
            Assert(state.State == PetState.Interrupted && !state.Busy, "Interrupted tasks are not successful tasks");
            state.Begin("own"); state.Apply("turn/started", P("threadId", "own", "turn", P("id", "turn2")));
            state.Apply("turn/completed", P("threadId", "own", "turn", P("id", "turn1", "status", "completed")));
            Assert(state.Busy, "Late completion from the previous turn is ignored");
            state.Apply("turn/completed", P("threadId", "own", "turn", P("id", "turn2", "status", "failed")));
            Assert(state.State == PetState.Failed && !state.Busy, "Failed tasks drive the failure animation");
            var assets = new Sprites(); int total = 0; foreach (var clip in assets.Clips.Values) if (clip.Name != "quota-thinking") total += clip.Frames.Length;
            Assert(total == 57 && (assets.Clips.Count == 9 || assets.Clips.Count == 10 && assets.Clips.ContainsKey("quota-thinking")), "All 57 original animation frames are packaged without replacement by quota art");
            if (assets.Clips.ContainsKey("quota-thinking")) {
                var extra = assets.Clips["quota-thinking"]; int loop = 0; foreach (int delay in extra.Durations) loop += delay;
                Assert(extra.Frames.Length == 5 && loop >= 1000 && loop <= 3000 && extra.Frames[0].PixelWidth == 192 && extra.Frames[0].PixelHeight == 208, "Approved quota animation has the expected dimensions and a short repeatable loop");
                int closedAt = extra.Durations[0] + extra.Durations[1], blinks = (5000 - closedAt - 1) / loop + 1;
                Assert(blinks >= 1 && blinks <= 2, "A five-second result display contains one or two complete blinks");
            }
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var pet = new PetWindow(assets);
            pet.Left = 30; pet.Top = 30;
            int opened = 0; pet.OpenChat = delegate { opened++; };
            pet.BeginPetDrag(new Point(100, 100)); pet.EndPetDrag(true);
            Assert(opened == 0 && pet.CurrentAnimation == "waving", "Character clicks keep interaction feedback without opening chat");
            pet.ChatBubble.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Assert(opened == 1, "The compose bubble opens the task panel");
            pet.BeginPetDrag(new Point(100, 100)); pet.MovePetDrag(new Point(140, 100), Matrix.Identity);
            bool wasRunning = pet.CurrentAnimation == "running-right";
            pet.EndPetDrag(true);
            Assert(wasRunning && pet.CurrentAnimation == "idle" && opened == 1, "Releasing a drag immediately restores idle without opening chat");
            pet.SetState(PetState.Completed); pet.BeginPetDrag(new Point(100, 100)); pet.MovePetDrag(new Point(80, 100), Matrix.Identity); pet.EndPetDrag(true);
            Assert(pet.CurrentAnimation == "idle", "Dragging after a finished task also returns immediately to idle");
            pet.Gesture("jumping", 5); pet.BeginPetDrag(new Point(100, 100)); pet.MovePetDrag(new Point(120, 100), Matrix.Identity); pet.EndPetDrag(false);
            Assert(pet.CurrentAnimation == "idle" && opened == 1, "Loss of capture clears both running and an earlier timed gesture");
            pet.SetState(PetState.Thinking); pet.BeginPetDrag(new Point(100, 100)); pet.MovePetDrag(new Point(80, 100), Matrix.Identity); pet.SetState(PetState.Working);
            bool dragStillVisible = pet.CurrentAnimation == "running-left"; pet.EndPetDrag(true);
            Assert(dragStillVisible && pet.CurrentAnimation == "running" && pet.CurrentState == PetState.Working, "Task updates preserve dragging and restore the current work animation on release");
            pet.SetState(PetState.Idle);
            QuotaUi(pet, assets, delegate { return opened; });
            QuotaFading();
            pet.SetScale(0.65); Render((FrameworkElement)pet.Content, pet.Width, pet.Height, Path.Combine(root, "pet-smallest.png"));
            pet.SetScale(1.6); Render((FrameworkElement)pet.Content, pet.Width, pet.Height, Path.Combine(root, "pet-largest.png")); pet.SetScale(1);
            var controller = new Controller(Path.Combine(root, "offline-session.json"), root, delegate(Action action) { action(); });
            var chat = new ChatWindow(controller, pet);
            foreach (var entry in new[] { new ChatEntry("user", "example-user", "帮我检查当前项目的文件。"), new ChatEntry("assistant", "example-assistant", "正在查看项目文件，完成后会把结果放在这里。") }) controller.Messages.Add(entry);
            foreach (PetState selected in new[] { PetState.Idle, PetState.Thinking, PetState.Working, PetState.Waiting, PetState.Completed, PetState.Interrupted, PetState.Failed }) {
                controller.Status.State = selected; chat.Refresh(); Render((FrameworkElement)pet.Content, pet.Width, pet.Height, Path.Combine(root, "pet-" + selected + ".png"));
                if (selected == PetState.Thinking) Render((FrameworkElement)chat.Content, chat.Width, chat.Height, Path.Combine(root, "panel-thinking.png"));
                Assert(pet.CurrentAnimation == TaskState.Animation(selected), "Rendered animation state " + selected);
            }
            chat.ExitWindow(); pet.Close(); controller.Dispose();
        }
        private static void Render(FrameworkElement element, double width, double height, string path) {
            element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout(); Theme.Snapshot(element, path);
        }
        private static void QuotaParsing() {
            object codex = P("limitId", "codex", "primary", P("usedPercent", 27, "windowDurationMins", 300, "resetsAt", 1900000000L), "secondary", P("usedPercent", 61, "windowDurationMins", 10080));
            var sample = QuotaSnapshot.Read(P("rateLimits", P("primary", P("usedPercent", 0, "windowDurationMins", 300)), "rateLimitsByLimitId", P("codex", codex, "other", P("primary", P("usedPercent", 99, "windowDurationMins", 300)))));
            Assert(sample.FiveHours.RemainingPercent == 73 && sample.Week.RemainingPercent == 39 && sample.LimitId == "codex", "Codex bucket takes precedence and percentages represent remaining usage");
            Assert(sample.FiveHours.ResetsAt == 1900000000L, "Reset timestamps retain their 64-bit value");
            var legacy = QuotaSnapshot.Read(P("rateLimits", codex));
            Assert(legacy.FiveHours.Display == "73%" && legacy.Week.Display == "39%", "Legacy single-bucket responses remain supported");
            var swapped = QuotaSnapshot.Read(P("rateLimits", P("primary", P("usedPercent", 18, "windowDurationMins", 10080), "secondary", P("usedPercent", 12.5, "windowDurationMins", 300))));
            Assert(swapped.Week.RemainingPercent == 82 && swapped.FiveHours.Display == "87.5%", "Window duration identifies five-hour and weekly limits even when their order changes");
            Assert(QuotaWindow.Read(P("usedPercent", -3)).Display == "100%" && QuotaWindow.Read(P("usedPercent", 130)).Display == "0%", "Remaining percentages are bounded from zero to one hundred");
            Assert(QuotaWindow.Read(P("usedPercent", null)).Display == "暂不可用" && QuotaWindow.Read(P()).Display == "暂不可用" && QuotaWindow.Read(P("usedPercent", "NaN")).Display == "暂不可用", "Null, missing and non-finite usage never become full or zero allowance");
            var unknown = QuotaSnapshot.Read(P("rateLimits", P("primary", P("usedPercent", 20, "windowDurationMins", 60), "secondary", P("usedPercent", 30))));
            Assert(unknown.FiveHours == null && unknown.Week == null, "Unknown durations are not mislabeled as five-hour or weekly limits");
            var absent = QuotaSnapshot.Read(P("rateLimits", codex, "rateLimitsByLimitId", P("codex", null)));
            Assert(absent.FiveHours == null && absent.Week == null, "An explicitly unavailable Codex bucket is not replaced by stale legacy values");
            var interaction = new QuotaInteraction(); var now = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
            int ticket = interaction.Begin(); Assert(!interaction.Expire(ticket, now.AddMinutes(1)) && interaction.Visible, "Network waiting does not consume the five-second display duration");
            Assert(interaction.Present(ticket, now.AddMinutes(1)) && !interaction.Expire(ticket, now.AddMinutes(1).AddMilliseconds(4999)), "Results remain visible for the complete five seconds");
            Assert(interaction.Expire(ticket, now.AddMinutes(1).AddSeconds(5)) && !interaction.Visible, "Five seconds after the result, the interaction expires");
            ticket = interaction.Begin(); interaction.Dismiss(); Assert(!interaction.Present(ticket, now), "A delayed response cannot reopen a left-click-dismissed query");
            ticket = interaction.Begin(); int latest = interaction.Begin(); Assert(!interaction.Present(ticket, now) && interaction.Present(latest, now) && !interaction.Present(latest, now.AddSeconds(1)), "Only the latest response can open the bubble and duplicate results cannot extend its timer");
            foreach (var area in new[] { new Rect(0, 0, 1920, 1040), new Rect(-1920, -200, 1920, 1080) }) {
                foreach (var point in new[] { area.TopLeft, new Point(area.Right - 192, area.Bottom - 254), new Point(area.Left + 600, area.Top + 400) }) {
                    var rect = QuotaPlacement.Place(area, new Rect(point, new Size(192, 254)), new Size(224, 142));
                    Assert(area.Contains(rect), "Quota bubble stays within the selected monitor: " + point);
                }
            }
            var normal = QuotaPlacement.Place(new Rect(0, 0, 1920, 1040), new Rect(600, 400, 192, 254), new Size(224, 142));
            Assert(normal.Left > 600 + 96 && normal.Bottom < 400 + 12, "Bubble follows the upward-right gaze when screen space is available");
        }
        private static void QuotaUi(PetWindow pet, Sprites assets, Func<int> opened) {
            int queries = 0; pet.QueryQuota = delegate { queries++; };
            foreach (object entry in pet.BuildMenu().Items) {
                var item = entry as MenuItem; if (item != null && Convert.ToString(item.Header) == "查询剩余额度") item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
            Assert(queries == 1, "The right-click quota menu invokes its bound query action");
            string pose = assets.Clips.ContainsKey("quota-thinking") ? "quota-thinking" : "running";
            var snapshot = QuotaSnapshot.Read(P("rateLimits", P("primary", P("usedPercent", 27, "windowDurationMins", 300), "secondary", P("usedPercent", 61, "windowDurationMins", 10080))));
            int ticket = pet.BeginQuotaInfo(), beforeOpened = opened();
            Assert(pet.CurrentAnimation == pose && pet.QuotaVisible, "Quota queries immediately select the quota pose");
            Assert(pet.ShowQuotaResult(ticket, snapshot), "A current result creates the quota bubble");
            Render((FrameworkElement)pet.QuotaBubble.Content, pet.QuotaBubble.Width, pet.QuotaBubble.Height, Path.Combine(root, "quota-bubble-example.png"));
            Render((FrameworkElement)pet.Content, pet.Width, pet.Height, Path.Combine(root, "quota-pose.png"));
            // A press no longer closes quota: the completed character release
            // distinguishes a stationary click from a drag. Mouse acceptance
            // is manual; validate the dismissal result here as domain state.
            Assert(pet.ConsumeQuotaClick() && pet.CurrentAnimation == "idle" && opened() == beforeOpened && !pet.QuotaVisible, "A completed quota click restores idle without opening chat or triggering a gesture");
            Assert(!pet.ShowQuotaResult(ticket, snapshot), "Dismissed UI rejects a delayed query response");
            ticket = pet.BeginQuotaInfo(); pet.SetState(PetState.Working);
            Assert(pet.CurrentAnimation == pose && pet.CurrentState == PetState.Working, "Task updates retain quota pose while remembering the current task state");
            pet.DismissQuotaInfo(); Assert(pet.CurrentAnimation == "running", "Closing quota resumes the task animation when a task is still active");
            pet.SetState(PetState.Idle); ticket = pet.BeginQuotaInfo(); Assert(pet.ShowQuotaError(ticket), "A failed query can show a truthful unavailable bubble");
            pet.QuotaBubble.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            Assert(pet.CurrentAnimation == "idle" && !pet.QuotaVisible, "The bubble click handler also closes it immediately");
            ticket = pet.BeginQuotaInfo(); pet.ShowQuotaResult(ticket, snapshot);
            var frame = new DispatcherFrame(); var wait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5200) };
            wait.Tick += delegate { wait.Stop(); frame.Continue = false; }; wait.Start(); Dispatcher.PushFrame(frame);
            Assert(!pet.QuotaVisible && pet.CurrentAnimation == "idle", "The actual WPF timer restores idle after five seconds");
        }
        private static async Task QuotaLive() {
            string workspace = Path.Combine(root, "quota-workspace"); Directory.CreateDirectory(workspace);
            using (var controller = new Controller(Path.Combine(root, "quota-session.json"), workspace, delegate(Action action) { action(); })) {
                await controller.ConnectAsync(); Assert(controller.AccountLabel.StartsWith("ChatGPT"), "Quota lookup uses saved ChatGPT authentication");
                string thread = controller.ThreadId; int messages = controller.Messages.Count; PetState state = controller.Status.State;
                var snapshot = await controller.ReadQuotaAsync();
                Assert(snapshot.FiveHours != null && snapshot.Week != null, "The live account returns both a five-hour and weekly quota window");
                Assert(snapshot.FiveHours.RemainingPercent.HasValue && snapshot.Week.RemainingPercent.HasValue && snapshot.FiveHours.RemainingPercent >= 0 && snapshot.FiveHours.RemainingPercent <= 100 && snapshot.Week.RemainingPercent >= 0 && snapshot.Week.RemainingPercent <= 100, "Both live remaining percentages are valid");
                Assert(controller.ThreadId == thread && controller.Messages.Count == messages && controller.Status.State == state && !controller.Busy && controller.Client.Connected, "Quota lookup creates no conversation, messages or inference task and leaves the connection usable");
                J.Save(Path.Combine(root, "verified-quota.json"), P("executable", controller.Client.Executable, "account", "ChatGPT", "limitId", snapshot.LimitId, "queriedAtUtc", DateTime.UtcNow.ToString("o"), "fiveHoursRemainingPercent", snapshot.FiveHours.RemainingPercent, "weeklyRemainingPercent", snapshot.Week.RemainingPercent, "fiveHoursDurationMins", snapshot.FiveHours.Minutes, "weeklyDurationMins", snapshot.Week.Minutes, "inferenceTaskStarted", false));
                Console.WriteLine("Remaining: 5 hours " + snapshot.FiveHours.Display + ", weekly " + snapshot.Week.Display);
            }
        }
        private static void Pump(int milliseconds) {
            var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
        }
        private static void QuotaFading() {
            var bubble = new QuotaBubbleWindow { Left = 600, Top = 350 };
            bubble.Reveal(); Pump(80);
            Assert(bubble.IsVisible && bubble.Opacity > 0 && bubble.Opacity < 1, "The hand-drawn quota bubble fades in through intermediate opacity");
            Pump(140); Assert(bubble.Opacity == 1, "Fade-in finishes at full opacity");
            bubble.FadeAway(); Pump(80);
            Assert(bubble.IsVisible && bubble.Opacity > 0 && bubble.Opacity < 1, "Dismissal fades out through intermediate opacity");
            bubble.Reveal(); Pump(240);
            Assert(bubble.IsVisible && bubble.Opacity == 1, "An old fade-out completion cannot hide a newly opened quota bubble");
            bubble.FadeAway(); Pump(240);
            Assert(!bubble.IsVisible && bubble.Opacity == 1, "Fade-out hides the window and resets opacity for the next query");
            bubble.Close();
        }
        private static async Task FileToolsLive() {
            string workspace = Path.Combine(root, "文件工具 空间-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(workspace);
            string marker = "TETO_READ_" + Guid.NewGuid().ToString("N"), second = "TETO_RESUME_" + Guid.NewGuid().ToString("N");
            string fixture = Path.Combine(workspace, "项目说明.md"), resultPath = Path.Combine(workspace, "result.txt");
            File.WriteAllText(fixture, marker, new System.Text.UTF8Encoding(false));
            string outside = Path.Combine(root, "outside-workspace-" + Guid.NewGuid().ToString("N") + ".txt");
            string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            var tools = new List<object>(); int approvals = 0;
            using (var controller = new Controller(Path.Combine(root, "file-tools-session.json"), workspace, delegate(Action action) { action(); })) {
                await controller.ConnectAsync();
                Assert(controller.AccountLabel.StartsWith("ChatGPT"), "File tools use the existing ChatGPT login without an API key");
                Console.WriteLine("Executable: " + controller.Client.Executable);
                Assert(controller.SetModel("gpt-6.1-sol") && controller.SetEffort("low"), "The user's GPT-6.1-Sol/low selection is supported");
                await controller.Client.VerifyWorkspaceToolsAsync(workspace);
                Assert(true, "The real command preflight succeeds without a model turn");
                object read = await controller.Client.RequestAsync("command/exec", J.O("command", new[] { shell, "-NoProfile", "-NonInteractive", "-Command", "[Console]::Write([IO.File]::ReadAllText('" + fixture.Replace("'", "''") + "'))" }, "cwd", workspace, "sandboxPolicy", J.O("type", "workspaceWrite", "writableRoots", new[] { workspace }, "networkAccess", false), "timeoutMs", 15000), 30000);
                Assert(J.I(read, "exitCode", -1) == 0 && J.S(read, "stdout") == marker, "The native command tool reads an opaque UTF-8 file from a Chinese path with spaces");
                object denied = await controller.Client.RequestAsync("command/exec", J.O("command", new[] { shell, "-NoProfile", "-NonInteractive", "-Command", "try { [IO.File]::WriteAllText('" + outside.Replace("'", "''") + "','UNEXPECTED'); exit 0 } catch { [Console]::Write('WRITE_DENIED'); exit 14 }" }, "cwd", workspace, "sandboxPolicy", J.O("type", "workspaceWrite", "writableRoots", new[] { workspace }, "networkAccess", false), "timeoutMs", 15000), 30000);
                Assert(J.I(denied, "exitCode", -1) == 14 && J.S(denied, "stdout") == "WRITE_DENIED" && !File.Exists(outside), "Sandbox boundaries still prevent writes outside the chosen workspace");
                controller.ApprovalRequested += delegate(RpcEvent request) {
                    approvals++;
                    // These fixture operations must work within the sandbox;
                    // do not approve a request that would bypass this test.
                    controller.Answer(request, request.Method == "item/permissions/requestApproval" ? J.O("permissions", J.O(), "scope", "turn") : J.O("decision", "decline"));
                };
                Action attach = delegate {
                    controller.Client.Event += delegate(object sender, RpcEvent e) {
                        if (e.Method == "item/completed") {
                            object item = J.Get(e.Params, "item"); string type = J.S(item, "type");
                            if (type == "commandExecution" || type == "fileChange") lock (tools) tools.Add(item);
                        }
                    };
                };
                attach();
                Assert(await controller.SendAsync("这是本地文件工具验证。只在当前工作文件夹操作，不请求提权、不访问网络、不读取其他文件夹。请实际读取 项目说明.md，用文件工具把读到的内容加上 |TETO_OK 写入 result.txt，UTF-8，无换行。读取前不要猜内容，最后只回复 FILE_TOOLS_OK。"), "The controller submits a real file task after its sandbox preflight");
                await Wait(controller, 180000);
                Assert(controller.Status.State == PetState.Completed && File.Exists(resultPath) && File.ReadAllText(resultPath).Trim() == marker + "|TETO_OK", "A real model turn reads the unknown file content and creates the correct output file");
                Assert(controller.Messages.Exists(m => m.Role == "assistant" && m.Text.Contains("FILE_TOOLS_OK")), "The file task's streamed reply reaches the pet conversation");
                Assert(approvals == 0, "Ordinary workspace file operations need no sandbox bypass or approval");
                string threadId = controller.ThreadId;
                controller.Client.Dispose(); File.WriteAllText(fixture, second, new System.Text.UTF8Encoding(false));
                await controller.ConnectAsync(); attach();
                Assert(await controller.SendAsync("这是恢复会话后的验证。项目说明.md 已由测试器更改，请重新用本地工具读该文件，把最新内容加上 |TETO_OK 写入 result.txt。只操作当前文件夹，不请求提权、不访问网络。最后只回复 RESUME_FILE_TOOLS_OK。"), "A reconnected controller submits a second real file task");
                await Wait(controller, 180000);
                Assert(controller.ThreadId == threadId && controller.Status.State == PetState.Completed && File.ReadAllText(resultPath).Trim() == second + "|TETO_OK", "Resuming the same conversation reads fresh disk content and updates the output file");
                Assert(approvals == 0 && tools.Count > 0, "Native file/command execution events are observed without bypassing permissions");
                J.Save(Path.Combine(root, "file-tools-events.json"), J.O("threadId", threadId, "workspace", workspace, "model", controller.Model, "effort", controller.Effort, "auth", "ChatGPT", "apiKeyUsed", false, "approvalRequests", approvals, "tools", tools));
            }
        }
        private static async Task Live() {
            string workspace = Path.Combine(root, "live-workspace-" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(workspace);
            const string marker = "TETO_FIXTURE_20261010";
            File.WriteAllText(Path.Combine(workspace, "fixture.txt"), marker, new System.Text.UTF8Encoding(false));
            using (var controller = new Controller(Path.Combine(root, "live-session.json"), workspace, delegate(Action action) { action(); })) {
                controller.NewChat(workspace);
                var states = new List<string>(); var events = new HashSet<string>(); int approvalCount = 0;
                controller.Changed += delegate { lock (states) if (states.Count == 0 || states[states.Count - 1] != controller.Status.State.ToString()) states.Add(controller.Status.State.ToString()); };
                controller.ApprovalRequested += delegate(RpcEvent request) {
                    approvalCount++; string command = J.S(request.Params, "command"), cwd = J.S(request.Params, "cwd");
                    bool testCommand = request.Method == "item/commandExecution/requestApproval" && String.Equals(cwd.TrimEnd('\\'), workspace.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) && (command.Contains("fixture.txt") || command.Contains("result.txt") || command.Contains("Start-Sleep")) && !command.Contains("Remove-Item") && !command.Contains("Invoke-WebRequest");
                    Console.WriteLine("Test approval: " + request.Method + " scoped=" + testCommand);
                    if (!testCommand) Console.WriteLine(J.Serialize(request.Params));
                    controller.Answer(request, J.O("decision", testCommand ? "accept" : "decline"));
                };
                controller.Activity += delegate(string line) { lock (events) events.Add(line.Split(new[] { " → " }, StringSplitOptions.None)[0]); };
                await controller.ConnectAsync(); Assert(controller.Client.Connected && controller.AccountLabel.StartsWith("ChatGPT"), "Uses saved ChatGPT authentication without API keys");
                await controller.SendAsync("这是本地桌宠集成测试。请记住会话口令 SESSION_TETO_20261010。读取当前文件夹中的 fixture.txt，然后用本地文件工具创建 result.txt，内容严格为读取到的标记加上 |TETO_OK，不要加换行。最后只回复 TETO_OK。请直接执行，不要只给操作步骤。");
                await Wait(controller, 180000);
                Assert(controller.Status.State == PetState.Completed, "Actual file task completed successfully");
                string file = Path.Combine(workspace, "result.txt"); Assert(File.Exists(file) && File.ReadAllText(file).Trim() == marker + "|TETO_OK", "Codex actually read and wrote the requested local file");
                Assert(controller.Messages.Exists(e => e.Role == "assistant" && e.Text.Contains("TETO_OK")), "Streamed reply reaches the conversation");
                string threadId = controller.ThreadId;
                controller.Client.Dispose();
                await controller.SendAsync("不要使用工具。只回复我上条消息给出的会话口令。"); await Wait(controller, 120000);
                Assert(controller.ThreadId == threadId && controller.Status.State == PetState.Completed, "Conversation resumes after restarting the private Codex process");
                Assert(controller.Messages[controller.Messages.Count - 1].Text.Contains("SESSION_TETO_20261010"), "Resumed conversation retains its context");
                bool cancelIssued = false;
                controller.Activity += delegate(string line) {
                    if (!cancelIssued && controller.Status.State == PetState.Working) { cancelIssued = true; Console.WriteLine("Interrupting actual running task"); controller.StopAsync(); }
                };
                await controller.SendAsync("这是停止功能测试。请用 PowerShell 的 Start-Sleep -Seconds 20 等待20秒，然后只回复 CANCEL_SHOULD_NOT_COMPLETE。现在执行。");
                await Wait(controller, 90000);
                Assert(cancelIssued && controller.Status.State == PetState.Interrupted, "Interrupting a running tool produces an interrupted state");
                Assert(!controller.Messages.Exists(e => e.Role == "assistant" && e.Text.Trim() == "CANCEL_SHOULD_NOT_COMPLETE"), "Cancellation is not displayed as a completed task");
                J.Save(Path.Combine(root, "live-events.json"), J.O("states", states, "methods", events, "approvalRequests", approvalCount, "threadId", threadId, "auth", "ChatGPT", "apiKeyUsed", false));
                Assert(states.Contains("Thinking") || events.Contains("item/reasoning/summaryTextDelta"), "Actual reasoning event was observed");
            }
        }
        private static async Task ModelRegression() {
            string workspace = Path.Combine(root, "model-workspace"); Directory.CreateDirectory(workspace);
            string store = Path.Combine(root, "legacy-model-session.json");
            using (var controller = new Controller(store, workspace, delegate(Action action) { action(); })) {
                await controller.ConnectAsync();
                Assert(controller.AccountLabel.StartsWith("ChatGPT"), "Regression uses ChatGPT authentication without API keys");
                Console.WriteLine("Executable: " + controller.Client.Executable);
                Console.WriteLine("Catalog: " + String.Join(", ", controller.Client.Models.ConvertAll(m => m.Id)));
                Console.WriteLine("Selected: " + controller.Model);
                // Seed a thread with the failing legacy configured model, but do
                // not run inference with it. Resuming must explicitly override it.
                object legacy = await controller.Client.RequestAsync("thread/start", J.O("cwd", workspace, "model", "gpt-6.1-sol", "sandbox", "workspace-write", "approvalPolicy", "on-request", "approvalsReviewer", "user"), 60000);
                controller.ThreadId = J.S(J.Get(legacy, "thread"), "id");
                string legacyId = controller.ThreadId;
                controller.Status.Begin(legacyId);
                await controller.Client.RequestAsync("turn/start", J.O("threadId", legacyId, "model", "gpt-6.1-sol", "input", new object[] { J.O("type", "text", "text", "不要使用任何工具。请只回复 LEGACY_SETUP。", "text_elements", new object[0]) }), 60000);
                await Wait(controller, 120000);
                controller.Model = "gpt-6.1-sol";
                await controller.SendAsync("不要使用任何工具。请只回复 TETO_MODEL_OK。"); await Wait(controller, 120000);
                Assert(controller.Status.State == PetState.Completed && controller.ThreadId == legacyId, "Legacy unsupported-model conversations resume without losing their thread");
                Assert(controller.Messages.Exists(e => e.Role == "assistant" && e.Text.Contains("TETO_MODEL_OK")), "The selected catalog model actually completes inference");
                string verifiedModel = controller.Model;
                controller.Client.Dispose();
                await controller.SendAsync("不要使用任何工具。请只回复上一条任务要求的英文口令。"); await Wait(controller, 120000);
                Assert(controller.Status.State == PetState.Completed && controller.Model == verifiedModel, "Reconnection keeps the working catalog model");
                Assert(controller.Messages[controller.Messages.Count - 1].Text.Contains("TETO_MODEL_OK"), "Model override retains conversation context");
                J.Save(Path.Combine(root, "verified-model.json"), J.O("model", verifiedModel, "catalog", controller.Client.Models.ConvertAll(m => m.Id), "auth", "ChatGPT", "apiKeyUsed", false));
            }
        }
        private static async Task Wait(Controller controller, int timeout) {
            var watch = Stopwatch.StartNew(); while (controller.Busy) { if (watch.ElapsedMilliseconds > timeout) { await controller.StopAsync(); throw new TimeoutException("Live test timeout"); } await Task.Delay(100); }
            Console.WriteLine("Final state: " + controller.Status.State);
            if (controller.Status.State == PetState.Failed) foreach (var e in controller.Messages) if (e.Role == "notice") Console.WriteLine(e.Text);
        }
    }
}
