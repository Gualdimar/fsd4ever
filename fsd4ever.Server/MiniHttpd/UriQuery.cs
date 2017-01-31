using System;
using System.Collections.Specialized;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     Represents a URI query parsed in name/value pairs.
    /// </summary>
    public sealed class UriQuery : NameValueCollection {
        /// <summary>
        ///     Parse a query from a given Uri.
        /// </summary>
        /// <param name="uri">The URI to parse from.</param>
        public UriQuery(Uri uri) : this(uri.Query.TrimStart('?'), urlEncoded: true) { }

        /// <summary>
        ///     Parse a query from a URL encoded query string.
        /// </summary>
        /// <param name="query">The query string to parse from.</param>
        public UriQuery(string query) : this(query, urlEncoded: true) { }

        /// <summary>
        ///     Parse a query from a given string.
        /// </summary>
        /// <param name="query">The query string to parse from.</param>
        /// <param name="urlEncoded">A value indicating whether the string is URL encoded or not.</param>
        public UriQuery(string query, bool urlEncoded) {
            for (var i = 0; i < query.Length; i++) {
                var start = i;
                var equalIndex = -1;
                while (i < query.Length) {
                    if (query[i] == '=') {
                        if (equalIndex < 0)
                            equalIndex = i;
                    }
                    else if (query[i] == '&') {
                        break;
                    }
                    i++;
                }

                string name;
                string value;

                if (equalIndex < 0) {
                    name = query.Substring(start, i - start);
                    value = string.Empty;
                }
                else {
                    name = query.Substring(start, equalIndex - start);
                    value = query.Substring(equalIndex + 1, i - equalIndex - 1);
                }

                if (urlEncoded)
                    Add(UrlEncoding.Decode(name), UrlEncoding.Decode(value));
                else
                    Add(name, value);

                if (i == query.Length - 1 && query[i] == '&')
                    Add(name: null, value: string.Empty);
            }
        }
    }
}
