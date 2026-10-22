using System;

namespace TetoDesk {
    // A monotonic clock is supplied by the window. Pure scheduling keeps this
    // feature independent of wall-clock changes and mouse automation in tests.
    public sealed class IdleEasterEggSchedule {
        public const int MinimumDelayMs = 30000, MaximumDelayMs = 120000;
        private readonly Func<int, int, int> next;
        public long? NextAtMs { get; private set; }
        public int LastDelayMs { get; private set; }
        public bool Playing { get; private set; }
        public IdleEasterEggSchedule(Func<int, int, int> random) { next = random; }
        public bool Poll(long now, bool eligible, int animationDurationMs) {
            if (!eligible) { Reset(); return false; }
            if (Playing) return false;
            if (!NextAtMs.HasValue) { Schedule(now, MinimumDelayMs); return false; }
            if (now < NextAtMs.Value) return false;
            // Schedule the next start from this actual start, rather than from
            // completion, so the animation itself does not add to the interval.
            Schedule(now, Math.Max(MinimumDelayMs, animationDurationMs + 1000));
            Playing = true; return true;
        }
        private void Schedule(long now, int minimum) {
            if (minimum > MaximumDelayMs) throw new InvalidOperationException("彩蛋动画过长，无法保持两分钟内的触发间隔。");
            int delay = next(minimum, MaximumDelayMs + 1);
            if (delay < minimum || delay > MaximumDelayMs) throw new InvalidOperationException("Invalid idle animation interval.");
            LastDelayMs = delay; NextAtMs = now + delay;
        }
        public void Complete(long now) {
            if (!Playing) return;
            Playing = false;
            // A suspended application or long explicit pause does not cause a
            // backlog of animations. Resume with one new full waiting period.
            if (!NextAtMs.HasValue || NextAtMs.Value <= now) Schedule(now, MinimumDelayMs);
        }
        public void Reset() { NextAtMs = null; LastDelayMs = 0; Playing = false; }
        public static bool Eligible(PetState state, bool visible, bool paused, bool interaction, bool quota, bool gesture, bool idleOrPlaying) {
            bool resting = state == PetState.Idle || state == PetState.Completed || state == PetState.Interrupted || state == PetState.Failed;
            return resting && visible && !paused && !interaction && !quota && !gesture && idleOrPlaying;
        }
        public static int Duration(AnimationClip clip) {
            int total = 0; foreach (int duration in clip.Durations) total = checked(total + duration); return total;
        }
    }
}
