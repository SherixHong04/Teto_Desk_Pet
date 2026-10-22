using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TetoDesk {
    public sealed class AnimationClip {
        public string Name;
        public BitmapSource[] Frames;
        public int[] Durations;
        public bool Loop = true;
        public int SpeechStartMs = -1, SpeechEndMs = -1;
        public bool AdvanceFrame(ref int frame, bool playOnce) {
            if (frame < 0 || frame >= Frames.Length) throw new ArgumentOutOfRangeException("frame");
            if (frame + 1 < Frames.Length) { frame++; return false; }
            if (Loop && !playOnce) { frame = 0; return false; }
            return true;
        }
    }
    public sealed class Sprites {
        public readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        public Sprites() {
            BitmapFrame atlas;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TetoDesk.teto.png")) atlas = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            atlas.Freeze();
            object config;
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("TetoDesk.animations.json"))) config = J.Parse(reader.ReadToEnd());
            foreach (object row in J.A(J.Get(config, "animations"))) {
                string name = J.S(row, "name"); int index = J.I(row, "row", 0), count = J.I(row, "frames", 0);
                var clip = new AnimationClip { Name = name, Frames = new BitmapSource[count], Durations = new int[count] };
                object[] durations = J.A(J.Get(row, "durations"));
                for (int i = 0; i < count; i++) {
                    var image = new CroppedBitmap(atlas, new Int32Rect(i * 192, index * 208, 192, 208)); image.Freeze();
                    clip.Frames[i] = image; clip.Durations[i] = Convert.ToInt32(durations[i]);
                }
                Clips[name] = clip;
            }
            LoadQuotaClip();
            LoadIdleBaguetteClip();
            LoadDoubleClickClip(DoubleClickEasterEgg.Animation);
            LoadDoubleClickClip(DoubleClickEasterEgg.DizzyAnimation);
        }
        private void LoadDoubleClickClip(string name) {
            var assembly = Assembly.GetExecutingAssembly();
            using (var sheet = assembly.GetManifestResourceStream("TetoDesk." + name + ".png"))
            using (var manifest = assembly.GetManifestResourceStream("TetoDesk." + name + ".json")) {
                if (sheet == null && manifest == null) return; // Only approved art enables this reaction.
                if (sheet == null || manifest == null) throw new InvalidDataException("Incomplete double-click animation resources.");
                Clips[name] = ReadDoubleClickClip(sheet, manifest, name);
            }
        }
        internal static AnimationClip ReadDoubleClickClip(Stream sheet, Stream manifest) {
            return ReadDoubleClickClip(sheet, manifest, DoubleClickEasterEgg.Animation);
        }
        internal static AnimationClip ReadDoubleClickClip(Stream sheet, Stream manifest, string name) {
            if (!DoubleClickEasterEgg.IsReaction(name)) throw new ArgumentException("Unknown double-click reaction.", "name");
            object config; using (var reader = new StreamReader(manifest)) config = J.Parse(reader.ReadToEnd());
            object[] rows = J.A(J.Get(config, "animations"));
            if (rows.Length != 1 || J.I(config, "width", 0) != 192 || J.I(config, "height", 0) != 208) throw new InvalidDataException("Invalid double-click animation layout.");
            object row = rows[0]; int count = J.I(row, "frames", 0), columns = J.I(config, "columns", count); object[] durations = J.A(J.Get(row, "durations"));
            if (J.S(row, "name") != name || J.I(row, "row", -1) != 0 || J.B(row, "loop") || count < 2 || count > 240 || columns < 1 || columns > count || durations.Length != count) throw new InvalidDataException("Invalid double-click animation manifest.");
            var atlas = BitmapFrame.Create(sheet, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); atlas.Freeze();
            if (atlas.PixelWidth != columns * 192 || atlas.PixelHeight != ((count + columns - 1) / columns) * 208) throw new InvalidDataException("Invalid double-click animation dimensions.");
            var clip = new AnimationClip { Name = name, Frames = new BitmapSource[count], Durations = new int[count], Loop = false };
            for (int i = 0; i < count; i++) {
                int duration = Convert.ToInt32(durations[i]);
                if (duration < 20 || duration > 5000) throw new InvalidDataException("Invalid double-click animation frame timing.");
                var image = new CroppedBitmap(atlas, new Int32Rect((i % columns) * 192, (i / columns) * 208, 192, 208)); image.Freeze();
                clip.Frames[i] = image; clip.Durations[i] = duration;
            }
            int total = IdleEasterEggSchedule.Duration(clip);
            if (total < 1000 || total > 30000) throw new InvalidDataException("Invalid double-click animation duration.");
            if (name == DoubleClickEasterEgg.Animation) {
                clip.SpeechStartMs = J.I(row, "speechStartMs", 0); clip.SpeechEndMs = J.I(row, "speechEndMs", total);
                if (clip.SpeechStartMs < 0 || clip.SpeechStartMs >= clip.SpeechEndMs || clip.SpeechEndMs > total) throw new InvalidDataException("Invalid double-click speech duration.");
            } else if (J.Get(row, "speechStartMs") != null || J.Get(row, "speechEndMs") != null) throw new InvalidDataException("The dizzy reaction must not inherit angry speech.");
            return clip;
        }
        private void LoadIdleBaguetteClip() {
            var assembly = Assembly.GetExecutingAssembly();
            using (var sheet = assembly.GetManifestResourceStream("TetoDesk.idle-baguette.png"))
            using (var manifest = assembly.GetManifestResourceStream("TetoDesk.idle-baguette.json")) {
                if (sheet == null && manifest == null) return; // Artwork pending approval: no placeholder runs.
                if (sheet == null || manifest == null) throw new InvalidDataException("Incomplete idle baguette resources.");
                object config; using (var reader = new StreamReader(manifest)) config = J.Parse(reader.ReadToEnd());
                object[] rows = J.A(J.Get(config, "animations"));
                if (rows.Length != 1 || J.I(config, "width", 0) != 192 || J.I(config, "height", 0) != 208) throw new InvalidDataException("Invalid idle baguette layout.");
                object row = rows[0]; int count = J.I(row, "frames", 0), columns = J.I(config, "columns", count); object[] durations = J.A(J.Get(row, "durations"));
                if (J.S(row, "name") != "idle-baguette" || J.I(row, "row", -1) != 0 || J.B(row, "loop") || count < 2 || count > 240 || columns < 1 || columns > count || durations.Length != count) throw new InvalidDataException("Invalid idle baguette manifest.");
                var atlas = BitmapFrame.Create(sheet, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); atlas.Freeze();
                if (atlas.PixelWidth != columns * 192 || atlas.PixelHeight != ((count + columns - 1) / columns) * 208) throw new InvalidDataException("Invalid idle baguette dimensions.");
                var clip = new AnimationClip { Name = "idle-baguette", Frames = new BitmapSource[count], Durations = new int[count], Loop = false };
                for (int i = 0; i < count; i++) {
                    int duration = Convert.ToInt32(durations[i]);
                    if (duration < 20 || duration > 5000) throw new InvalidDataException("Invalid idle baguette frame timing.");
                    var image = new CroppedBitmap(atlas, new Int32Rect((i % columns) * 192, (i / columns) * 208, 192, 208)); image.Freeze();
                    clip.Frames[i] = image; clip.Durations[i] = duration;
                }
                int total = IdleEasterEggSchedule.Duration(clip);
                if (total < 1000 || total > 119000) throw new InvalidDataException("Idle baguette duration must permit a 30–120 second start interval.");
                Clips[clip.Name] = clip;
            }
        }
        private void LoadQuotaClip() {
            var assembly = Assembly.GetExecutingAssembly();
            using (var sheet = assembly.GetManifestResourceStream("TetoDesk.quota-thinking.png"))
            using (var manifest = assembly.GetManifestResourceStream("TetoDesk.quota-thinking.json")) {
                if (sheet == null && manifest == null) return;
                if (sheet == null || manifest == null) throw new InvalidDataException("Incomplete quota animation resources.");
                object config; using (var reader = new StreamReader(manifest)) config = J.Parse(reader.ReadToEnd());
                object[] rows = J.A(J.Get(config, "animations"));
                if (rows.Length != 1 || J.I(config, "width", 0) != 192 || J.I(config, "height", 0) != 208) throw new InvalidDataException("Invalid quota animation layout.");
                object row = rows[0]; int count = J.I(row, "frames", 0); object[] durations = J.A(J.Get(row, "durations"));
                if (J.S(row, "name") != "quota-thinking" || J.I(row, "row", -1) != 0 || count < 1 || count > 60 || durations.Length != count) throw new InvalidDataException("Invalid quota animation manifest.");
                var atlas = BitmapFrame.Create(sheet, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); atlas.Freeze();
                if (atlas.PixelWidth != count * 192 || atlas.PixelHeight != 208) throw new InvalidDataException("Invalid quota animation dimensions.");
                var clip = new AnimationClip { Name = "quota-thinking", Frames = new BitmapSource[count], Durations = new int[count] };
                for (int i = 0; i < count; i++) {
                    int duration = Convert.ToInt32(durations[i]);
                    if (duration < 20 || duration > 5000) throw new InvalidDataException("Invalid quota animation timing.");
                    var frame = new CroppedBitmap(atlas, new Int32Rect(i * 192, 0, 192, 208)); frame.Freeze();
                    clip.Frames[i] = frame; clip.Durations[i] = duration;
                }
                Clips[clip.Name] = clip;
            }
        }
        public static System.Drawing.Icon Icon() {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TetoDesk.teto.ico")) return (System.Drawing.Icon)new System.Drawing.Icon(stream).Clone();
        }
    }
}
