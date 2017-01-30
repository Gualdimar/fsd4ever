using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     Represents an HTTP response to be sent to the client in response to a request.
    /// </summary>
    public class HttpResponse : MarshalByRefObject {
        /// <summary>
        ///     Represents an event that is triggered by an <see>HttpResponse</see> object.
        /// </summary>
        public delegate void ResponseEventHandler(object sender, ResponseEventArgs e);

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private long bytesSent;

        private bool isChunked;
        private bool isImmediate;
        private readonly Stream outputStream;

        private Stream responseContent;

        internal HttpResponse(HttpRequest request) {
            Request = request;
            outputStream = request.Client.Stream;
        }

        /// <summary>
        ///     Gets or sets a stream containing the content to send to the client.
        /// </summary>
        public Stream ResponseContent {
            get { return responseContent; }
            set {
                if (HeadersSent)
                    throw new InvalidOperationException("Response headers cannot be changed after they are sent");
                responseContent = value;
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the header has already been sent.
        /// </summary>
        public bool HeadersSent { get; private set; }

        /// <summary>
        ///     Gets the number of bytes of the response object have been sent so far.
        /// </summary>
        public long BytesSent {
            get {
                var stream = responseContent as ImmediateResponseStream;
                return stream?.BytesSent ?? bytesSent;
            }
        }

        /// <summary>
        ///     Gets the <see>HttpRequest</see> of which this object is a response to.
        /// </summary>
        public HttpRequest Request { get; }

        /// <summary>
        ///     Event that is triggered before the response content is sent.
        /// </summary>
        public event ResponseEventHandler SendingResponse;

        /// <summary>
        ///     Event that is triggered after the response content is sent.
        /// </summary>
        public event ResponseEventHandler SentResponse;

        private void BeginResponse() {
            var enc = Encoding.Default;
            if (enc.Equals(Encoding.UTF8))
                enc = Utf8;
            var writer = new StreamWriter(outputStream, enc) {
                NewLine = "\r\n"
            };

            SetHeader("Date", DateTime.Now.ToString("r", CultureInfo.InvariantCulture));
            SetHeader("Server", Request.Server.ServerName);
            if (responseContent != null)
                if (GetHeader("Content-Type") == null)
                    SetHeader("Content-Type", "application/octet-stream");

            writer.WriteLine("HTTP/" + Request.HttpVersion + " " + ResponseCode + " " +
                             StatusCodes.GetDescription(ResponseCode));

            foreach (string header in Headers) {
                if (Headers[header] == null)
                    continue;
                writer.WriteLine(header + ": " + Headers[header]);
            }

            writer.WriteLine();
            writer.Flush();

            HeadersSent = true;

            SendingResponse?.Invoke(this, new ResponseEventArgs(Request.Client, this, ContentLength));
        }

        /// <summary>
        ///     Begins an immediate response to the client. This is recommended only for streaming data to HTTP/1.0 clients.
        /// </summary>
        public void BeginImmediateResponse() {
            BeginResponse();
            isImmediate = true;
            responseContent = outputStream;
        }

        /// <summary>
        ///     Begins a chunked response and sets <see cref="ResponseContent" /> to a chunked stream to which data can be written
        ///     and sent immediately to the client.
        /// </summary>
        /// <returns>
        ///     True if a chunked transmission has begun; otherwise false if ResponseContent will write directly to the
        ///     response and disconnect at the end.
        /// </returns>
        public bool BeginChunkedOutput() {
            if (isChunked)
                return true;

            if (Request.HttpVersion == "1.0") {
                BeginImmediateResponse();
                return false;
            }

            isChunked = true;

            if (Request.Ranges != null && Request.Ranges.Length != 0)
                responseCode = "416";

            SetHeader("Transfer-Encoding", "chunked");

            BeginResponse();

            responseContent = new ChunkedStream(outputStream);
            return true;
        }

        private static long GetRangeLen(ByteRange range, Stream stream) {
            if (!stream.CanSeek)
                return -1;

            if (range.Last > stream.Length)
                return -1;
            if (range.First > stream.Length)
                return -1;

            if (range.Last == -1)
                return stream.Length - range.First;

            if (range.First == -1)
                return range.Last;

            return range.Last - range.First + 1;
        }

        internal void WriteOutput() {
            //Finished chunked data, write footer.
            if (isChunked || isImmediate) {
                if (isChunked && Request.HttpVersion != "1.0") {
                    var data = Encoding.UTF8.GetBytes("0;\r\n\r\n");
                    outputStream.Write(data, offset: 0, count: data.Length);
                    outputStream.Flush();
                }
                SentResponse?.Invoke(this, new ResponseEventArgs(Request.Client, this));
                return;
            }

            var rangesValid = false;
            long totalLen = 0;
            if (responseContent != null && Request.Ranges != null && Request.Ranges.Length != 0) {
                if (responseCode == null || responseCode[index: 0] == '2')
                    responseCode = "206"; //Partial Content

                foreach (var range in Request.Ranges) {
                    var len = GetRangeLen(range, responseContent);
                    if (len == -1)
                        continue;
                    rangesValid = true;
                    totalLen += len;
                }

                if (!rangesValid && responseCode != null && responseCode == "206")
                    responseCode = "416"; //Requested range not satisfiable
            }

            if (responseContent != null)
                ContentLength = rangesValid ? totalLen : responseContent.Length;

            BeginResponse();

            if (Request.Method == "HEAD") {
                responseContent.Close();
                return;
            }

            if (responseContent == null)
                return;
            {
                try {
                    byte[] buffer;
                    if (responseContent.CanSeek)
                        buffer = responseContent.Length < 1024 * 4 ? new byte[responseContent.Length] : new byte[1024 * 4];
                    else
                        buffer = new byte[1024 * 4];

                    if (rangesValid) {
                        foreach (var range in Request.Ranges) {
                            var len = GetRangeLen(range, responseContent);
                            if (len == -1)
                                continue;

                            if (range.Last == -1)
                                responseContent.Seek(range.First, SeekOrigin.Begin);
                            else if (range.First == -1)
                                responseContent.Seek(-range.Last, SeekOrigin.End);
                            else
                                responseContent.Seek(range.First, SeekOrigin.Begin);

                            int bufLen;
                            while (
                            (bufLen =
                                responseContent.Read(buffer,
                                                     offset: 0,
                                                     count: (int)len < buffer.Length ? (int)len : buffer.Length)) != 0) {
                                outputStream.Write(buffer, offset: 0, count: bufLen);
                                len -= bufLen;
                            }
                        }
                    }
                    else {
                        responseContent.Seek(offset: 0, origin: SeekOrigin.Begin);
                        try {
                            int len;
                            while ((len = responseContent.Read(buffer, offset: 0, count: buffer.Length)) != 0) {
                                outputStream.Write(buffer, offset: 0, count: len);
                                bytesSent += len;
                            }
                        }
                        catch (IOException) {
                            Request.Client.Disconnect();
                        }
                    }
                }
                finally {
                    SentResponse?.Invoke(this, new ResponseEventArgs(Request.Client, this));
                    responseContent.Close();
                }
            }
        }

        #region Headers

        /// <summary>
        ///     Returns the value of the specified header.
        /// </summary>
        /// <param name="name">The name of the header value to be returned.</param>
        /// <returns>The value of the specified header.</returns>
        public string GetHeader(string name) {
            return Headers[name];
        }

        /// <summary>
        ///     Sets the value of a specified header.
        /// </summary>
        /// <param name="name">The name of the header to which to assign a value.</param>
        /// <param name="value">The value to assign to the header.</param>
        public void SetHeader(string name, string value) {
            if (HeadersSent)
                throw new InvalidOperationException("Response headers cannot be changed after they are sent");

            Headers[name] = value;
        }

        private NameValueCollection Headers { get; } = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

        private string responseCode;

        /// <summary>
        ///     Gets or sets the HTTP status code of the response.
        /// </summary>
        public string ResponseCode {
            get {
                if (responseCode != null)
                    return responseCode;
                return Request.StatusCode;
            }
            set {
                if (HeadersSent)
                    throw new InvalidOperationException("Response code cannot be changed after they are sent");
                responseCode = value;
            }
        }

        /// <summary>
        ///     Gets or sets the MIME content-type of the response content.
        /// </summary>
        public string ContentType {
            get { return GetHeader("Content-Type"); }
            set { SetHeader("Content-Type", value); }
        }

        /// <summary>
        ///     Gets or sets the length of the response content.
        /// </summary>
        public long ContentLength {
            get {
                try {
                    var lengthString = GetHeader("Content-Length");
                    if (lengthString == null)
                        return -1;
                    return long.Parse(lengthString, NumberStyles.Number, CultureInfo.InvariantCulture);
                }
                catch (FormatException) {
                    return -1;
                }
            }
            set { SetHeader("Content-Length", value.ToString()); }
        }

        #endregion
    }
}
