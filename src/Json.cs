using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace TetoDesk {
    public static class J {
        public static Dictionary<string, object> O(params object[] values) {
            var result = new Dictionary<string, object>();
            for (int i = 0; i < values.Length; i += 2) result[(string)values[i]] = values[i + 1];
            return result;
        }
        public static object Get(object value, string key) {
            var map = value as Dictionary<string, object>;
            object answer;
            return map != null && map.TryGetValue(key, out answer) ? answer : null;
        }
        public static string S(object value, string key) { return Str(Get(value, key)); }
        public static string Str(object value) { return value == null ? "" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture); }
        public static int I(object value, string key, int fallback) { int n; return Int32.TryParse(S(value, key), out n) ? n : fallback; }
        public static bool B(object value, string key) { return Get(value, key) is bool && (bool)Get(value, key); }
        public static object[] A(object value) {
            var a = value as object[]; if (a != null) return a;
            var list = value as ArrayList; return list == null ? new object[0] : list.ToArray();
        }
        public static string Serialize(object value) { return Serializer().Serialize(value); }
        public static object Parse(string text) { return Serializer().DeserializeObject(text); }
        private static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 256 * 1024 * 1024, RecursionLimit = 128 }; }
        public static void Save(string path, object value) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".new";
            File.WriteAllText(temporary, Serialize(value), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
    }
}
