#if DEBUG
//#define DUMP
#endif

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     Represents a client connection in an HTTP/1.x transaction.
    /// </summary>
    public class HttpClient : MarshalByRefObject, IDisposable {
        private readonly byte[] buffer = new byte[512];
        private double idleTime;
        private bool preventTimeout;

        private Thread processingThread;
        private HttpRequest request;

        internal readonly HttpServer Server;
        private readonly Socket socket;
        internal readonly NetworkStream Stream;
        private StringBuilder textBuf;

        internal HttpClient(Socket socket, HttpServer server) {
            Server = server;
            this.socket = socket;
            var remoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;
            IsConnected = true;
            RemoteAddress = remoteEndPoint.Address.ToString();
            RemotePort = remoteEndPoint.Port;

            server.Stopping += server_Stopping;

            Stream = new NetworkStream(socket, ownsSocket: true);

            try {
                Stream.BeginRead(buffer, offset: 0, size: buffer.Length, callback: OnReceiveData, state: this);
            }
            catch (IOException) {
                Disconnect();
                throw;
            }
            server.OneHertzTick += server_OneHertzTick;
        }

        /// <summary>
        ///     Gets a value indicating whether the client is currently connected.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        ///     Gets the IP address of the client.
        /// </summary>
        public string RemoteAddress { get; }

        /// <summary>
        ///     Gets the port through which the client is connected.
        /// </summary>
        public int RemotePort { get; }

        #region IDisposable Members

        /// <summary>
        ///     Disconnects the client.
        /// </summary>
        public void Dispose() { Disconnect(); }

        #endregion

        /// <summary>
        ///     Disposes the client if it hasn't already been disposed.
        /// </summary>
        ~HttpClient() { Disconnect(); }

        /// <summary>
        ///     Returns a hash code generated from the client's IP address.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() { return RemoteAddress.GetHashCode(); }

        /// <summary>
        ///     Returns a value indicating whether this client is the same instance as another.
        /// </summary>
        /// <param name="obj">Another <see>HttpClient</see> to which to compare this object.</param>
        /// <returns>True if this is the same instance of the <see>HttpClient</see>, otherwise false.</returns>
        public override bool Equals(object obj) { return this == obj; }

        /// <summary>
        ///     Occurs when a client is disconnected.
        /// </summary>
        public event EventHandler Disconnected;

        private static int FindByte(byte[] buf, int offset, int count, byte b) {
            for (var i = offset; i < buf.Length && count > 0; i++, count--)
                if (buf[i] == b)
                    return i;
            return -1;
        }

        private void OnReceiveData(IAsyncResult ar) {
            if (processingThread == null)
                processingThread = Thread.CurrentThread;
            lock (socket) {
                try {
                    idleTime = 0;
                    if (!IsConnected)
                        return;

                    var dataLen = Stream.EndRead(ar);

                    if (dataLen < 1) {
                        Disconnect();
                        return;
                    }

                    if (textBuf?.Length > 0x8000) {
                        Disconnect();
                        return;
                    }

                    for (var bufPos = 0; bufPos < dataLen; bufPos++) {
                        if (request == null) {
                            request = new HttpRequest(this);
                            textBuf = new StringBuilder(buffer.Length);
                        }

                        if (request.Mode == HttpRequest.DataMode.Text) {
                            var len = FindByte(buffer, bufPos, dataLen, b: 10);
                            if (len != -1) {
                                textBuf.Append(Encoding.Default.GetString(buffer, bufPos, len + 1 - bufPos));
                                request.ProcessLine(textBuf.ToString().Trim('\r', '\n'));
                                textBuf.Length = 0;
                                bufPos = len;
                            }
                            else {
                                textBuf.Append(Encoding.Default.GetString(buffer, bufPos, dataLen - bufPos));
                                bufPos = dataLen;
                            }
                        }

                        if (request.Mode == HttpRequest.DataMode.Binary) {
                            request.ProcessData(buffer, bufPos, dataLen - bufPos);
                            bufPos = dataLen;
                        }

                        if (request.IsRequestFinished) {
                            preventTimeout = true;
                            var forceDisconnect = false;
                            try {
                                Server.OnRequestReceived(this, request);
                            }
                            catch (HttpRequestException e) {
                                request.Response.ResponseCode = e.ResponseCode;
                                if (e.ResponseCode == "500")
                                    Server.Log.WriteLine(e.ToString());
                                forceDisconnect = true;
                            }
                            request.SendResponse();
                            var modeTemp = request.ConnectionMode;

                            var requestVer = request.HttpVersion;

                            request.Dispose();
                            request = null;
                            idleTime = 0;
                            preventTimeout = false;
                            if (modeTemp != ConnectionMode.Close && !forceDisconnect && requestVer != "1.0")
                                continue;
                            Disconnect();
                            return;
                        }
                    }

                    if (IsConnected)
                        Stream.BeginRead(buffer, offset: 0, size: buffer.Length, callback: OnReceiveData, state: this);
                }
                catch (SocketException) {
                    Disconnect();
                }
                catch (IOException e) {
                    Disconnect();
                    if (!(e.InnerException is SocketException)) {
#if !DEBUG
						server.Log.WriteLine("Error: " + e.ToString());
#else
                        throw;
#endif
                    }
                }
                catch (ThreadAbortException) {
                    Disconnect();
                }
#if !DEBUG
				catch(Exception e)
				{
					server.Log.WriteLine("Error: " + e.ToString());
					Disconnect();
				}
#endif
            }
        }

        internal void Disconnect() {
            if (IsConnected)
                lock (socket) {
                    Disconnected?.Invoke(this, e: null);
                    request?.Dispose();
                    Stream.Close();
                    socket.Close();
                    IsConnected = false;
                    GC.SuppressFinalize(this);
                }
        }

        private void server_Stopping(object sender, EventArgs e) {
            processingThread?.Abort();
        }

        private void server_OneHertzTick(object sender, EventArgs e) {
            idleTime += 1000;
            if (idleTime >= Server.Timeout && !preventTimeout)
                Disconnect();
        }
    }
}
