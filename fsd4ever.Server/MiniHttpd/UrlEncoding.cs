using System;
using System.Text;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     Provides somewhat lenient URL encoding suited for Unicode Uris.
    /// </summary>
    public static class UrlEncoding {
        private static readonly string[] UrlEncStrings = InitUrlStrings();

        private static string[] InitUrlStrings() {
            var urlEncStrings = new string[256];
            for (var i = 0; i < 255; i++)
                urlEncStrings[i] = "%" + i.ToString("X2");
            return urlEncStrings;
        }

        /// <summary>
        ///     Url encodes a string, but allow unicode characters to pass unencoded.
        /// </summary>
        /// <param name="value">The string to encode.</param>
        /// <returns>The Url encoded string.</returns>
        public static string Encode(string value) {
            if (value == null)
                return null;

            var ret = new StringBuilder(value.Length);

            for (var i = 0; i < value.Length; i++) {
                var ch = value[i];
                if (ch == ' ')
                    ret.Append(value: '+');
                else if (!IsSafe(ch))
                    ret.Append(UrlEncStrings[ch]);
                else
                    ret.Append(ch);
            }

            return ret.ToString();
        }

        /// <summary>
        ///     Decodes a Url using System.Uri's Unescape method.
        /// </summary>
        /// <param name="value">The string to decode.</param>
        /// <returns>The Url decoded string.</returns>
        public static string Decode(string value) {
            if (value == null)
                return null;

            return UnescapeUri(value);
        }

        private static string UnescapeUri(string str) { return Uri.UnescapeDataString(str.Replace("+", "%20")); }

        private static bool IsSafe(char ch) {
            if (char.IsLetterOrDigit(ch))
                return true;

            switch (ch) {
                case '\'':
                case '(':
                case ')':
                case '[':
                case ']':
                case '*':
                case '-':
                case '.':
                case '!':
                case '_':
                    return true;
            }

            if (ch > 255)
                return true;

            return false;
        }
    }
}
