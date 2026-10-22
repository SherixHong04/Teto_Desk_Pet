using System;
using System.IO;

namespace TetoDesk {
    // One draw per eligible double-click. Preview, dragging and missing art
    // never advance the streak. Percentages are integer percentage points.
    public sealed class DoubleClickChance {
        private readonly Func<int, int, int> random;
        public int Percent { get; private set; }
        public Action Changed;
        public DoubleClickChance(Func<int, int, int> draw) : this(draw, 5) { }
        private DoubleClickChance(Func<int, int, int> draw, int savedPercent) {
            if (draw == null) throw new ArgumentNullException("draw");
            random = draw; Percent = ValidPercent(savedPercent) ? savedPercent : 5;
        }
        private static bool ValidPercent(int percent) { return percent == 100 || (percent >= 5 && percent <= 95 && (percent - 5) % 10 == 0); }
        public bool TryChoose(bool eligible, bool dizzyReady, out string animation) {
            animation = "";
            if (!eligible) return false;
            animation = DoubleClickEasterEgg.Animation;
            if (!dizzyReady) return true;
            int roll = Percent == 100 ? 0 : random(0, 100);
            if (roll < 0 || roll >= 100) throw new InvalidOperationException("A reaction probability draw must be in [0,100).");
            bool dizzy = roll < Percent;
            Percent = dizzy ? 5 : Math.Min(100, Percent + 10);
            if (dizzy) animation = DoubleClickEasterEgg.DizzyAnimation;
            if (Changed != null) Changed();
            return true;
        }
        public static DoubleClickChance Load(string path, Func<int, int, int> draw) {
            int percent = 5;
            try { if (File.Exists(path)) percent = J.I(J.Parse(File.ReadAllText(path)), "dizzyChancePercent", 5); } catch { }
            return new DoubleClickChance(draw, percent);
        }
        public void Save(string path) { J.Save(path, J.O("dizzyChancePercent", Percent)); }
    }
}
