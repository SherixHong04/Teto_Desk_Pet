using System;
using System.Windows;

namespace TetoDesk {
    public enum PetClickResult { None, PendingSingle, Single, Double }

    // Classifies domain press/release data; it never reads or sends mouse input.
    public sealed class PetClickInteraction {
        private readonly int intervalMs;
        private readonly Size range;
        private Point firstDown, currentDown;
        private long firstAtMs, currentAtMs;
        private bool pressing, secondPress;
        public bool PendingSingle { get; private set; }
        public long SingleDueAtMs { get; private set; }
        public PetClickInteraction(int doubleClickTimeMs, Size doubleClickRange) {
            if (doubleClickTimeMs < 1 || doubleClickRange.Width < 1 || doubleClickRange.Height < 1) throw new ArgumentOutOfRangeException("doubleClickTimeMs");
            intervalMs = doubleClickTimeMs; range = doubleClickRange;
        }
        // A new press outside the double-click range flushes the first single.
        public bool Begin(Point point, long nowMs) {
            secondPress = PendingSingle && nowMs >= firstAtMs && nowMs - firstAtMs <= intervalMs
                && Math.Abs(point.X - firstDown.X) <= range.Width / 2
                && Math.Abs(point.Y - firstDown.Y) <= range.Height / 2;
            bool flush = PendingSingle && !secondPress;
            PendingSingle = false; pressing = true; currentDown = point; currentAtMs = nowMs;
            return flush;
        }
        public PetClickResult End(long nowMs, bool validClick) {
            if (!pressing) return PetClickResult.None;
            pressing = false;
            if (!validClick) { Cancel(); return PetClickResult.None; }
            if (secondPress) { Cancel(); return PetClickResult.Double; }
            firstDown = currentDown; firstAtMs = currentAtMs;
            SingleDueAtMs = Math.Max(nowMs, firstAtMs + intervalMs);
            PendingSingle = true; return PetClickResult.PendingSingle;
        }
        public PetClickResult Poll(long nowMs) {
            if (!PendingSingle || nowMs < SingleDueAtMs) return PetClickResult.None;
            Cancel(); return PetClickResult.Single;
        }
        public void Cancel() { PendingSingle = pressing = secondPress = false; }
    }
    public static class DoubleClickEasterEgg {
        public const string Animation = "double-click-angry";
        public const string DizzyAnimation = "double-click-dizzy";
        public const string Phrase = "君はじつに馬鹿だな!";
        public static bool IsReaction(string name) { return name == Animation || name == DizzyAnimation; }
        public static bool Eligible(PetState state, bool paused, bool pointerBusy, bool menuOpen, bool quotaVisible, bool taskCue) {
            return HoverInteraction.ShouldJump(true, state, paused, pointerBusy, menuOpen, quotaVisible, taskCue);
        }
    }
}
