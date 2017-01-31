using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     Represents an HTTP request received from a client.
    /// </summary>
    public class HttpRequest : MarshalByRefObject, IDisposable {
        internal HttpRequest(HttpClient client) {
            Mode = DataMode.Text;
            state = ProcessingState.RequestLine;
            ConnectionMode = ConnectionMode.KeepAlive;
            Client = client;
            Response = new HttpResponse(this);
        }

        #region IDisposable Members

        /// <summary>
        ///     Disposes the request.
        /// </summary>
        public void Dispose() {
            PostData.Close();
        }

        #endregion

        #region State

        internal enum DataMode {
            /// <summary>
            ///     Text mode transmission
            /// </summary>
            Text,

            /// <summary>
            ///     Binary mode transmission
            /// </summary>
            Binary
        }

        internal DataMode Mode { get; private set; }

        internal bool IsRequestFinished { get; private set; }

        private bool isRequestError;

        /// <summary>
        ///     Gets the associated <see cref="Client" />.
        /// </summary>
        public HttpClient Client { get; }

        /// <summary>
        ///     Gets the server to which this request was sent.
        /// </summary>
        public HttpServer Server => Client?.Server;

        /// <summary>
        ///     Gets a value indicating whether this request is a syntactically valid HTTP/1.x reuest.
        /// </summary>
        public bool IsValidRequest => !isRequestError;

        /// <summary>
        ///     Gets the status code of the request.
        /// </summary>
        public string StatusCode { get; private set; } = "200";

        /// <summary>
        ///     Gets or sets the error message, if any.
        /// </summary>
        public string ErrorMessage { get; set; }

        private void RequestError(string statusCode, string message) {
            ConnectionMode = ConnectionMode.Close;
            IsRequestFinished = true;
            StatusCode = statusCode;
            ErrorMessage = message;
            isRequestError = true;
        }

        #endregion

        #region Processing

        private static readonly string[] HttpDateTimeFormats = {
            "ddd, d MMM yyyy H:m:s GMT",
            "dddd, d-MMM-yy H:m:s GMT",
            "ddd MMM d H:mm:s yy"
        };

        private static DateTime ParseHttpTime(string str) {
            DateTime dt;
            try {
                dt = DateTime.ParseExact(str,
                                         HttpDateTimeFormats,
                                         DateTimeFormatInfo.InvariantInfo,
                                         DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal);
            }
            catch (FormatException) {
                dt = DateTime.Parse(str, CultureInfo.InvariantCulture);
            }
            return dt;
        }

        private enum ProcessingState {
            RequestLine = 0,
            Headers
        }

        private ProcessingState state;

        private string requestUri;

        private void PostProcessHeaders() {
            if (HttpVersion == "1.1" && Host == null) {
                RequestError("400", "HTTP/1.1 requests must include Host header");
                return;
            }

            if (Client.Server.RequireAuthentication && Server.Authenticator.Authenticate(Username, Password) == false) {
                Response.SetHeader("WWW-Authenticate", "Basic realm=\"" + Client.Server.AuthenticateRealm + "\"");
                RequestError("401", StatusCodes.GetDescription("401"));
                return;
            }

            try {
                // Try parsing a relative URI
                //uri = new Uri(client.server.ServerUri, requestUri);
                Uri = Client.Server.GetRelUri(requestUri);
            }
            catch {
                try {
                    // Try parsing an absolute URI
                    //uri = new Uri(requestUri);
                    Uri = Client.Server.GetAbsUri(requestUri);
                }
                catch (UriFormatException) {
                    RequestError("400", "Invalid URI");
                    return;
                }
                catch (IndexOutOfRangeException) // System.Uri in .NET 1.1 throws this exception in certain cases
                {
                    RequestError("400", "Invalid URI");
                    return;
                }
            }

            if (Host != null)
                Uri = Client.Server.GetHostUri(Host, requestUri);

            // Try to determine the time difference between the client and this computer; adjust ifModifiedSince and ifUnmodifiedSince accordingly
            if (Date != DateTime.MinValue) {
                if (IfModifiedSince != DateTime.MinValue)
                    IfModifiedSince = IfModifiedSince.Add(DateTime.UtcNow.Subtract(Date));
                if (IfUnmodifiedSince != DateTime.MinValue)
                    IfUnmodifiedSince = IfUnmodifiedSince.Add(DateTime.UtcNow.Subtract(Date));
            }

            if (Method == "POST") {
                if (ContentLength == long.MinValue) {
                    RequestError("411", StatusCodes.GetDescription("411"));
                    return;
                }
                Mode = DataMode.Binary;
            }
            else {
                IsRequestFinished = true;
            }
        }

        internal void ProcessLine(string line) {
            switch (state) {
                case ProcessingState.RequestLine: {
                    var protocol = line.Split(' ');
                    if (protocol.Length != 3) {
                        RequestError("400", "Invalid protocol string");
                        return;
                    }

                    switch (protocol[0]) {
                        case "GET":
                        case "POST":
                        case "HEAD":
                            Method = protocol[0];
                            break;
                        //case "PUT":
                        //case "DELETE":
                        //case "OPTIONS":
                        //case "TRACE":
                        default:
                            RequestError("501", StatusCodes.GetDescription("501"));
                            return;
                    }

                    if (protocol[1].Length > 2500) {
                        RequestError("414", StatusCodes.GetDescription("414"));
                        return;
                    }
                    requestUri = protocol[1];

                    if (!protocol[2].StartsWith("HTTP/") || !(protocol[2].Length > "HTTP/".Length)) {
                        RequestError("400", "Invalid protocol string");
                        return;
                    }

                    HttpVersion = protocol[2].Substring("HTTP/".Length);

                    Date = DateTime.Now;

                    ConnectionMode = HttpVersion == "1.0" ? ConnectionMode.Close : ConnectionMode.KeepAlive;

                    state = ProcessingState.Headers;
                    break;
                }
                case ProcessingState.Headers: {
                    if (Headers.Count > MaxHeaderLines) {
                        RequestError("400", "Maximum header line count exceeded");
                        return;
                    }

                    if (line.Length == 0) {
                        PostProcessHeaders();
                        return;
                    }

                    var colonIndex = line.IndexOf(":", StringComparison.Ordinal);
                    if (colonIndex <= 1)
                        return;
                    var val = line.Substring(colonIndex + 1).Trim();
                    var name = line.Substring(startIndex: 0, length: colonIndex);

                    try {
                        Headers.Add(name, val);
                    }
                    catch {
                        // ignored
                    }

                    switch (name.ToLower(CultureInfo.InvariantCulture)) {
                        case "host":
                            Host = val;
                            break;
                        case "authorization": {
                            if (val.Length < 6)
                                break;

                            var encoded = val.Substring(startIndex: 6, length: val.Length - 6);
                            byte[] byteAuth;
                            try {
                                byteAuth = Convert.FromBase64String(encoded);
                            }
                            catch (FormatException) {
                                break;
                            }

                            var strings = Encoding.UTF8.GetString(byteAuth).Split(':');
                            if (strings.Length != 2)
                                break;

                            Username = strings[0];
                            Password = strings[1];

                            break;
                        }
                        case "content-type":
                            ContentType = val;
                            break;
                        case "content-length":
                            try {
                                ContentLength = long.Parse(val, NumberStyles.Integer, CultureInfo.InvariantCulture);
                            }
                            catch (FormatException) { }
                            if (ContentLength > Client.Server.MaxPostLength)
                                RequestError("413", StatusCodes.GetDescription("413"));
                            else if (ContentLength < 0)
                                RequestError("400", StatusCodes.GetDescription("400"));
                            break;
                        case "accept":
                            Accept = val;
                            break;
                        case "accept-language":
                            AcceptLanguage = val;
                            break;
                        case "user-agent":
                            UserAgent = val;
                            break;
                        case "connection":
                            ConnectionMode = string.Compare(val, "close", ignoreCase: true, culture: CultureInfo.InvariantCulture) ==
                                             0 ? ConnectionMode.Close : ConnectionMode.KeepAlive;
                            break;
                        case "if-modified-since":
                            try {
                                IfModifiedSince = ParseHttpTime(val);
                            }
                            catch (FormatException) { }
                            break;
                        case "if-unmodified-since":
                            try {
                                IfUnmodifiedSince = ParseHttpTime(val);
                            }
                            catch (FormatException) { }
                            break;
                        case "range":
                            try {
                                var rangeStrings = val.Split(',');
                                Ranges = new ByteRange[rangeStrings.Length];
                                for (var i = 0; i < rangeStrings.Length; i++)
                                    Ranges[i] = new ByteRange(rangeStrings[i]);
                            }
                            catch (FormatException) {
                                Ranges = null;
                            }
                            break;
                    }
                    break;
                }
            }
        }

        #endregion

        #region POST data processing

        private long dataRemaining = -1;

        internal void ProcessData(byte[] buffer, int offset, int length) {
            if (dataRemaining == -1) {
                dataRemaining = ContentLength;

                // Trim the leading LF.
                offset++;
                length--;
            }
            if (dataRemaining == 0) {
                IsRequestFinished = true;
                PostData.Seek(offset: 0, loc: SeekOrigin.Begin);
                return;
            }

            length = (int)(dataRemaining < length ? dataRemaining : length);
            if (PostData.Length + length >= Server.MaxPostLength) {
                IsRequestFinished = true;
                length = (int)(Server.MaxPostLength - PostData.Length);
            }

            PostData.Write(buffer, offset, length);
            dataRemaining -= length;
            if (dataRemaining <= 0) {
                IsRequestFinished = true;
                PostData.Seek(offset: 0, loc: SeekOrigin.Begin);
            }
        }

        /// <summary>
        ///     Returns the POST data received from the client.
        /// </summary>
        public MemoryStream PostData { get; } = new MemoryStream();

        #endregion

        #region Response

        /// <summary>
        ///     Gets the collection of HTTP headers received from the client.
        /// </summary>
        public NameValueCollection Headers { get; } = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

        internal void SendResponse() {
            if (Response.ResponseContent == null) {
                //Default page
                var stream = new MemoryStream(capacity: 512);
                var writer = new StreamWriter(stream);

                //string message = response.ResponseCode + " " + StatusCodes.GetDescription(response.ResponseCode);
                var message = Response.ResponseCode + " " +
                              (ErrorMessage ?? StatusCodes.GetDescription(Response.ResponseCode));
                writer.WriteLine("<html><head><title>" + message + "</title></head>");
                writer.WriteLine("<body><h2>" + message + "</h2>");
                if (ErrorMessage != null)
                    writer.WriteLine(ErrorMessage);
                writer.WriteLine("<hr>" + Client.Server.ServerName);
                writer.WriteLine("</body></html>");

                writer.Flush();
                Response.ContentType = ContentTypes.GetExtensionType(".html");
                Response.ResponseContent = stream;
            }

            Response.WriteOutput();
        }

        /// <summary>
        ///     Gets the <see cref="HttpResponse" /> to this request.
        /// </summary>
        public HttpResponse Response { get; }

        #endregion

        #region Headers

        /// <summary>
        ///     Gets or sets the maximum allowed headers per each request.
        /// </summary>
        public static int MaxHeaderLines { get; set; } = 30;

        /// <summary>
        ///     Gets the <see cref="ConnectionMode" /> of the request.
        /// </summary>
        public ConnectionMode ConnectionMode { get; private set; }

        /// <summary>
        ///     Gets the HTTP <see cref="Method" /> of the request.
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        ///     Gets the <see cref="Uri" /> requested by the client.
        /// </summary>
        public Uri Uri { get; private set; }

        private NameValueCollection query;

        /// <summary>
        ///     Gets the parsed URI queries.
        /// </summary>
        public NameValueCollection Query {
            get {
                if (query == null)
                    query = new UriQuery(Uri);

                return query;
            }
        }

        /// <summary>
        ///     Gets the HTTP version of the request.
        /// </summary>
        public string HttpVersion { get; private set; } = "1.1";

        /// <summary>
        ///     Gets the time the request was received, as noted by the client.
        /// </summary>
        public DateTime Date { get; private set; } = DateTime.MinValue;

        /// <summary>
        ///     Gets the host requested by the client.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        ///     Gets the MIME content-type of the POST data of the request.
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        ///     Gets the length of the POST data in bytes.
        /// </summary>
        public long ContentLength { get; private set; }

        /// <summary>
        ///     Gets a list of MIME types accepted by the client.
        /// </summary>
        public string Accept { get; private set; }

        /// <summary>
        ///     Gets the list of languages accepted by the client.
        /// </summary>
        public string AcceptLanguage { get; private set; }

        /// <summary>
        ///     Gets the client software used by the client.
        /// </summary>
        public string UserAgent { get; private set; }

        /// <summary>
        ///     Gets the time to which the request should be cancelled if the requested resource has not been modified since.
        /// </summary>
        public DateTime IfModifiedSince { get; private set; } = DateTime.MinValue;

        /// <summary>
        ///     Gets the time to which the request should be cancelled if the requested resource has been modified since.
        /// </summary>
        public DateTime IfUnmodifiedSince { get; private set; } = DateTime.MinValue;

        /// <summary>
        ///     Gets the requested response content ranges.
        /// </summary>
        public ByteRange[] Ranges { get; private set; }

        /// <summary>
        ///     Gets a value specifying the protocol (HTTP or HTTPS).
        /// </summary>
        public HttpProtocol Protocol { get; } = HttpProtocol.Http;

        /// <summary>
        ///     Gets the client's username specified in the request.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        ///     Gets the client's password specified in the request.
        /// </summary>
        public string Password { get; private set; }

        #endregion
    }

    /// <summary>
    ///     Defines connection mode options
    /// </summary>
    public enum ConnectionMode {
        /// <summary>
        ///     Persist the connection after the response has been sent to the client.
        /// </summary>
        KeepAlive,

        /// <summary>
        ///     Disconnect the client after the response has been sent.
        /// </summary>
        Close
    }

    /// <summary>
    ///     Defines available HTTP protocols.
    /// </summary>
    public enum HttpProtocol {
        /// <summary>
        ///     Normal HTTP.
        /// </summary>
        Http,

        /// <summary>
        ///     HTTP with secure extensions.
        /// </summary>
        Https
    }
}
