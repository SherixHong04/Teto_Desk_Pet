using System;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Automation;
using System.Windows.Threading;

namespace TetoDesk {
    public sealed class QuotaWindow {
        public int Minutes;
        public double? RemainingPercent;
        public long? ResetsAt;
        public string Display { get { return RemainingPercent.HasValue ? RemainingPercent.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "暂不可用"; } }
        public string ResetDisplay(DateTime now) {
            if (!ResetsAt.HasValue || ResetsAt.Value < 0 || ResetsAt.Value > 253402300799L) return "重置时间暂不可用";
            DateTime reset = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ResetsAt.Value);
            TimeSpan wait = reset - now.ToUniversalTime();
            if (wait <= TimeSpan.Zero) return "即将重置";
            if (wait.TotalDays >= 1) {
                long hours = (long)Math.Ceiling(wait.TotalHours);
                return (hours / 24) + " 天" + (hours % 24 == 0 ? "" : " " + (hours % 24) + " 小时") + "后重置";
            }
            int minutes = (int)Math.Ceiling(wait.TotalMinutes);
            if (minutes >= 60) return (minutes / 60) + " 小时" + (minutes % 60 == 0 ? "" : " " + (minutes % 60) + " 分钟") + "后重置";
            return minutes + " 分钟后重置";
        }
        public static QuotaWindow Read(object value) {
            if (value == null) return null;
            var result = new QuotaWindow { Minutes = J.I(value, "windowDurationMins", 0) };
            double used; long reset;
            if (Double.TryParse(J.S(value, "usedPercent"), NumberStyles.Float, CultureInfo.InvariantCulture, out used) && !Double.IsNaN(used) && !Double.IsInfinity(used)) result.RemainingPercent = Math.Max(0, Math.Min(100, 100 - used));
            if (Int64.TryParse(J.S(value, "resetsAt"), out reset)) result.ResetsAt = reset;
            return result;
        }
    }
    public sealed class QuotaSnapshot {
        public QuotaWindow FiveHours, Week;
        public string LimitId;
        public static QuotaSnapshot Read(object response) {
            object buckets = J.Get(response, "rateLimitsByLimitId");
            object selected;
            var map = buckets as Dictionary<string, object>;
            if (map == null || !map.TryGetValue("codex", out selected)) selected = J.Get(response, "rateLimits");
            var result = new QuotaSnapshot { LimitId = J.S(selected, "limitId") };
            foreach (string key in new[] { "primary", "secondary" }) {
                var window = QuotaWindow.Read(J.Get(selected, key));
                if (window == null) continue;
                // Duration, rather than primary/secondary order, identifies the
                // two requested windows. Missing information is not zero usage.
                if (window.Minutes == 300) result.FiveHours = window;
                if (window.Minutes == 10080) result.Week = window;
            }
            return result;
        }
    }
    public sealed class QuotaInteraction {
        private int generation;
        public bool Visible { get; private set; }
        public bool HasResult { get; private set; }
        public bool BubbleRevealed { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public int Begin() { generation++; Visible = true; HasResult = BubbleRevealed = false; return generation; }
        public bool Reveal(int ticket) {
            if (!Visible || ticket != generation || BubbleRevealed) return false;
            BubbleRevealed = true; return true;
        }
        public bool Present(int ticket, DateTime now) {
            if (!Visible || ticket != generation || HasResult) return false;
            HasResult = true; ExpiresAt = now.AddSeconds(5); return true;
        }
        public bool Expire(int ticket, DateTime now) {
            if (!Visible || !HasResult || ticket != generation || now < ExpiresAt) return false;
            Dismiss(); return true;
        }
        public void Dismiss() { generation++; Visible = HasResult = BubbleRevealed = false; }
    }
    public static class QuotaPlacement {
        public static Point Head(Rect pet) { double scale = pet.Width / 192; return new Point(pet.Left + 131 * scale, pet.Top + 12 * scale); }
        public static Rect Place(Rect workArea, Rect pet, Size size) {
            var head = Head(pet);
            double width = Math.Min(size.Width, Math.Max(80, workArea.Width - 16)), height = Math.Min(size.Height, Math.Max(80, workArea.Height - 16));
            double left = Math.Max(workArea.Left + 8, Math.Min(head.X - 34, workArea.Right - width - 8));
            double top = Math.Max(workArea.Top + 8, Math.Min(head.Y - height - 8, workArea.Bottom - height - 8));
            return new Rect(left, top, width, height);
        }
    }
    internal sealed class QuotaDisplayRow {
        public readonly StackPanel Panel = new StackPanel();
        public readonly TextBlock Reset, Remaining;
        public readonly ProgressBar Bar;
        public QuotaDisplayRow(string label) {
            var heading = Theme.Text(label, 12, Brushes.Black); heading.FontWeight = FontWeights.SemiBold; heading.Margin = new Thickness(0, 0, 0, 1); Panel.Children.Add(heading);
            var details = new Grid(); details.ColumnDefinitions.Add(new ColumnDefinition()); details.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Reset = Theme.Text("重置时间暂不可用", 10.5, Theme.Color("#595959")); Reset.TextWrapping = TextWrapping.NoWrap; Reset.TextTrimming = TextTrimming.CharacterEllipsis;
            Remaining = Theme.Text("剩余 --", 10.5, Theme.Color("#595959")); Remaining.TextWrapping = TextWrapping.NoWrap; Remaining.Margin = new Thickness(6, 0, 0, 0); Grid.SetColumn(Remaining, 1);
            details.Children.Add(Reset); details.Children.Add(Remaining); Panel.Children.Add(details);
            Bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 4, Margin = new Thickness(0, 3, 0, 0), Background = Theme.Color("#DEDEDE"), Foreground = Theme.Color("#292929"), BorderThickness = new Thickness(0) };
            Bar.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ProgressBar'><Grid x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border Background='{TemplateBinding Background}' CornerRadius='2'/><Border x:Name='PART_Indicator' Background='{TemplateBinding Foreground}' HorizontalAlignment='Left' CornerRadius='2'/></Grid></ControlTemplate>");
            AutomationProperties.SetName(Bar, label + "剩余百分比"); Panel.Children.Add(Bar);
        }
        public void Set(QuotaWindow window, DateTime now) {
            Reset.Text = window == null ? "重置时间暂不可用" : window.ResetDisplay(now);
            bool known = window != null && window.RemainingPercent.HasValue;
            Remaining.Text = known ? "剩余 " + window.Display : "剩余 --";
            Bar.Value = known ? window.RemainingPercent.Value : 0;
            AutomationProperties.SetHelpText(Bar, known ? Remaining.Text : "额度数据暂不可用");
        }
    }
    public sealed class QuotaBubbleWindow : Window {
        internal const double QuotaWidth = 260, QuotaHeight = 184;
        private readonly TextBlock title;
        private readonly QuotaDisplayRow five, week;
        private readonly TextBlock speech;
        private readonly Border quotaContent, speechContent, balanceContent;
        private readonly TextBlock balanceAmount, balanceDetail, balancePeriod, balanceNext;
        private Size preferredSize = new Size(QuotaWidth, QuotaHeight);
        private QuotaSnapshot snapshot;
        private bool queryLoading;
        private readonly DispatcherTimer countdown;
        internal string SpeechText { get { return speech.Text; } }
        internal string FiveResetText { get { return five.Reset.Text; } }
        internal string WeekResetText { get { return week.Reset.Text; } }
        internal string FiveRemainingText { get { return five.Remaining.Text; } }
        internal string WeekRemainingText { get { return week.Remaining.Text; } }
        internal double FiveBarValue { get { return five.Bar.Value; } }
        internal double WeekBarValue { get { return week.Bar.Value; } }
        private readonly Path outline, accent, thoughts;
        private int transition;
        public Action Dismiss;
        public QuotaBubbleWindow() {
            Title = "Teto Desk · 剩余额度"; Width = QuotaWidth; Height = QuotaHeight;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true; Focusable = false;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            var root = new Grid { Background = Brushes.Transparent };
            outline = new Path { Fill = Brushes.White, Stroke = Theme.Color("#171717"), StrokeThickness = 2.1, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            accent = new Path { Stroke = Theme.Color("#171717"), StrokeThickness = .7, Opacity = .65, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false };
            thoughts = new Path { Fill = Brushes.White, Stroke = Theme.Color("#171717"), StrokeThickness = 1.8, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            root.Children.Add(outline); root.Children.Add(accent); root.Children.Add(thoughts);
            var content = new StackPanel();
            title = Theme.Text("剩余额度", 15, Brushes.Black); title.FontWeight = FontWeights.SemiBold; title.TextAlignment = TextAlignment.Center; title.Margin = new Thickness(0, 0, 0, 4); content.Children.Add(title);
            five = new QuotaDisplayRow("5 小时额度"); week = new QuotaDisplayRow("每周额度"); five.Panel.Margin = new Thickness(0, 0, 0, 7);
            content.Children.Add(five.Panel); content.Children.Add(week.Panel);
            quotaContent = new Border { Padding = new Thickness(30, 14, 30, 12), Margin = new Thickness(0, 0, 0, 38), Child = content };
            root.Children.Add(quotaContent);
            speech = Theme.Text("", 17, Brushes.Black); speech.FontFamily = new FontFamily("Yu Gothic UI"); speech.TextAlignment = TextAlignment.Center; speech.VerticalAlignment = VerticalAlignment.Center;
            speechContent = new Border { Padding = new Thickness(36, 12, 36, 12), Margin = new Thickness(0, 0, 0, 38), Child = speech, Visibility = Visibility.Collapsed };
            root.Children.Add(speechContent);
            var balancePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var balanceTitle = Theme.Text("DeepSeek 账户余额", 15, Brushes.Black); balanceTitle.FontWeight = FontWeights.SemiBold; balanceTitle.TextAlignment = TextAlignment.Center; balancePanel.Children.Add(balanceTitle);
            balanceAmount = Theme.Text("", 22, Brushes.Black); balanceAmount.FontWeight = FontWeights.SemiBold; balanceAmount.TextAlignment = TextAlignment.Center; balanceAmount.Margin = new Thickness(0, 2, 0, 2); balancePanel.Children.Add(balanceAmount);
            balanceDetail = Theme.Text("", 10, Theme.Muted); balanceDetail.TextAlignment = TextAlignment.Center; balancePanel.Children.Add(balanceDetail);
            balancePeriod = Theme.Text("", 12, Brushes.Black); balancePeriod.TextAlignment = TextAlignment.Center; balancePeriod.FontWeight = FontWeights.SemiBold; balancePeriod.Margin = new Thickness(0, 4, 0, 0); balancePanel.Children.Add(balancePeriod);
            balanceNext = Theme.Text("", 10, Theme.Muted); balanceNext.TextAlignment = TextAlignment.Center; balancePanel.Children.Add(balanceNext);
            balanceContent = new Border { Padding = new Thickness(28, 14, 28, 14), Margin = new Thickness(0, 0, 0, 38), Child = balancePanel, Visibility = Visibility.Collapsed };
            balanceContent.ToolTip = "充值与赠送余额按币种合计；峰谷时段按北京时间。\n规则核对：2026-10-19\n" + PricingPeriod.Source;
            root.Children.Add(balanceContent);
            Content = root; DrawBubble();
            countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; countdown.Tick += delegate { RefreshCountdown(DateTime.UtcNow); };
            Closed += delegate { countdown.Stop(); };
            PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; if (Dismiss != null) Dismiss(); };
            AutomationProperties.SetName(this, "剩余额度，左键关闭");
        }
        public void SetSnapshot(QuotaSnapshot snapshot) {
            SetSnapshot(snapshot, DateTime.UtcNow);
        }
        internal void SetSnapshot(QuotaSnapshot value, DateTime now) {
            SetQuotaLayout();
            snapshot = value; title.Text = "剩余额度"; RefreshCountdown(now); if (IsVisible) countdown.Start();
        }
        internal void RefreshCountdown(DateTime now) {
            if (balanceContent.Visibility == Visibility.Visible) { balancePeriod.Text = PricingPeriod.Label(now); balanceNext.Text = PricingPeriod.Next(now); return; }
            if (quotaContent.Visibility != Visibility.Visible) return;
            if (queryLoading) return;
            five.Set(snapshot == null ? null : snapshot.FiveHours, now); week.Set(snapshot == null ? null : snapshot.Week, now);
        }
        public void SetUnavailable() { SetQuotaLayout(); snapshot = null; title.Text = "查询未成功，请稍后重试"; title.FontSize = 12; RefreshCountdown(DateTime.UtcNow); if (IsVisible) countdown.Start(); }
        private void SetQuotaLayout() {
            countdown.Stop(); queryLoading = false; title.FontSize = 15;
            Title = "Teto Desk · 剩余额度"; preferredSize = new Size(QuotaWidth, QuotaHeight);
            quotaContent.Visibility = Visibility.Visible; speechContent.Visibility = balanceContent.Visibility = Visibility.Collapsed;
            Width = preferredSize.Width; Height = preferredSize.Height; DrawBubble();
            AutomationProperties.SetName(this, "剩余额度，左键关闭");
        }
        public void SetSpeech(string text) {
            if (String.IsNullOrWhiteSpace(text)) throw new ArgumentException("Speech cannot be empty.", "text");
            countdown.Stop(); Title = "Teto Desk · 彩蛋"; speech.Text = text;
            speech.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            double width = Math.Max(140, Math.Min(480, Math.Ceiling(speech.DesiredSize.Width + 72)));
            speech.Measure(new Size(width - 72, Double.PositiveInfinity));
            preferredSize = new Size(width, Math.Max(96, Math.Ceiling(speech.DesiredSize.Height + 80)));
            quotaContent.Visibility = balanceContent.Visibility = Visibility.Collapsed; speechContent.Visibility = Visibility.Visible;
            Width = preferredSize.Width; Height = preferredSize.Height; DrawBubble();
            AutomationProperties.SetName(this, text);
        }
        public void SetBalance(BalanceSnapshot value, string error = null) {
            countdown.Stop(); queryLoading = false; Title = "Teto Desk · DeepSeek 账户余额";
            preferredSize = new Size(QuotaWidth, QuotaHeight); Width = preferredSize.Width; Height = preferredSize.Height;
            quotaContent.Visibility = speechContent.Visibility = Visibility.Collapsed; balanceContent.Visibility = Visibility.Visible;
            balanceAmount.FontSize = value == null ? 13 : value.Wallets.Count > 1 ? 15 : 22;
            balanceAmount.Text = value == null ? "余额暂不可用" : value.TotalText;
            balanceDetail.Text = value == null ? "请检查 Harness 登录与网络" : value.DetailText;
            balanceContent.ToolTip = error ?? ("充值与赠送余额按币种合计；北京时间。\n规则核对：2026-10-19\n" + PricingPeriod.Source);
            RefreshCountdown(DateTime.UtcNow); DrawBubble(); if (IsVisible) countdown.Start(); AutomationProperties.SetName(this, "DeepSeek 账户余额，左键关闭");
        }
        internal void SetQueryLoading(bool balance) {
            if (balance) {
                SetBalance(null); balanceAmount.Text = "正在查询…"; balanceDetail.Text = "正在读取账户余额";
                balanceContent.ToolTip = "正在读取 DeepSeek 账户余额。";
            } else {
                SetQuotaLayout(); snapshot = null; title.Text = "正在查询额度…";
                five.Remaining.Text = week.Remaining.Text = "查询中"; five.Reset.Text = week.Reset.Text = "正在读取账户信息";
                five.Bar.Value = week.Bar.Value = 0;
            }
            queryLoading = true; AutomationProperties.SetName(this, balance ? "正在查询账户余额，左键关闭" : "正在查询额度，左键关闭");
        }
        public void Place(Rect area, Rect pet) {
            var rect = QuotaPlacement.Place(area, pet, preferredSize);
            Width = rect.Width; Height = rect.Height; Left = rect.Left; Top = rect.Top;
            DrawBubble();
        }
        private void DrawBubble() {
            double w = Width, b = Height - 38;
            // Fixed asymmetric curves imitate an ink outline without moving
            // the text or making the border jitter during the animation.
            outline.Data = Geometry.Parse(String.Format(CultureInfo.InvariantCulture,
                "M{0},3 C{1},1 {2},{3} {4},{5} C{6},{7} {8},{9} {10},{11} C{12},{13} {14},{15} {16},{17} C{18},{19} 4,{20} 3,{21} C1,{22} 9,{23} {16},{24} C{25},{26} {27},4 {0},3 Z",
                w*.5,w*.76,w-8,b*.10,w-4,b*.37,w-1,b*.66,w-9,b*.83,w*.85,b*.91,w*.69,b+3,w*.35,b+2,w*.18,b*.94,w*.05,b*.89,b*.75,b*.55,b*.35,b*.18,b*.10,w*.28,b*.025,w*.40));
            accent.Data = Geometry.Parse(String.Format(CultureInfo.InvariantCulture,
                "M15,{0} C8,{1} 5,{2} 6,{3} M{4},{5} C{6},{7} {8},{9} {10},{11} M{12},{13} C{14},{15} {16},{17} {18},{19}",
                b*.28,b*.34,b*.42,b*.46,w-19,b*.22,w-7,b*.29,w-5,b*.40,w-6,b*.47,w*.17,b*.96,w*.26,b+3,w*.39,b+4,w*.47,b+2));
            double cx = w*.28, cy = b+12, sx = cx-21, sy = cy+17;
            thoughts.Data = Geometry.Parse(String.Format(CultureInfo.InvariantCulture,
                "M{0},{1} C{0},{2} {3},{4} {5},{6} C{7},{8} {9},{1} {5},{10} C{11},{12} {13},{14} {0},{1} Z M{15},{16} C{17},{18} {19},{20} {21},{22} C{23},{16} {19},{24} {17},{25} C{15},{25} {26},{27} {15},{16} Z",
                cx-9,cy+1,cy-4,cx-2,cy-6,cx+4,cy-5,cx+10,cy-4,cx+9,cy+4,cx-1,cy+6,cx-8,cy+5,sx-5,sy,sx-4,sy-3,sx+1,sy-4,sx+5,sy-2,sx+5,sy+3,sy+3,sx-6,sy+1));
        }
        internal void Reveal() {
            if (quotaContent.Visibility == Visibility.Visible || balanceContent.Visibility == Visibility.Visible) { RefreshCountdown(DateTime.UtcNow); countdown.Start(); }
            transition++; BeginAnimation(OpacityProperty, null); Opacity = 0; Show();
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        }
        internal void HideImmediately() {
            countdown.Stop();
            transition++; BeginAnimation(OpacityProperty, null); Hide(); Opacity = 1;
        }
        internal void FadeAway() {
            countdown.Stop();
            int ticket = ++transition;
            if (!IsVisible) { HideImmediately(); return; }
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(180));
            fade.Completed += delegate { if (ticket == transition) HideImmediately(); };
            BeginAnimation(OpacityProperty, fade);
        }
    }
}
