using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TetoDesk {
    public sealed class BalanceWallet {
        public string Currency;
        public decimal Paid, Bonus;
        public string Amount(decimal amount) { return (Currency == "CNY" ? "¥" : Currency + " ") + Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture); }
    }
    public sealed class BalanceSnapshot {
        public readonly List<BalanceWallet> Wallets = new List<BalanceWallet>();
        public static BalanceSnapshot Read(object response) {
            if (response == null || J.S(response, "status") != "ready") throw new InvalidOperationException("请先在 DeepSeek Harness 中登录账号，再查询账户余额。");
            var result = new BalanceSnapshot();
            foreach (string key in new[] { "value", "bonusWallets" }) foreach (object entry in J.A(J.Get(response, key))) {
                string currency = J.S(entry, "currency"); decimal amount;
                if (currency.Length == 0 || !Decimal.TryParse(J.S(entry, "balance"), NumberStyles.Float, CultureInfo.InvariantCulture, out amount)) throw new InvalidOperationException("账户余额格式无法识别，请更新 Harness 后重试。");
                var wallet = result.Wallets.Find(w => w.Currency == currency);
                if (wallet == null) { wallet = new BalanceWallet { Currency = currency }; result.Wallets.Add(wallet); }
                if (key == "value") wallet.Paid += amount; else wallet.Bonus += amount;
            }
            if (result.Wallets.Count == 0) throw new InvalidOperationException("账户未返回余额信息。");
            return result;
        }
        public string TotalText { get { return String.Join(" / ", Wallets.Select(w => w.Amount(w.Paid + w.Bonus))); } }
        public string DetailText { get { return String.Join("\n", Wallets.Select(w => "充值 " + w.Amount(w.Paid) + " · 赠送 " + w.Amount(w.Bonus))); } }
    }
    public static class PricingPeriod {
        // DeepSeek published pricing, checked 2026-10-19. UTC+8 is independent of the PC timezone.
        public const string Source = "https://api-docs.deepseek.com/quick_start/pricing";
        public const string CalendarSource = "https://www.gov.cn/zhengce/content/202511/content_7047090.htm";
        private static bool Holiday(DateTime china) {
            string day = china.ToString("MMdd", CultureInfo.InvariantCulture);
            foreach (string range in new[] { "0101-0103", "0215-0223", "0404-0406", "0501-0505", "0619-0621", "0925-0927", "1001-1007" })
                if (String.CompareOrdinal(day, range.Substring(0, 4)) >= 0 && String.CompareOrdinal(day, range.Substring(5, 4)) <= 0) return true;
            return false;
        }
        // Null means the holiday calendar has not been confirmed for that year.
        public static bool? Peak(DateTime utc) {
            DateTime china = utc.ToUniversalTime().AddHours(8);
            if (china.DayOfWeek == DayOfWeek.Saturday || china.DayOfWeek == DayOfWeek.Sunday) return false;
            if (china.Year != 2026) return null;
            if (Holiday(china)) return false;
            double hour = china.TimeOfDay.TotalHours;
            return (hour >= 9 && hour < 12) || (hour >= 14 && hour < 18);
        }
        public static string Label(DateTime utc) { bool? peak = Peak(utc); return !peak.HasValue ? "峰谷时段待更新" : peak.Value ? "当前：峰期" : "当前：谷期"; }
        public static string Next(DateTime utc) {
            utc = utc.ToUniversalTime(); bool? current = Peak(utc); if (!current.HasValue) return "请更新节假日日历";
            DateTime minute = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc).AddMinutes(1);
            for (int i = 0; i < 14 * 24 * 60; i++, minute = minute.AddMinutes(1)) {
                bool? next = Peak(minute); if (!next.HasValue) return "后续节假日日历待更新";
                if (next != current) { TimeSpan wait = minute - utc; int mins = (int)Math.Ceiling(wait.TotalMinutes); return (mins >= 60 ? (mins / 60) + "小时" : "") + (mins % 60 > 0 ? (mins % 60) + "分钟" : "") + "后转" + (next.Value ? "峰期" : "谷期"); }
            }
            return "按北京时间 · 官方时段规则";
        }
    }
}
