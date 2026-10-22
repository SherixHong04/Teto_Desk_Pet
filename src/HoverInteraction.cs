namespace TetoDesk {
    public static class HoverInteraction {
        public static bool ShouldJump(bool hovered, PetState state, bool paused, bool pointerBusy, bool menuOpen, bool quotaVisible, bool manualGesture, bool alreadyPlayed = false) {
            bool resting = state == PetState.Idle || state == PetState.Completed || state == PetState.Interrupted || state == PetState.Failed;
            return hovered && !alreadyPlayed && resting && !paused && !pointerBusy && !menuOpen && !quotaVisible && !manualGesture;
        }
    }
}
