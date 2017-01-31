using System;
using System.Globalization;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     Represents a byte range to be used by the HTTP/1.1 protocol.
    /// </summary>
    public struct ByteRange {
        /// <summary>
        ///     Creates a new <see cref="ByteRange" /> from the specified string.
        /// </summary>
        /// <param name="parse">A byte-range string given by an HTTP/1.1 header value.</param>
        public ByteRange(string parse) {
            if (!parse.StartsWith("bytes="))
                throw new FormatException();

            parse = parse.Substring("bytes=".Length);

            var ranges = parse.Split('-');
            if (ranges.Length != 2)
                throw new FormatException();

            var a = ranges[0].Trim();
            var b = ranges[1].Trim();

            var aNull = false;
            var bNull = false;

            First = 0;
            Last = 0;

            try {
                if (a.Length == 0 && b.Length == 0)
                    throw new FormatException();
                if (a.Length != 0 && b.Length == 0) {
                    First = long.Parse(a, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    Last = -1;
                    bNull = true;
                }
                else if (a.Length == 0 && b.Length != 0) {
                    First = -1;
                    Last = long.Parse(a, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    aNull = true;
                }
                else if (a.Length != 0 && b.Length != 0) {
                    First = long.Parse(a, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    Last = long.Parse(a, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
            }
            catch {
                throw new FormatException();
            }

            if (!aNull && First < 0)
                throw new FormatException();
            if (!bNull && Last < 0)
                throw new FormatException();
            if (!aNull && !bNull && First > Last)
                throw new FormatException();
        }

        /// <summary>
        ///     Creates a new <see cref="ByteRange" /> from the specified values.
        /// </summary>
        /// <param name="first">The index of the first byte.</param>
        /// <param name="last">The index of the last byte.</param>
        public ByteRange(long first, long last) {
            First = first;
            Last = last;
        }

        /// <summary>
        ///     Gets the index of the first byte.
        /// </summary>
        public long First { get; }

        /// <summary>
        ///     Gets the index of the last byte.
        /// </summary>
        public long Last { get; }
    }
}
