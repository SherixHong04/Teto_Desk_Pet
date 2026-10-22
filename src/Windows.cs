using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Automation;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace TetoDesk {
    public static class Theme {
        public static Brush Ink = Color("#352D38"), Muted = Color("#857985"), Rose = Color("#B74367"), Line = Color("#EDDFE5"), Soft = Color("#FCF7F9");
        public static Brush Color(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        public static TextBlock Text(string text, double size, Brush color) { return new TextBlock { Text = text, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap }; }
        public static Button Button(string text, Action action, bool primary) {
            var button = new Button { Content = text, Padding = new Thickness(13, 8, 13, 8), FontSize = 12, Cursor = Cursors.Hand, Foreground = primary ? Brushes.White : Ink, Background = primary ? Rose : Brushes.White, BorderBrush = primary ? Rose : Line, BorderThickness = new Thickness(1), MinHeight = 32 };
            button.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.85'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='BorderBrush' Value='#B74367'/><Setter Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            button.Click += delegate { action(); }; AutomationProperties.SetName(button, text); return button;
        }
        public static Button Picker(string text, Action action) {
            var button = Button(text, action, false); button.Padding = new Thickness(8, 5, 8, 5);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Stretch' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='#FCF7F9'/><Setter Property='BorderBrush' Value='#D6B6C4'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='BorderBrush' Value='#B74367'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            return button;
        }
        public static Button CollapseButton(Action action) {
            var button = Button("收起", action, false);
            // Set named visual elements: setters on Button lose to its local colors.
            button.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='Surface' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' Padding='{TemplateBinding Padding}'><ContentPresenter x:Name='Label' TextElement.Foreground='{TemplateBinding Foreground}' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='#E81123'/><Setter TargetName='Surface' Property='BorderBrush' Value='#E81123'/><Setter TargetName='Label' Property='TextElement.Foreground' Value='White'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Background' Value='#C42B1C'/><Setter TargetName='Surface' Property='BorderBrush' Value='#C42B1C'/><Setter TargetName='Label' Property='TextElement.Foreground' Value='White'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            return button;
        }
        public static void Snapshot(FrameworkElement element, string path) {
            element.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
        }
    }
    public sealed class PetWindow : Window {
        private readonly Sprites sprites;
        private readonly Image figure;
        private readonly Grid characterSurface;
        private readonly TextBlock statusText;
        private readonly DispatcherTimer animationTimer;
        private readonly DispatcherTimer idleTimer;
        private readonly Stopwatch idleClock = Stopwatch.StartNew();
        private readonly IdleEasterEggSchedule idleSchedule;
        private AnimationClip clip;
        private int frame;
        private PetState state = PetState.Idle;
        private DateTime gestureEnd;
        private string gesture = "";
        private bool paused, dragging, pointerDown, characterHovered, hoverJumping, hoverConsumed;
        private bool menuOpen, taskCueOnce, quotaPress;
        private string pendingReactionPreview = "";
        private int reactionPreviewTicket;
        private UIElement dragSurface;
        private Point startWindow;
        private readonly DragMotion motion = new DragMotion();
        private readonly QuotaInteraction quota = new QuotaInteraction();
        private QuotaBubbleWindow quotaBubble;
        private DispatcherTimer quotaTimer, quotaBubbleTimer;
        internal const int QueryBubbleDelayMs = 200;
        private readonly PetClickInteraction clicks = new PetClickInteraction(Forms.SystemInformation.DoubleClickTime,
            new Size(Forms.SystemInformation.DoubleClickSize.Width, Forms.SystemInformation.DoubleClickSize.Height));
        private readonly DispatcherTimer clickTimer, speechTimer;
        private readonly Stopwatch reactionFrameClock = new Stopwatch();
        private QuotaBubbleWindow reactionBubble;
        private bool reactionPlaying, reactionSpeechActive;
        private string reactionAnimation = "";
        private readonly DoubleClickChance reactionChance;
        private int reactionElapsedMs;
        private bool closed;
        private HwndSource source;
        public Action OpenChat, Exit, NewChat, HidePet, QueryQuota;
        public string QueryMenuText = "查询剩余额度";
        public Action PositionChanged;
        public double PetScale = 1.0;
        public PetState CurrentState { get { return state; } }
        public string CurrentAnimation { get { return clip == null ? "" : clip.Name; } }
        internal Button ChatBubble { get; private set; }
        internal Border CharacterHitArea { get; private set; }
        internal bool QuotaVisible { get { return quota.Visible; } }
        internal bool QuotaBubbleRevealed { get { return quota.BubbleRevealed; } }
        internal QuotaBubbleWindow QuotaBubble { get { return quotaBubble; } }
        internal bool IdleEasterEggReady { get { return sprites.Clips.ContainsKey("idle-baguette"); } }
        internal bool IdleEasterEggPlaying { get { return idleSchedule.Playing; } }
        internal bool DoubleClickEasterEggReady { get { return sprites.Clips.ContainsKey(DoubleClickEasterEgg.Animation); } }
        internal bool DizzyEasterEggReady { get { return sprites.Clips.ContainsKey(DoubleClickEasterEgg.DizzyAnimation); } }
        internal int DizzyChancePercent { get { return reactionChance.Percent; } }
        internal bool DoubleClickEasterEggPlaying { get { return reactionPlaying; } }
        internal bool ReactionSpeechActive { get { return reactionSpeechActive; } }
        internal QuotaBubbleWindow ReactionBubble { get { return reactionBubble; } }
        private bool PointerBusy { get { return pointerDown || dragging || dragSurface != null || IsMouseCaptureWithin; } }
        private bool PreserveDragAnimation { get { return PetDragFeedback.PreserveAnimation(reactionPlaying, QuotaVisible); } }
        private bool ShouldHoverJump { get { return HoverInteraction.ShouldJump(characterHovered, state, paused, PointerBusy, menuOpen, QuotaVisible, gesture.Length > 0 || taskCueOnce || reactionPlaying, hoverConsumed && !hoverJumping); } }
        public PetWindow(Sprites assets) : this(assets, new DoubleClickChance(new Random().Next)) { }
        internal PetWindow(Sprites assets, DoubleClickChance chance) {
            if (chance == null) throw new ArgumentNullException("chance");
            sprites = assets; reactionChance = chance;
            var random = new Random(); idleSchedule = new IdleEasterEggSchedule(random.Next);
            Title = "Teto Desk · 桌宠"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            var panel = new StackPanel();
            characterSurface = new Grid { Width = 192, Height = 208 };
            // Alpha 1 keeps the layered HWND hit-testable over transparent
            // pixels inside this fixed rectangle, without a visible panel.
            CharacterHitArea = new Border { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand };
            characterSurface.Children.Add(CharacterHitArea);
            figure = new Image { Width = 192, Height = 208, Stretch = Stretch.Fill, IsHitTestVisible = false };
            characterSurface.Children.Add(figure);
            ToolTipService.SetIsEnabled(figure, false);
            AutomationProperties.SetName(figure, "Teto 桌宠");
            RenderOptions.SetBitmapScalingMode(figure, BitmapScalingMode.HighQuality);
            CharacterHitArea.MouseLeftButtonDown += Down; CharacterHitArea.MouseMove += Move; CharacterHitArea.MouseLeftButtonUp += Up; CharacterHitArea.LostMouseCapture += LostCapture;
            CharacterHitArea.MouseEnter += delegate { SetCharacterHovered(true); };
            CharacterHitArea.MouseLeave += delegate { SetCharacterHovered(false); };
            statusText = Theme.Text("待机", 11, Theme.Color("#D4D1D7"));
            statusText.TextWrapping = TextWrapping.NoWrap; statusText.TextTrimming = TextTrimming.CharacterEllipsis;
            var bubbleContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bubbleContent.Children.Add(ComposeIcon()); statusText.Margin = new Thickness(9, 0, 0, 0); statusText.VerticalAlignment = VerticalAlignment.Center; bubbleContent.Children.Add(statusText);
            ChatBubble = new Button { Content = bubbleContent, Height = 40, MinWidth = 86, Padding = new Thickness(13, 0, 13, 0), Margin = new Thickness(0, 4, 0, 2), HorizontalAlignment = HorizontalAlignment.Center, Cursor = Cursors.Hand, Background = Theme.Color("#191919"), BorderBrush = Theme.Color("#282828"), BorderThickness = new Thickness(1), ToolTip = "打开任务面板 · Ctrl+Alt+T" };
            ChatBubble.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='20' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='#272727'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter Property='Background' Value='#333333'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='BorderBrush' Value='#AFA8B1'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            AutomationProperties.SetName(ChatBubble, "打开任务面板");
            ChatBubble.Click += delegate { if (OpenChat != null) OpenChat(); };
            panel.Children.Add(characterSurface); panel.Children.Add(ChatBubble); Content = panel;
            PreviewMouseLeftButtonDown += delegate { CancelIdleEasterEgg(); };
            MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e) { BuildMenu().IsOpen = true; e.Handled = true; };
            LocationChanged += delegate { PositionQuotaBubble(); };
            IsVisibleChanged += delegate { if (!IsVisible) { CancelReactionPreview(); SetCharacterHovered(false); CancelCharacterClicks(); CancelDoubleClickReaction(); } CancelIdleEasterEgg(); if (!IsVisible) { DismissQuotaInfo(); Play(RestAnimation()); } else UpdateIdleEasterEgg(); };
            animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) }; animationTimer.Tick += Tick;
            idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; idleTimer.Tick += delegate { UpdateIdleEasterEgg(); };
            clickTimer = new DispatcherTimer(); clickTimer.Tick += delegate {
                if (clicks.Poll(idleClock.ElapsedMilliseconds) == PetClickResult.Single) { clickTimer.Stop(); PerformSingleClick(); }
            };
            speechTimer = new DispatcherTimer(); speechTimer.Tick += delegate { speechTimer.Stop(); UpdateReactionSpeech(); };
            SetScale(1); Play("idle"); animationTimer.Start();
            if (IdleEasterEggReady) idleTimer.Start();
            SourceInitialized += delegate {
                source = (HwndSource)PresentationSource.FromVisual(this); source.AddHook(WindowMessage);
                RegisterHotKey(source.Handle, 9042, 0x0002 | 0x0001 | 0x4000, 0x54); // Ctrl+Alt+T
            };
            Closed += delegate { closed = true; CancelCharacterClicks(); CancelDoubleClickReaction(); if (reactionBubble != null) reactionBubble.Close(); idleSchedule.Reset(); idleTimer.Stop(); idleClock.Stop(); quota.Dismiss(); StopQuotaTimer(); if (quotaBubble != null) quotaBubble.Close(); animationTimer.Stop(); if (source != null) UnregisterHotKey(source.Handle, 9042); };
        }
        private static UIElement ComposeIcon() {
            var canvas = new Canvas { Width = 24, Height = 24 };
            foreach (string geometry in new[] { "M12 3H5A2 2 0 0 0 3 5V19A2 2 0 0 0 5 21H19A2 2 0 0 0 21 19V12", "M18.375 2.625A2.121 2.121 0 0 1 21.375 5.625L12 15L8 16L9 12Z" }) {
                canvas.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse(geometry), Stroke = Brushes.White, StrokeThickness = 1.9, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
            }
            return new Viewbox { Width = 20, Height = 20, Child = canvas };
        }
        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled) {
            if (message == 0x0312 && wparam.ToInt32() == 9042) { Show(); if (OpenChat != null) OpenChat(); handled = true; }
            return IntPtr.Zero;
        }
        public void SetScale(double value) {
            PetScale = Math.Max(0.65, Math.Min(1.6, value)); figure.Width = 192 * PetScale; figure.Height = 208 * PetScale;
            characterSurface.Width = figure.Width; characterSurface.Height = figure.Height;
            Rect hit = PetInteractionRegion.AtScale(PetScale);
            CharacterHitArea.Width = hit.Width; CharacterHitArea.Height = hit.Height; CharacterHitArea.Margin = new Thickness(hit.Left, hit.Top, 0, 0);
            Width = 192 * PetScale; Height = 208 * PetScale + 46;
            ChatBubble.MaxWidth = Math.Min(150, Width - 8); statusText.MaxWidth = ChatBubble.MaxWidth - 57;
            PositionQuotaBubble();
            if (PositionChanged != null) PositionChanged();
        }
        public void SetState(PetState next) {
            if (state == next) return;
            CancelReactionPreview();
            CancelCharacterClicks(); CancelDoubleClickReaction(); CancelIdleEasterEgg(); state = next; taskCueOnce = false;
            if (QuotaVisible) return;
            statusText.Text = TaskState.Label(next);
            if (!dragging) { gesture = ""; bool cue = next == PetState.Completed || next == PetState.Failed; Play(cue ? TaskState.Animation(next) : RestAnimation()); taskCueOnce = cue; }
        }
        public void Gesture(string name, double seconds) { CancelReactionPreview(); CancelCharacterClicks(); CancelDoubleClickReaction(); CancelIdleEasterEgg(); DismissQuotaInfo(); gesture = name; gestureEnd = DateTime.UtcNow.AddSeconds(seconds); Play(name, true); }
        internal int BeginQuotaInfo(bool balance = false) {
            CancelReactionPreview();
            CancelCharacterClicks(); CancelDoubleClickReaction();
            CancelIdleEasterEgg();
            StopQuotaTimer(); if (quotaBubble != null) quotaBubble.HideImmediately();
            int ticket = quota.Begin(); gesture = ""; statusText.Text = balance ? "查询余额" : "查询额度";
            Play(sprites.Clips.ContainsKey("quota-thinking") ? "quota-thinking" : "running");
            if (quotaBubble == null) quotaBubble = new QuotaBubbleWindow { Dismiss = DismissQuotaInfo, ShowInTaskbar = ShowInTaskbar };
            quotaBubble.SetQueryLoading(balance);
            var lead = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(QueryBubbleDelayMs) }; quotaBubbleTimer = lead;
            lead.Tick += delegate { lead.Stop(); if (quotaBubbleTimer != lead) return; quotaBubbleTimer = null; RevealQuotaBubble(ticket); };
            lead.Start();
            return ticket;
        }
        private void RevealQuotaBubble(int ticket) {
            if (closed || !quota.Reveal(ticket)) return;
            StopQuotaBubbleTimer();
            if (IsVisible) { quotaBubble.Owner = this; PositionQuotaBubble(); quotaBubble.Reveal(); }
        }
        internal bool ShowQuotaResult(int ticket, QuotaSnapshot snapshot) { return PresentQuota(ticket, snapshot, false); }
        internal bool ShowQuotaError(int ticket) { return PresentQuota(ticket, null, true); }
        internal bool ShowBalanceResult(int ticket, BalanceSnapshot value) { return PresentQuota(ticket, null, false, true, value); }
        internal bool ShowBalanceError(int ticket, string error) { return PresentQuota(ticket, null, true, true, null, error); }
        private bool PresentQuota(int ticket, QuotaSnapshot snapshot, bool error, bool balance = false, BalanceSnapshot value = null, string message = null) {
            if (closed || !quota.Present(ticket, DateTime.UtcNow)) return false;
            if (quotaBubble == null) quotaBubble = new QuotaBubbleWindow { Dismiss = DismissQuotaInfo, ShowInTaskbar = ShowInTaskbar };
            if (balance) quotaBubble.SetBalance(value, message); else if (error) quotaBubble.SetUnavailable(); else quotaBubble.SetSnapshot(snapshot);
            statusText.Text = balance ? "查询余额" : "查询额度";
            // Start the visible result with a fresh animation cycle; query
            // latency must not decide where the five-second preview begins.
            Play(sprites.Clips.ContainsKey("quota-thinking") ? "quota-thinking" : "running", true);
            // A loading bubble already on screen is updated in place, without
            // replaying its fade-in. The original result+5s deadline is retained.
            RevealQuotaBubble(ticket);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) }; quotaTimer = timer;
            timer.Tick += delegate {
                if (quotaTimer != timer) { timer.Stop(); return; }
                if (quota.Expire(ticket, DateTime.UtcNow)) { StopQuotaTimer(); RestoreAfterQuota(); }
                else timer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, (quota.ExpiresAt - DateTime.UtcNow).TotalMilliseconds));
            };
            timer.Start(); return true;
        }
        internal bool ConsumeQuotaClick() { if (!QuotaVisible) return false; DismissQuotaInfo(); return true; }
        internal void DismissQuotaInfo() {
            if (!QuotaVisible) return;
            quota.Dismiss(); StopQuotaTimer(); RestoreAfterQuota();
        }
        private void StopQuotaBubbleTimer() { if (quotaBubbleTimer != null) { quotaBubbleTimer.Stop(); quotaBubbleTimer = null; } }
        private void StopQuotaTimer() { StopQuotaBubbleTimer(); if (quotaTimer != null) { quotaTimer.Stop(); quotaTimer = null; } }
        private void RestoreAfterQuota() {
            if (quotaBubble != null) quotaBubble.FadeAway();
            if (closed) return;
            hoverConsumed |= characterHovered; hoverJumping = false;
            gesture = ""; statusText.Text = TaskState.Label(state); Play(RestAnimation());
        }
        private string RestAnimation() { return ShouldHoverJump ? "jumping" : state == PetState.Idle || state == PetState.Completed || state == PetState.Interrupted || state == PetState.Failed ? "idle" : TaskState.Animation(state); }
        internal void SetCharacterHovered(bool value) {
            if (closed || characterHovered == value) return;
            characterHovered = value;
            if (!value) hoverConsumed = false;
            RefreshHoverAnimation();
        }
        internal void SetAnimationPaused(bool value) {
            CancelReactionPreview();
            CancelCharacterClicks(); CancelDoubleClickReaction();
            paused = value; CancelIdleEasterEgg(); RefreshHoverAnimation();
            if (DoubleClickEasterEgg.IsReaction(clip.Name)) Play(RestAnimation());
        }
        private void RefreshHoverAnimation() {
            if (ShouldHoverJump) { CancelIdleEasterEgg(); Play("jumping"); }
            else if (hoverJumping) Play(RestAnimation(), true);
        }
        private void PositionQuotaBubble() {
            if (!IsVisible || closed) return;
            PlaceInteractionBubbles(WorkingArea(), new Rect(Left, Top, Width, Height));
        }
        internal void PlaceInteractionBubbles(Rect area, Rect pet) {
            if (quotaBubble != null && (quota.BubbleRevealed || quotaBubble.IsVisible)) quotaBubble.Place(area, pet);
            if (reactionBubble != null && (reactionSpeechActive || reactionBubble.IsVisible)) reactionBubble.Place(area, pet);
        }
        private void Play(string name, bool restart = false) {
            if (reactionPlaying && name != reactionAnimation) CancelDoubleClickReaction();
            if (name != "idle-baguette" && idleSchedule.Playing) idleSchedule.Reset();
            hoverJumping = name == "jumping" && ShouldHoverJump;
            if (hoverJumping) hoverConsumed = true;
            if (!restart && clip != null && clip.Name == name) return;
            taskCueOnce = false; clip = sprites.Clips[name]; frame = 0; figure.Source = clip.Frames[0]; animationTimer.Interval = TimeSpan.FromMilliseconds(clip.Durations[0]);
        }
        private void CancelIdleEasterEgg() {
            idleSchedule.Reset();
            if (clip != null && clip.Name == "idle-baguette") { gesture = ""; Play(RestAnimation()); }
        }
        private void UpdateIdleEasterEgg() {
            if (closed || !IdleEasterEggReady) { idleSchedule.Reset(); return; }
            long now = idleClock.ElapsedMilliseconds;
            bool interaction = PointerBusy || menuOpen || characterHovered || reactionPlaying || clicks.PendingSingle;
            bool eligible = IdleEasterEggSchedule.Eligible(state, IsVisible, paused, interaction, QuotaVisible, gesture.Length > 0, clip != null && (clip.Name == "idle" || idleSchedule.Playing));
            if (!eligible) { if (idleSchedule.Playing) CancelIdleEasterEgg(); else idleSchedule.Reset(); idleTimer.Interval = TimeSpan.FromSeconds(1); return; }
            if (idleSchedule.Poll(now, true, IdleEasterEggSchedule.Duration(sprites.Clips["idle-baguette"]))) Play("idle-baguette", true);
            long until = idleSchedule.NextAtMs.HasValue ? idleSchedule.NextAtMs.Value - now : 1000;
            if (idleSchedule.Playing && until <= 0) until = 1000;
            idleTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(1000, until)));
        }
        private void Tick(object sender, EventArgs e) {
            if (gesture.Length > 0 && DateTime.UtcNow >= gestureEnd && !dragging && !QuotaVisible) { gesture = ""; Play(RestAnimation()); UpdateIdleEasterEgg(); return; }
            if (paused) return;
            if (reactionPlaying) { reactionElapsedMs += clip.Durations[frame]; reactionFrameClock.Restart(); }
            if (clip.AdvanceFrame(ref frame, taskCueOnce || hoverJumping)) {
                if (clip.Name == "idle-baguette") idleSchedule.Complete(idleClock.ElapsedMilliseconds);
                hoverJumping = false; CancelDoubleClickReaction();
                gesture = ""; taskCueOnce = false; Play(RestAnimation()); UpdateIdleEasterEgg(); return;
            }
            figure.Source = clip.Frames[frame]; animationTimer.Interval = TimeSpan.FromMilliseconds(clip.Durations[frame]);
            if (reactionPlaying) UpdateReactionSpeech();
        }
        private void CancelCharacterClicks() { clicks.Cancel(); clickTimer.Stop(); }
        private void PerformSingleClick() {
            if (!reactionPlaying && !paused && !menuOpen && !QuotaVisible && !taskCueOnce
                && (state == PetState.Idle || state == PetState.Completed || state == PetState.Interrupted)) Gesture("waving", 1.4);
        }
        internal bool TryStartDoubleClickEasterEgg() {
            return StartDoubleClickReaction(DoubleClickEasterEgg.Animation);
        }
        internal bool TryStartDizzyEasterEgg() { return StartDoubleClickReaction(DoubleClickEasterEgg.DizzyAnimation); }
        internal bool TryStartRandomDoubleClickEasterEgg() {
            if (closed || !DoubleClickEasterEggReady || reactionPlaying) return false;
            string chosen;
            if (!reactionChance.TryChoose(DoubleClickEasterEgg.Eligible(state, paused, PointerBusy, menuOpen, QuotaVisible, taskCueOnce), DizzyEasterEggReady, out chosen)) return false;
            return StartDoubleClickReaction(chosen);
        }
        private bool StartDoubleClickReaction(string name) {
            if (closed || !sprites.Clips.ContainsKey(name) || !DoubleClickEasterEgg.Eligible(state, paused, PointerBusy, menuOpen, QuotaVisible, taskCueOnce)) return false;
            CancelReactionPreview();
            CancelCharacterClicks(); CancelDoubleClickReaction(); CancelIdleEasterEgg(); gesture = "";
            hoverConsumed |= characterHovered; reactionPlaying = true; reactionElapsedMs = 0; reactionAnimation = name;
            Play(name, true); reactionFrameClock.Restart(); UpdateReactionSpeech(); return true;
        }
        private void UpdateReactionSpeech() {
            speechTimer.Stop(); if (!reactionPlaying) return;
            int elapsed = reactionElapsedMs + (int)Math.Min(clip.Durations[frame], reactionFrameClock.ElapsedMilliseconds);
            bool speaking = clip.Name == DoubleClickEasterEgg.Animation && elapsed >= clip.SpeechStartMs && elapsed < clip.SpeechEndMs;
            if (speaking != reactionSpeechActive) {
                reactionSpeechActive = speaking;
                if (speaking) {
                    if (reactionBubble == null) reactionBubble = new QuotaBubbleWindow { Dismiss = delegate { CancelDoubleClickReaction(); Play(RestAnimation()); }, ShowInTaskbar = ShowInTaskbar };
                    reactionBubble.SetSpeech(DoubleClickEasterEgg.Phrase);
                    if (IsVisible) { reactionBubble.Owner = this; PositionQuotaBubble(); reactionBubble.Reveal(); }
                } else if (reactionBubble != null) reactionBubble.FadeAway();
            }
            int next = elapsed < clip.SpeechStartMs ? clip.SpeechStartMs : clip.SpeechEndMs;
            if (next > elapsed && next < reactionElapsedMs + clip.Durations[frame]) {
                speechTimer.Interval = TimeSpan.FromMilliseconds(next - elapsed); speechTimer.Start();
            }
        }
        private void CancelDoubleClickReaction() {
            speechTimer.Stop(); reactionFrameClock.Stop(); reactionPlaying = reactionSpeechActive = false;
            reactionAnimation = "";
            if (reactionBubble != null) reactionBubble.FadeAway();
        }
        private void Down(object sender, MouseButtonEventArgs e) {
            BeginPetDrag(PointToScreen(e.GetPosition(this)));
            dragSurface = (UIElement)sender;
            if (!dragSurface.CaptureMouse()) { dragSurface = null; EndPetDrag(false); RefreshHoverAnimation(); }
            e.Handled = true;
        }
        private void Move(object sender, MouseEventArgs e) {
            if (dragSurface == null || !dragSurface.IsMouseCaptured) return;
            if (e.LeftButton != MouseButtonState.Pressed) { ReleasePetDrag(false); return; }
            MovePetDrag(PointToScreen(e.GetPosition(this)), source != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity);
        }
        internal void BeginPetDrag(Point pointer) {
            quotaPress = QuotaVisible;
            clickTimer.Stop();
            if (!reactionPlaying && DoubleClickEasterEggReady && DoubleClickEasterEgg.Eligible(state, paused, PointerBusy, menuOpen, QuotaVisible, taskCueOnce)) {
                if (clicks.Begin(pointer, idleClock.ElapsedMilliseconds)) PerformSingleClick();
            } else clicks.Cancel();
            pointerDown = true; CancelIdleEasterEgg(); RefreshHoverAnimation();
            motion.Begin(pointer); startWindow = new Point(Left, Top); dragging = false;
        }
        internal void MovePetDrag(Point pointer, Matrix fromDevice) {
            motion.Move(pointer, fromDevice);
            dragging = motion.Dragging;
            if (!dragging) return;
            CancelCharacterClicks();
            Left = startWindow.X + motion.Offset.X; Top = startWindow.Y + motion.Offset.Y;
            ApplyDragFeedback(motion.Animation);
        }
        internal void ApplyDragFeedback(string animation) {
            if (PreserveDragAnimation) return;
            if (animation.Length > 0) { gesture = ""; Play(animation); }
        }
        internal void FinishDragFeedback() {
            if (!PreserveDragAnimation) { gesture = ""; Play(RestAnimation()); }
        }
        private void Up(object sender, MouseButtonEventArgs e) {
            if (dragSurface == null || !dragSurface.IsMouseCaptured) return;
            MovePetDrag(PointToScreen(e.GetPosition(this)), source != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity);
            ReleasePetDrag(true); e.Handled = true;
        }
        private void LostCapture(object sender, MouseEventArgs e) {
            if (dragSurface != sender) return;
            dragSurface = null; EndPetDrag(false); RefreshHoverAnimation();
        }
        private void ReleasePetDrag(bool allowClick) {
            var captured = dragSurface; dragSurface = null;
            if (captured != null && captured.IsMouseCaptured) captured.ReleaseMouseCapture();
            EndPetDrag(allowClick);
            RefreshHoverAnimation();
        }
        internal void EndPetDrag(bool allowClick) {
            bool moved = dragging; dragging = false; pointerDown = false;
            bool quotaClick = PetDragFeedback.DismissQuotaOnRelease(quotaPress, moved, allowClick); quotaPress = false;
            if (moved) {
                CancelCharacterClicks();
                FinishDragFeedback();
                KeepVisible(); if (PositionChanged != null) PositionChanged();
            } else if (allowClick) {
                if (quotaClick) { CancelCharacterClicks(); ConsumeQuotaClick(); return; }
                if (!DoubleClickEasterEggReady) { PerformSingleClick(); return; }
                PetClickResult result = clicks.End(idleClock.ElapsedMilliseconds, true);
                if (result == PetClickResult.Double) TryStartRandomDoubleClickEasterEgg();
                else if (result == PetClickResult.PendingSingle) {
                    clickTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, clicks.SingleDueAtMs - idleClock.ElapsedMilliseconds)); clickTimer.Start();
                }
            } else CancelCharacterClicks();
        }
        public Rect WorkingArea() {
            if (source == null) return SystemParameters.WorkArea;
            var point = PointToScreen(new Point(Width / 2, Height / 2));
            var area = Forms.Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y)).WorkingArea;
            var transform = source.CompositionTarget.TransformFromDevice;
            return new Rect(transform.Transform(new Point(area.Left, area.Top)), transform.Transform(new Point(area.Right, area.Bottom)));
        }
        public void KeepVisible() {
            var area = WorkingArea(); Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width)); Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
        }
        private void CancelReactionPreview() { reactionPreviewTicket++; pendingReactionPreview = ""; }
        internal void SetMenuInteraction(bool value) {
            menuOpen = value;
            if (value) {
                CancelReactionPreview(); CancelCharacterClicks(); CancelDoubleClickReaction(); CancelIdleEasterEgg();
                if (DoubleClickEasterEgg.IsReaction(clip.Name)) Play(RestAnimation());
            }
            RefreshHoverAnimation();
            if (!value) { UpdateIdleEasterEgg(); QueueReactionPreview(); }
        }
        internal bool RequestReactionPreview(string name) {
            if (closed || !DoubleClickEasterEgg.IsReaction(name) || !sprites.Clips.ContainsKey(name)) return false;
            pendingReactionPreview = name; reactionPreviewTicket++; QueueReactionPreview(); return true;
        }
        private void QueueReactionPreview() {
            if (closed || menuOpen || pendingReactionPreview.Length == 0) return;
            int ticket = reactionPreviewTicket;
            // Background priority lets menu closure/input cleanup finish. A
            // ticket prevents stale callbacks from starting a later selection.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                if (closed || menuOpen || ticket != reactionPreviewTicket || pendingReactionPreview.Length == 0) return;
                string name = pendingReactionPreview; pendingReactionPreview = "";
                StartDoubleClickReaction(name);
            }));
        }
        private void AddReactionPreview(ContextMenu menu, MenuItem animations, string label, string name) {
            var item = new MenuItem { Header = label, IsEnabled = DoubleClickEasterEgg.Eligible(state, paused, PointerBusy, false, QuotaVisible, taskCueOnce) };
            item.Click += delegate { RequestReactionPreview(name); menu.IsOpen = false; };
            animations.Items.Add(item);
        }
        internal ContextMenu BuildMenu() {
            var menu = new ContextMenu();
            menu.Opened += delegate { SetMenuInteraction(true); };
            menu.Closed += delegate { SetMenuInteraction(false); };
            Add(menu, "打开任务面板   Ctrl+Alt+T", delegate { if (OpenChat != null) OpenChat(); });
            Add(menu, "新会话", delegate { if (NewChat != null) NewChat(); });
            Add(menu, QueryMenuText, delegate { if (QueryQuota != null) QueryQuota(); });
            menu.Items.Add(new Separator());
            var sizes = new MenuItem { Header = "桌宠大小" };
            foreach (double scale in new[] { 0.75, 1.0, 1.25, 1.5 }) {
                double chosen = scale; var item = new MenuItem { Header = ((int)(scale * 100)).ToString() + "%", IsCheckable = true, IsChecked = Math.Abs(PetScale - scale) < .02 };
                item.Click += delegate { SetScale(chosen); KeepVisible(); }; sizes.Items.Add(item);
            }
            menu.Items.Add(sizes);
            var animations = new MenuItem { Header = "查看现有动作" };
            string[] labels = { "待机", "向右移动", "向左移动", "挥手", "跳跃", "失落", "等待回应", "工作思考", "查看结果" };
            string[] names = { "idle", "running-right", "running-left", "waving", "jumping", "failed", "waiting", "running", "review" };
            for (int i = 0; i < names.Length; i++) { string chosen = names[i]; var item = new MenuItem { Header = labels[i] }; item.Click += delegate { Gesture(chosen, 5); }; animations.Items.Add(item); }
            if (IdleEasterEggReady) { var egg = new MenuItem { Header = "吃法棍（待机彩蛋）" }; egg.Click += delegate { Gesture("idle-baguette", IdleEasterEggSchedule.Duration(sprites.Clips["idle-baguette"]) / 1000.0 + 1); }; animations.Items.Add(egg); }
            if (DoubleClickEasterEggReady) AddReactionPreview(menu, animations, "被双击惹恼（彩蛋）", DoubleClickEasterEgg.Animation);
            if (DizzyEasterEggReady) AddReactionPreview(menu, animations, "眩晕后恢复（彩蛋）", DoubleClickEasterEgg.DizzyAnimation);
            menu.Items.Add(animations);
            var pause = new MenuItem { Header = "暂停动画", IsCheckable = true, IsChecked = paused }; pause.Click += delegate { SetAnimationPaused(pause.IsChecked); }; menu.Items.Add(pause);
            menu.Items.Add(new Separator());
            Add(menu, "隐藏到托盘", delegate { if (HidePet != null) HidePet(); });
            Add(menu, "退出", delegate { if (Exit != null) Exit(); });
            return menu;
        }
        private static void Add(ContextMenu menu, string title, Action action) { var item = new MenuItem { Header = title }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    }
    public sealed class ChatWindow : Window {
        private readonly Controller controller;
        private readonly PetWindow pet;
        private readonly StackPanel messages = new StackPanel(), approvals = new StackPanel();
        private readonly ScrollViewer scroll;
        private readonly TextBox input;
        private readonly TextBlock status, account, folder;
        private readonly Button send, stop, newChat, workspace, resetWorkspace, providerPicker, collapse;
        private readonly Button attach, modelPicker, effortPicker;
        private readonly TextBlock modelName, effortName, placeholder;
        private readonly WrapPanel attachmentCards = new WrapPanel();
        private readonly ScrollViewer attachmentScroll;
        private readonly List<ChatAttachment> pendingAttachments = new List<ChatAttachment>();
        private readonly Dictionary<string, TextBox> bubbles = new Dictionary<string, TextBox>();
        private readonly List<Tuple<RpcEvent, Border>> requests = new List<Tuple<RpcEvent, Border>>();
        private bool closing;
        private readonly PanelFade panelFade = new PanelFade();
        private string shownBackend = "";
        private Point resizeOrigin;
        private Rect resizeBounds;
        private Matrix resizeFromDevice = Matrix.Identity;
        public ChatWindow(Controller model, PetWindow owner) {
            controller = model; pet = owner;
            Title = "Teto Desk · 任务面板"; Width = 448; Height = 480; MinWidth = 380; MinHeight = 360;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = true; Topmost = false;
            FontFamily = new FontFamily("Microsoft YaHei UI"); Foreground = Theme.Ink;
            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var chrome = new Border { Background = Brushes.White, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Child = grid };
            chrome.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (e.OriginalSource == chrome || e.OriginalSource == grid) { e.Handled = true; DragMove(); }
            };
            var surface = new Grid(); surface.Children.Add(chrome); AddResizeHandles(surface); Content = surface;
            var head = new DockPanel { LastChildFill = true, Background = Brushes.Transparent, Margin = new Thickness(0, 0, 0, 12) };
            collapse = Theme.CollapseButton(HidePanel); DockPanel.SetDock(collapse, Dock.Right); head.Children.Add(collapse);
            var titles = new StackPanel(); var title = Theme.Text("TETO DESK", 18, Theme.Rose); title.FontWeight = FontWeights.Bold; titles.Children.Add(title);
            head.Children.Add(titles);
            head.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; DragMove(); }; grid.Children.Add(head);
            var statusRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            status = Theme.Text("●  待机", 12, Theme.Rose); statusRow.Children.Add(status); Grid.SetRow(statusRow, 1); grid.Children.Add(statusRow);
            var location = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            location.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            location.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            location.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            newChat = Theme.Button("新会话", delegate { ClearDraft(); controller.NewChat(controller.Workspace); }, false); Grid.SetColumn(newChat, 2); location.Children.Add(newChat);
            folder = Theme.Text("", 11, Theme.Muted); folder.TextTrimming = TextTrimming.CharacterEllipsis; folder.TextWrapping = TextWrapping.NoWrap; folder.TextAlignment = TextAlignment.Center;
            workspace = Theme.Picker("", ChooseWorkspace); workspace.Content = folder; workspace.HorizontalContentAlignment = HorizontalAlignment.Left; AutomationProperties.SetName(workspace, "选择任务文件夹"); location.Children.Add(workspace);
            resetWorkspace = Theme.Button("×", ClearWorkspace, false); resetWorkspace.Width = 20; resetWorkspace.Height = 24; resetWorkspace.MinHeight = 24; resetWorkspace.FontSize = 15; resetWorkspace.Padding = new Thickness(0); resetWorkspace.BorderThickness = new Thickness(0); resetWorkspace.HorizontalAlignment = HorizontalAlignment.Right; resetWorkspace.VerticalAlignment = VerticalAlignment.Center; resetWorkspace.Margin = new Thickness(0, 0, 4, 0);
            resetWorkspace.ToolTip = "取消指定路径，恢复默认 Tasks，并开始新会话。\n不会删除原目录或文件。"; AutomationProperties.SetName(resetWorkspace, "取消指定任务路径"); location.Children.Add(resetWorkspace);
            account = Theme.Text(controller.AccountLabel, 11, Theme.Ink); account.TextWrapping = TextWrapping.NoWrap; account.TextTrimming = TextTrimming.CharacterEllipsis; account.VerticalAlignment = VerticalAlignment.Center; account.TextAlignment = TextAlignment.Center;
            providerPicker = Theme.Picker("", ChooseBackend); providerPicker.Margin = new Thickness(8, 0, 8, 0); AutomationProperties.SetName(providerPicker, "切换对话服务");
            var providerContent = new DockPanel(); var providerCaret = Theme.Text("⌄", 13, Theme.Muted); providerCaret.Margin = new Thickness(4, 0, 0, 0); DockPanel.SetDock(providerCaret, Dock.Right); providerContent.Children.Add(providerCaret); providerContent.Children.Add(account); providerPicker.Content = providerContent;
            Grid.SetColumn(providerPicker, 1); location.Children.Add(providerPicker); Grid.SetRow(location, 2); grid.Children.Add(location);
            scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 2, 6, 0) };
            var conversation = new StackPanel(); conversation.Children.Add(messages); conversation.Children.Add(approvals); scroll.Content = conversation; Grid.SetRow(scroll, 3); grid.Children.Add(scroll);
            var composerBody = new StackPanel();
            attachmentScroll = new ScrollViewer { Content = attachmentCards, MaxHeight = 96, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };
            composerBody.Children.Add(attachmentScroll);
            var inputSurface = new Grid();
            placeholder = Theme.Text("向 Teto 发送消息", 13, Theme.Muted); placeholder.IsHitTestVisible = false; inputSurface.Children.Add(placeholder);
            input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Theme.Ink, Background = Brushes.Transparent, BorderThickness = new Thickness(0), MinHeight = 52, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 20000, Padding = new Thickness(0) };
            AutomationProperties.SetName(input, "任务输入框"); input.ToolTip = "输入任务；Enter 发送，Shift+Enter 换行";
            input.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { e.Handled = true; if (!controller.Busy) Send(); } };
            input.TextChanged += delegate { placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; UpdateComposer(); };
            inputSurface.Children.Add(input); composerBody.Children.Add(inputSurface);
            var toolbar = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            attach = Theme.Button("+", ChooseAttachments, false); attach.Width = 32; attach.Height = 32; attach.FontSize = 22; attach.Padding = new Thickness(0); attach.Background = Brushes.Transparent; attach.BorderThickness = new Thickness(0); attach.Margin = new Thickness(0, 0, 4, 0);
            attach.ToolTip = "添加照片、文档和语音（最多10个，单个20MB）\nDeepSeek 语音先本地转写；最长10分钟。"; AutomationProperties.SetName(attach, "添加附件"); toolbar.Children.Add(attach);
            modelPicker = Theme.Picker("", ChooseModel);
            var modelContent = new DockPanel(); var modelCaret = Theme.Text("⌄", 13, Theme.Muted); modelCaret.Margin = new Thickness(4, 0, 0, 0); DockPanel.SetDock(modelCaret, Dock.Right); modelContent.Children.Add(modelCaret);
            modelName = Theme.Text("选择模型", 11, Theme.Ink); modelName.TextWrapping = TextWrapping.NoWrap; modelName.TextTrimming = TextTrimming.CharacterEllipsis; modelContent.Children.Add(modelName); modelPicker.Content = modelContent;
            AutomationProperties.SetName(modelPicker, "选择模型"); Grid.SetColumn(modelPicker, 1); toolbar.Children.Add(modelPicker);
            effortPicker = Theme.Picker("", ChooseEffort); effortPicker.Margin = new Thickness(6, 0, 6, 0);
            effortName = Theme.Text("强度 · 默认 ⌄", 11, Theme.Ink); effortName.TextWrapping = TextWrapping.NoWrap; effortPicker.Content = effortName;
            AutomationProperties.SetName(effortPicker, "选择思考强度"); Grid.SetColumn(effortPicker, 2); toolbar.Children.Add(effortPicker);
            var actions = new Grid { Width = 32, Height = 32 };
            send = Theme.Button("↑", Send, true); send.Padding = new Thickness(0); send.FontSize = 20; send.ToolTip = "发送消息"; AutomationProperties.SetName(send, "发送消息"); actions.Children.Add(send);
            stop = Theme.Button("■", Stop, true); stop.Padding = new Thickness(0); stop.FontSize = 12; stop.ToolTip = "停止任务"; AutomationProperties.SetName(stop, "停止任务"); actions.Children.Add(stop);
            var roundAction = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' CornerRadius='16'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.85'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"); send.Template = roundAction; stop.Template = roundAction;
            Grid.SetColumn(actions, 3); toolbar.Children.Add(actions); composerBody.Children.Add(toolbar);
            var composer = new Border { Child = composerBody, CornerRadius = new CornerRadius(18), Background = Theme.Soft, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(12), Margin = new Thickness(0, 12, 0, 8), AllowDrop = true };
            composer.PreviewDragOver += delegate(object sender, DragEventArgs e) { e.Effects = !controller.Busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            composer.Drop += delegate(object sender, DragEventArgs e) { if (!controller.Busy && e.Data.GetDataPresent(DataFormats.FileDrop)) AddAttachments((string[])e.Data.GetData(DataFormats.FileDrop)); e.Handled = true; };
            Grid.SetRow(composer, 4); grid.Children.Add(composer);
            var hint = Theme.Text("Enter 发送 · Shift+Enter 换行", 10, Theme.Muted); hint.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(hint, 5); grid.Children.Add(hint);
            surface.SizeChanged += delegate { attachmentScroll.MaxHeight = surface.ActualHeight < 400 ? 44 : 96; input.MinHeight = surface.ActualHeight < 400 ? 44 : 52; input.MaxHeight = surface.ActualHeight < 400 ? 44 : 110; };
            controller.Changed += Refresh; controller.ApprovalRequested += AddApproval;
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { HidePanel(); e.Handled = true; } };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!closing) { e.Cancel = true; HidePanel(); } };
            Refresh();
        }
        public void ShowPanel() {
            if (closing) return;
            bool visible = IsVisible;
            if (!visible) Place();
            panelFade.Reveal(this, visible, delegate { Show(); });
            Activate(); input.Focus();
        }
        public void HidePanel() {
            if (closing || !IsVisible) return;
            panelFade.Conceal(this, delegate { Hide(); });
        }
        private void Place() {
            Rect area = pet.WorkingArea();
            // Reopening preserves the size chosen during this run. Clamp only
            // when the current monitor cannot contain the selected dimensions.
            Width = Math.Max(MinWidth, Math.Min(Width, area.Width - 16));
            Height = Math.Max(MinHeight, Math.Min(Height, area.Height - 16));
            double x = pet.Left - Width - 16; if (x < area.Left) x = pet.Left + pet.Width + 16;
            Left = Math.Max(area.Left + 8, Math.Min(x, area.Right - Width - 8)); Top = Math.Max(area.Top + 8, Math.Min(pet.Top + pet.Height - Height, area.Bottom - Height - 8));
        }
        private void AddResizeHandles(Grid surface) {
            var template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Thumb'><Border Background='Transparent'/></ControlTemplate>");
            AddResizeHandle(surface, template, -1, 0, HorizontalAlignment.Left, VerticalAlignment.Stretch, 6, Double.NaN, new Thickness(0, 18, 0, 18), Cursors.SizeWE, "调整窗口左边缘");
            AddResizeHandle(surface, template, 1, 0, HorizontalAlignment.Right, VerticalAlignment.Stretch, 6, Double.NaN, new Thickness(0, 18, 0, 18), Cursors.SizeWE, "调整窗口右边缘");
            AddResizeHandle(surface, template, 0, -1, HorizontalAlignment.Stretch, VerticalAlignment.Top, Double.NaN, 6, new Thickness(18, 0, 18, 0), Cursors.SizeNS, "调整窗口上边缘");
            AddResizeHandle(surface, template, 0, 1, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, Double.NaN, 6, new Thickness(18, 0, 18, 0), Cursors.SizeNS, "调整窗口下边缘");
            AddResizeHandle(surface, template, -1, -1, HorizontalAlignment.Left, VerticalAlignment.Top, 18, 18, new Thickness(0), Cursors.SizeNWSE, "调整窗口左上角");
            AddResizeHandle(surface, template, 1, -1, HorizontalAlignment.Right, VerticalAlignment.Top, 18, 18, new Thickness(0), Cursors.SizeNESW, "调整窗口右上角");
            AddResizeHandle(surface, template, -1, 1, HorizontalAlignment.Left, VerticalAlignment.Bottom, 18, 18, new Thickness(0), Cursors.SizeNESW, "调整窗口左下角");
            AddResizeHandle(surface, template, 1, 1, HorizontalAlignment.Right, VerticalAlignment.Bottom, 18, 18, new Thickness(0), Cursors.SizeNWSE, "调整窗口右下角");
            surface.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M3,13 L13,3 M8,13 L13,8"), Stroke = Theme.Muted, StrokeThickness = 1, Opacity = .5, Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 6, 6), IsHitTestVisible = false });
        }
        private void AddResizeHandle(Grid surface, ControlTemplate template, int horizontal, int vertical, HorizontalAlignment alignX, VerticalAlignment alignY, double width, double height, Thickness margin, Cursor cursor, string label) {
            var handle = new Thumb { Template = template, Width = width, Height = height, HorizontalAlignment = alignX, VerticalAlignment = alignY, Margin = margin, Cursor = cursor, Focusable = false };
            AutomationProperties.SetName(handle, label);
            handle.DragStarted += delegate {
                resizeOrigin = PointToScreen(Mouse.GetPosition(this)); resizeBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
                var source = PresentationSource.FromVisual(this) as HwndSource;
                resizeFromDevice = source == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            };
            handle.DragDelta += delegate(object sender, DragDeltaEventArgs e) {
                if (WindowState != WindowState.Normal) return;
                Vector delta = resizeFromDevice.Transform(PointToScreen(Mouse.GetPosition(this)) - resizeOrigin);
                double nextWidth = resizeBounds.Width, nextHeight = resizeBounds.Height;
                if (horizontal != 0) nextWidth = Math.Max(MinWidth, resizeBounds.Width + horizontal * delta.X);
                if (vertical != 0) nextHeight = Math.Max(MinHeight, resizeBounds.Height + vertical * delta.Y);
                Width = nextWidth; Height = nextHeight;
                if (horizontal < 0) Left = resizeBounds.Right - nextWidth;
                if (vertical < 0) Top = resizeBounds.Bottom - nextHeight;
                e.Handled = true;
            };
            surface.Children.Add(handle);
        }
        private async void Send() {
            string text = input.Text.Trim(); if (controller.Busy || (text.Length == 0 && pendingAttachments.Count == 0)) return;
            if (await controller.SendAsync(text, new List<ChatAttachment>(pendingAttachments))) ClearDraft();
        }
        private async void Stop() { await controller.StopAsync(); }
        private void ClearDraft() { pendingAttachments.Clear(); input.Clear(); RefreshAttachments(); }
        private void ChooseAttachments() {
            if (controller.Busy) return;
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "添加照片、文件和语音", Multiselect = true, CheckFileExists = true, Filter = "图片、文档和音频|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp;*.tif;*.tiff;*.pdf;*.docx;*.xlsx;*.pptx;*.txt;*.md;*.csv;*.json;*.cs;*.py;*.wav;*.mp3;*.m4a;*.aac;*.wma;*.flac;*.ogg;*.opus|所有文件|*.*" };
            if (dialog.ShowDialog(this) == true) AddAttachments(dialog.FileNames);
        }
        private void AddAttachments(IEnumerable<string> paths) {
            if (controller.Busy) return;
            try {
                var added = new List<ChatAttachment>(pendingAttachments);
                foreach (string path in paths) {
                    var file = AttachmentInput.FromFile(path);
                    if (!added.Exists(f => String.Equals(f.Path, file.Path, StringComparison.OrdinalIgnoreCase))) added.Add(file);
                }
                AttachmentInput.Validate(added, null);
                pendingAttachments.Clear(); pendingAttachments.AddRange(added); RefreshAttachments(); input.Focus();
            } catch (Exception e) { MessageBox.Show(this, e.Message, "无法添加附件"); }
        }
        private void RefreshAttachments() {
            attachmentCards.Children.Clear();
            foreach (var file in pendingAttachments) {
                var captured = file;
                attachmentCards.Children.Add(AttachmentCard(file, delegate { pendingAttachments.Remove(captured); RefreshAttachments(); }, 174));
            }
            attachmentScroll.Visibility = pendingAttachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateComposer();
        }
        private static Border AttachmentCard(ChatAttachment file, Action remove, double maxWidth) {
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = new Border { Width = 30, Height = 32, CornerRadius = new CornerRadius(5), Background = Theme.Soft, Margin = new Thickness(0, 0, 7, 0) };
            if (file.IsImage) {
                try {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 60; bitmap.UriSource = new Uri(file.Path); bitmap.EndInit(); bitmap.Freeze();
                    icon.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
                } catch { icon.Child = Theme.Text("图", 12, Theme.Rose); }
            } else { var glyph = Theme.Text("▤", 18, Theme.Rose); glyph.HorizontalAlignment = HorizontalAlignment.Center; glyph.VerticalAlignment = VerticalAlignment.Center; icon.Child = glyph; }
            row.Children.Add(icon);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var name = Theme.Text(file.Name, 10, Theme.Ink); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis; info.Children.Add(name);
            var detail = Theme.Text((file.IsImage ? "图片" : DeepSeekInput.IsAudio(file.Path) ? "语音" : "文件") + " · " + file.SizeLabel, 9, Theme.Muted); detail.TextWrapping = TextWrapping.NoWrap; info.Children.Add(detail); Grid.SetColumn(info, 1); row.Children.Add(info);
            if (remove != null) { var close = Theme.Button("×", remove, false); close.Width = 20; close.MinHeight = 20; close.Height = 24; close.Padding = new Thickness(0); close.BorderThickness = new Thickness(0); close.Background = Brushes.Transparent; close.Margin = new Thickness(3, 0, 0, 0); close.ToolTip = "移除 " + file.Name; AutomationProperties.SetName(close, "移除附件 " + file.Name); Grid.SetColumn(close, 2); row.Children.Add(close); }
            var card = new Border { Child = row, Width = maxWidth, Background = Brushes.White, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(7), Margin = new Thickness(0, 0, 6, 5), ToolTip = file.Name + "\n" + file.SizeLabel + (file.ReadHint.Length > 0 ? "\n" + file.ReadHint : "") };
            AutomationProperties.SetName(card, "附件 " + file.Name); return card;
        }
        private async void ChooseModel() {
            if (controller.Busy) return;
            try { if (controller.Models.Count == 0) await controller.ConnectAsync(); }
            catch (Exception e) { MessageBox.Show(this, e.Message, "无法读取模型列表"); return; }
            if (controller.Busy) return;
            var menu = new ContextMenu { PlacementTarget = modelPicker, Placement = PlacementMode.Top, MaxHeight = 350 };
            foreach (var model in controller.Models) {
                var captured = model;
                var item = new MenuItem { Header = model.Name, IsCheckable = true, IsChecked = model.Id == controller.Model, ToolTip = model.Id + (model.SupportsImages ? " · 支持图片" : " · 仅文字") };
                item.Click += delegate { controller.SetModel(captured.Id); }; menu.Items.Add(item);
            }
            modelPicker.ContextMenu = menu; menu.IsOpen = true;
        }
        private void ChooseBackend() {
            if (controller.Busy || controller.Connecting) return;
            controller.RefreshProviders();
            var menu = BuildProviderMenu(); providerPicker.ContextMenu = menu; menu.IsOpen = true;
        }
        internal ContextMenu BuildProviderMenu() {
            var menu = new ContextMenu { PlacementTarget = providerPicker, Placement = PlacementMode.Bottom };
            foreach (string backend in controller.AvailableBackends) {
                string selected = backend;
                var item = new MenuItem { Header = backend == "deepseek" ? "DeepSeek Harness · 账号登录" : "ChatGPT · Codex", IsCheckable = true, IsChecked = controller.Backend == backend };
                item.Click += async delegate {
                    if (!controller.SetBackend(selected)) return; ClearDraft();
                    try { await controller.ConnectAsync(); } catch (Exception e) { MessageBox.Show(this, e.Message, "连接服务"); }
                    Refresh();
                }; menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            var files = new MenuItem { Header = "打开任务文件夹" }; files.Click += delegate { try { Process.Start(new ProcessStartInfo("explorer.exe", HarnessRuntime.Quote(controller.Workspace)) { UseShellExecute = true }); } catch (Exception e) { MessageBox.Show(this, e.Message, "打开文件夹"); } }; menu.Items.Add(files);
            var recheck = new MenuItem { Header = "重新检测本机客户端" };
            recheck.Click += delegate { controller.RefreshProviders(); Refresh(); }; menu.Items.Add(recheck);
            var path = new MenuItem { Header = "定位已安装的客户端…" };
            path.Click += async delegate {
                var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择 codex.exe 或 DeepSeek Harness.exe", Filter = "支持的客户端|codex*.exe;*DeepSeek*.exe|应用程序|*.exe", CheckFileExists = true };
                if (dialog.ShowDialog(this) != true) return;
                try { controller.ConfigureExecutable(dialog.FileName); ClearDraft(); await controller.ConnectAsync(); } catch (Exception e) { MessageBox.Show(this, e.Message, "连接服务"); }
                Refresh();
            }; menu.Items.Add(path); return menu;
        }
        private void ChooseEffort() {
            if (controller.Busy || controller.Models.Count == 0) return;
            var model = ModelCatalog.Select(controller.Models, controller.Model);
            var menu = new ContextMenu { PlacementTarget = effortPicker, Placement = PlacementMode.Top };
            foreach (var effort in model.Efforts) {
                var captured = effort;
                var item = new MenuItem { Header = ModelCatalog.EffortLabel(effort.Id) + " · " + effort.Id + (effort.Id == model.Effort ? "（模型默认）" : ""), IsCheckable = true, IsChecked = effort.Id == controller.Effort, ToolTip = effort.Description };
                item.Click += delegate { controller.SetEffort(captured.Id); }; menu.Items.Add(item);
            }
            effortPicker.ContextMenu = menu; menu.IsOpen = true;
        }
        private void UpdateComposer() {
            if (send == null) return;
            bool busy = controller.Busy;
            send.IsEnabled = !busy && controller.HasAvailableBackend && (input.Text.Trim().Length > 0 || pendingAttachments.Count > 0);
            send.Visibility = busy ? Visibility.Collapsed : Visibility.Visible; stop.Visibility = busy ? Visibility.Visible : Visibility.Collapsed; stop.IsEnabled = busy;
            input.IsReadOnly = busy; attach.IsEnabled = !busy; attachmentCards.IsEnabled = !busy; modelPicker.IsEnabled = !busy;
            if (controller.Models.Count > 0) {
                var model = ModelCatalog.Select(controller.Models, controller.Model);
                modelName.Text = model.Name; modelPicker.ToolTip = "选择模型 · " + model.Id;
                effortName.Text = "强度 · " + ModelCatalog.EffortLabel(controller.Effort) + " ⌄";
                effortPicker.IsEnabled = !busy && model.Efforts.Count > 1;
                effortPicker.ToolTip = model.Efforts.Count > 0 ? "思考强度 · " + controller.Effort + "（较高强度通常需要更多时间）" : "此模型没有可调整的思考强度";
            } else { modelName.Text = "选择模型"; modelPicker.ToolTip = "连接 Codex 后读取模型列表"; effortPicker.IsEnabled = false; }
        }
        internal void PreviewDraft(string text, IList<ChatAttachment> files) { input.Text = text; pendingAttachments.Clear(); pendingAttachments.AddRange(files); RefreshAttachments(); }
        internal object ComposerProperties() {
            var controls = new[] { attach, modelPicker, effortPicker, controller.Busy ? stop : send };
            bool fits = true; var bounds = new List<object>();
            var toolbar = (Grid)attach.Parent;
            foreach (var button in controls) { Point p = button.TranslatePoint(new Point(), toolbar); fits &= p.X >= -.5 && p.X + button.ActualWidth <= toolbar.ActualWidth + .5; bounds.Add(J.O("name", AutomationProperties.GetName(button), "x", p.X, "width", button.ActualWidth)); }
            return J.O("model", controller.Model, "effort", controller.Effort, "attachments", pendingAttachments.Count, "toolbarWidth", toolbar.ActualWidth, "toolbarFits", fits, "controls", bounds, "sendingEnabled", send.IsEnabled, "stopVisible", stop.Visibility == Visibility.Visible);
        }
        private void ChooseWorkspace() {
            if (controller.Busy) return;
            using (var dialog = new Forms.FolderBrowserDialog { Description = "选择要处理的任务文件夹。更换文件夹会开始一个新会话。", SelectedPath = controller.Workspace, ShowNewFolderButton = true }) {
                if (dialog.ShowDialog() == Forms.DialogResult.OK) { ClearDraft(); controller.NewChat(dialog.SelectedPath); }
            }
        }
        private void ClearWorkspace() { if (controller.ResetWorkspace()) ClearDraft(); }
        public void Refresh() {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke((Action)Refresh); return; }
            pet.SetState(controller.Status.State);
            if (shownBackend != controller.Backend) { shownBackend = controller.Backend; bubbles.Clear(); messages.Children.Clear(); approvals.Children.Clear(); requests.Clear(); }
            status.Text = "●  " + (String.IsNullOrEmpty(controller.PreparationLabel) ? TaskState.Label(controller.Status.State) : controller.PreparationLabel); account.Text = controller.AccountLabel; providerPicker.ToolTip = "当前服务：" + (controller.Backend == "deepseek" ? "DeepSeek Harness" : "ChatGPT / Codex") + "\n点击切换服务，或打开任务文件夹";
            providerPicker.IsEnabled = !controller.Busy && !controller.Connecting;
            folder.Text = "▱  " + new DirectoryInfo(controller.Workspace).Name;
            resetWorkspace.Visibility = controller.HasCustomWorkspace ? Visibility.Visible : Visibility.Collapsed;
            resetWorkspace.IsEnabled = !controller.Busy && !controller.Connecting;
            workspace.Padding = controller.HasCustomWorkspace ? new Thickness(6, 5, 26, 5) : new Thickness(8, 5, 8, 5);
            workspace.ToolTip = "工作文件夹：" + controller.Workspace + "\n点击选择要读取或处理的项目文件夹；更换文件夹会开始新会话。";
            UpdateComposer(); workspace.IsEnabled = !controller.Busy; newChat.IsEnabled = !controller.Busy;
            bool bottom = scroll.ExtentHeight - scroll.VerticalOffset - scroll.ViewportHeight < 60;
            if (controller.Messages.Count == 0 && bubbles.Count > 0) { bubbles.Clear(); messages.Children.Clear(); }
            if (controller.Messages.Count == 0 && messages.Children.Count == 0) AddWelcome();
            if (controller.Messages.Count > 0 && bubbles.Count == 0) messages.Children.Clear();
            foreach (var entry in controller.Messages) {
                TextBox bubble;
                if (!bubbles.TryGetValue(entry.Id, out bubble)) {
                    var content = new StackPanel(); var label = Theme.Text(entry.Role == "user" ? "你" : entry.Role == "assistant" ? (controller.Backend == "deepseek" ? "Teto · DeepSeek" : "Teto · Codex") : "提示", 10, entry.Role == "notice" ? Theme.Muted : Theme.Rose); label.Margin = new Thickness(0, 0, 0, 6); content.Children.Add(label);
                    if (entry.Attachments.Count > 0) { var files = new WrapPanel(); foreach (var file in entry.Attachments) files.Children.Add(AttachmentCard(file, null, 174)); content.Children.Add(files); }
                    bubble = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = entry.Role == "notice" ? Theme.Muted : Theme.Ink, FontSize = entry.Role == "notice" ? 11 : 13, Padding = new Thickness(0), IsReadOnlyCaretVisible = false };
                    AutomationProperties.SetName(bubble, entry.Role == "assistant" ? "Codex 回复" : "会话消息"); content.Children.Add(bubble);
                    var border = new Border { Child = content, CornerRadius = new CornerRadius(12), Background = entry.Role == "user" ? Theme.Soft : Brushes.White, BorderBrush = Theme.Line, BorderThickness = entry.Role == "notice" ? new Thickness(0) : new Thickness(1), Padding = new Thickness(12), Margin = new Thickness(entry.Role == "user" ? 20 : 0, 0, entry.Role == "user" ? 0 : 6, 10) };
                    messages.Children.Add(border); bubbles[entry.Id] = bubble;
                }
                if (bubble.Text != entry.Text) { int start = bubble.SelectionStart, length = bubble.SelectionLength; bubble.Text = entry.Text; bubble.Select(Math.Min(start, bubble.Text.Length), Math.Min(length, Math.Max(0, bubble.Text.Length - start))); }
            }
            foreach (var request in requests.ToArray()) if (!controller.IsPending(request.Item1)) { approvals.Children.Remove(request.Item2); requests.Remove(request); }
            if (bottom) Dispatcher.BeginInvoke((Action)delegate { scroll.ScrollToEnd(); }, DispatcherPriority.Background);
        }
        private void AddWelcome() {
            var welcome = new StackPanel { Margin = new Thickness(2, 12, 8, 12) };
            var title = Theme.Text("今天一起做点什么？", 20, Theme.Ink); title.FontWeight = FontWeights.SemiBold; welcome.Children.Add(title);
            var intro = Theme.Text("输入任务，或添加照片、文档。\n" + (controller.Backend == "deepseek" ? "使用 DeepSeek Harness 已登录的账号。" : "使用现有 ChatGPT 登录与 Codex 额度。"), 12, Theme.Muted); intro.Margin = new Thickness(0, 10, 0, 20); welcome.Children.Add(intro);
            messages.Children.Add(welcome);
        }
        private void AddApproval(RpcEvent request) {
            var content = new StackPanel(); var title = Theme.Text("需要你确认", 14, Theme.Rose); title.FontWeight = FontWeights.SemiBold; title.Margin = new Thickness(0, 0, 0, 8); content.Children.Add(title);
            var border = new Border { Background = Theme.Soft, BorderBrush = Theme.Rose, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0, 0, 6, 10), Child = content };
            if (request.Method == "item/tool/requestUserInput") {
                var fields = new Dictionary<string, Func<string>>();
                foreach (object question in J.A(J.Get(request.Params, "questions"))) {
                    var description = Theme.Text(J.S(question, "question"), 12, Theme.Ink); description.Margin = new Thickness(0, 0, 0, 8); content.Children.Add(description);
                    object[] options = J.A(J.Get(question, "options")); string id = J.S(question, "id");
                    if (J.B(question, "isSecret")) {
                        var password = new PasswordBox { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(6) }; content.Children.Add(password); fields[id] = delegate { return password.Password; };
                    } else {
                        var answer = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(6), MinHeight = 32 };
                        foreach (object option in options) answer.Items.Add(J.S(option, "label")); content.Children.Add(answer); fields[id] = delegate { return answer.Text; };
                    }
                }
                content.Children.Add(Theme.Button("提交回答", delegate {
                    var answers = new Dictionary<string, object>();
                    foreach (var field in fields) { string value = field.Value().Trim(); if (value.Length == 0) { MessageBox.Show(this, "请填写回答后再提交。", "Teto Desk"); return; } answers[field.Key] = J.O("answers", new object[] { value }); }
                    controller.Answer(request, J.O("answers", answers));
                }, true));
            } else {
                string command = J.S(request.Params, "command"), reason = J.S(request.Params, "reason");
                string detail = request.Method == "item/permissions/requestApproval" ? J.Serialize(J.Get(request.Params, "permissions")) : command.Length > 0 ? command : request.Method == "item/fileChange/requestApproval" ? "修改文件：" + J.S(request.Params, "grantRoot") : "执行此操作";
                var display = new TextBox { Text = reason + (reason.Length > 0 ? "\n\n" : "") + detail, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 11, Background = Brushes.Transparent, BorderThickness = new Thickness(0), MaxHeight = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 10) }; content.Children.Add(display);
                var actions = new StackPanel { Orientation = Orientation.Horizontal };
                actions.Children.Add(Theme.Button("允许本次", delegate {
                    controller.Answer(request, request.Method == "item/permissions/requestApproval" ? J.O("permissions", J.Get(request.Params, "permissions"), "scope", "turn") : J.O("decision", "accept"));
                }, true));
                var deny = Theme.Button("拒绝", delegate { controller.Answer(request, request.Method == "item/permissions/requestApproval" ? J.O("permissions", J.O(), "scope", "turn") : J.O("decision", "decline")); }, false); deny.Margin = new Thickness(8, 0, 0, 0); actions.Children.Add(deny); content.Children.Add(actions);
            }
            approvals.Children.Add(border); requests.Add(Tuple.Create(request, border)); ShowPanel(); Dispatcher.BeginInvoke((Action)delegate { scroll.ScrollToEnd(); });
        }
        public void ExitWindow() { closing = true; panelFade.Cancel(this); Close(); }
    }
}
