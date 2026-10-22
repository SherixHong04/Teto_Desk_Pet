using System.Windows;

namespace TetoDesk {
    public static class PetInteractionRegion {
        // Covers the idle/jump body's fixed envelope, including raised arms.
        // The 192x208 artwork and its alpha silhouette never define hit testing.
        public static Rect AtScale(double scale) { return new Rect(35 * scale, 3 * scale, 120 * scale, 202 * scale); }
    }
    public static class PetDragFeedback {
        public static bool PreserveAnimation(bool reactionPlaying, bool quotaVisible) { return reactionPlaying || quotaVisible; }
        public static bool DismissQuotaOnRelease(bool queryAtPress, bool dragged, bool validRelease) { return queryAtPress && !dragged && validRelease; }
    }
}
