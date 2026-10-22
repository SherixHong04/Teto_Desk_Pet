using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace TetoDesk {
    // The callback belongs to one transition: reopening cancels a pending hide.
    internal sealed class PanelFade {
        internal const int RevealMs = 140, ConcealMs = 110;
        private int generation;
        internal AnimationClock Clock { get; private set; }
        internal bool Hiding { get; private set; }
        internal void Reveal(UIElement target, bool visible, Action show) {
            if (visible && !Hiding) return;
            Start(target, visible ? target.Opacity : 0, 1, RevealMs, false, null);
            if (!visible) show();
        }
        internal void Conceal(UIElement target, Action hide) {
            if (Hiding) return;
            Start(target, target.Opacity, 0, ConcealMs, true, hide);
        }
        private void Start(UIElement target, double from, double to, int milliseconds, bool hiding, Action completed) {
            int ticket = ++generation; Hiding = hiding;
            if (Clock != null) Clock.Controller.Stop();
            target.BeginAnimation(UIElement.OpacityProperty, null); target.Opacity = from;
            var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var clock = (AnimationClock)animation.CreateClock(true); Clock = clock;
            clock.Completed += delegate {
                if (ticket != generation) return;
                target.Opacity = to; target.ApplyAnimationClock(UIElement.OpacityProperty, null);
                Clock = null; Hiding = false;
                if (completed != null) completed();
            };
            target.ApplyAnimationClock(UIElement.OpacityProperty, clock);
        }
        internal void Cancel(UIElement target) {
            generation++; Hiding = false;
            if (Clock != null) Clock.Controller.Stop(); Clock = null;
            target.BeginAnimation(UIElement.OpacityProperty, null); target.Opacity = 1;
        }
    }
}
