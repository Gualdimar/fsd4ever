using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     An HTTP listener.
    /// </summary>
    public class HttpServer : MarshalByRefObject, IDisposable {
        #region Constructors

        /// <summary>
        ///     Creates an <see cref="HttpServer" /> on the default port and address.
        /// </summary>
        public HttpServer() : this(port: 80) { }

        /// <summary>
        ///     Creates an <see cref="HttpServer" /> on the specified port and default address.
        /// </summary>
        /// <param name="port">An available port between 1 and 65535. Specify 0 to use any open port.</param>
        public HttpServer(int port) : this(IPAddress.Any, port) { }

        /// <summary>
        ///     Creates an <see cref="HttpServer" /> on the specified port and address.
        /// </summary>
        /// <param name="localAddress">An <see cref="IPAddress" /> on which to listen for HTTP requests.</param>
        /// <param name="port">An available port between 1 and 65535. Specify 0 to use any open port.</param>
        public HttpServer(IPAddress localAddress, int port) {
            this.port = port;
            this.localAddress = localAddress;

            ServerUri =
                new Uri("http://" + Dns.GetHostName() +
                        (port != 80 ? ":" + port.ToString(CultureInfo.InvariantCulture) : ""));

            var name = Assembly.GetExecutingAssembly().GetName();
            ServerName = name.Name + "/" + name.Version;

            idleTimer = new Timer(TimerCallback, state: null, dueTime: 0, period: 1000);

            try {
                Authenticator = new BasicAuthenticator();
            }
            catch (NotImplementedException) { }
            catch (MemberAccessException) { }
        }

        /// <summary>
        ///     Disposes the server if it hasn't already been disposed.
        /// </summary>
        ~HttpServer() {
            Dispose();
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        ///     Occurs when the server is disposed.
        /// </summary>
        public event EventHandler Disposed;

        private bool isDisposed;

        /// <summary>
        ///     Shuts down and disposes the server.
        /// </summary>
        public virtual void Dispose() {
            Stop();

            if (isDisposed)
                return;
            isDisposed = true;

            idleTimer.Dispose();

            Disposed?.Invoke(this, e: null);
        }

        #endregion

        #region Server Settings

        private int port;

        /// <summary>
        ///     Gets or sets the port on which to listen to HTTP requests. Specify 0 to use any open port.
        /// </summary>
        public int Port {
            get { return port; }
            set {
                if (IsRunning)
                    throw new InvalidOperationException("Port cannot be changed while the server is running.");
                port = value;

                var uri = new UriBuilder(ServerUri) {
                    Port = port
                };
                ServerUri = uri.Uri;
            }
        }

        /// <summary>
        ///     Gets or sets the server's host name.
        /// </summary>
        public string HostName {
            get { return ServerUri.Host; }
            set {
                var uri = new UriBuilder(ServerUri) {
                    Host = value
                };
                ServerUri = uri.Uri;
            }
        }

        private IPAddress localAddress;

        /// <summary>
        ///     Gets or sets the IP address on which to listen to HTTP requests.
        /// </summary>
        public IPAddress LocalAddress {
            get { return localAddress; }
            set {
                if (IsRunning)
                    return;
                localAddress = value;
            }
        }

        /// <summary>
        ///     Gets the highest HTTP version recognized by the server.
        /// </summary>
        public static string HttpVersion => "1.1";

        /// <summary>
        ///     Gets or sets the name of the server.
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        ///     Gets the thread on which the listener is operating.
        /// </summary>
        public Thread ListenerThread { get; private set; }

        /// <summary>
        ///     Occurs when the server's <see cref="Uri" /> changes.
        /// </summary>
        public event EventHandler ServerUriChanged;

        private Uri serverUri;

        /// <summary>
        ///     Gets or sets the server's <see cref="Uri" />.
        /// </summary>
        public Uri ServerUri {
            get { return serverUri; }
            set {
                serverUri = value;
                relUriCache.Clear();
                ServerUriChanged?.Invoke(this, e: null);
            }
        }

        /// <summary>
        ///     Gets or sets the time, in milliseconds, of the time after which a client is idle for that the client should be
        ///     disconnected.
        /// </summary>
        public double Timeout { get; set; } = 100000;

        private int uriCacheMax = 1000;

        /// <summary>
        ///     Gets or sets the maximum size of the URI cache.
        /// </summary>
        public int UriCacheMax {
            get { return uriCacheMax; }
            set {
                uriCacheMax = value;
                if (absUriCache.Count > value)
                    lock (absUriCache) {
                        absUriCache.Clear();
                    }
                if (relUriCache.Count > value)
                    relUriCache.Clear();
                if (uriHostsCount <= value)
                    return;
                uriHostsCount = 0;
                uriHosts.Clear();
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether the server should log HTTP requests.
        /// </summary>
        public bool LogRequests { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the server should log client connections and disconnections.
        /// </summary>
        public bool LogConnections { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the server requires authentication for its resources to be accessed.
        /// </summary>
        public bool RequireAuthentication { get; set; }

        /// <summary>
        ///     Gets or sets a value of the realm presented to the user when authenticating.
        /// </summary>
        public string AuthenticateRealm { get; set; }

        /// <summary>
        ///     Gets or sets the maximum length of content that the client can post.
        /// </summary>
        public long MaxPostLength { get; set; } = 4 * 1024 * 1024;

        #endregion

        #region Caches

        private readonly Hashtable absUriCache = new Hashtable();
        private readonly Hashtable relUriCache = new Hashtable();

        internal Uri GetAbsUri(string uri) {
            Uri ret;
            lock (absUriCache) {
                ret = absUriCache[uri] as Uri;
                if (ret != null)
                    return ret;
                if (absUriCache.Count > uriCacheMax)
                    absUriCache.Clear();
                ret = new Uri(uri);
                absUriCache[uri] = ret;
            }
            return ret;
            //			return new Uri(uri);
        }

        internal Uri GetRelUri(string uri) {
            Uri ret;
            lock (relUriCache) {
                ret = relUriCache[uri] as Uri;
                if (ret != null)
                    return ret;
                if (relUriCache.Count > uriCacheMax)
                    relUriCache.Clear();
                ret = new Uri(serverUri, uri);
                relUriCache[uri] = ret;
            }
            return ret;
            //			return new Uri(serverUri, uri);
        }

        private readonly Hashtable uriHosts = new Hashtable();
        private int uriHostsCount;

        internal Uri GetHostUri(string host, string uri) {
            Uri ret;

            lock (uriHosts) {
                var uris = uriHosts[host] as Hashtable;
                if (uris == null) {
                    uris = new Hashtable();
                    uriHosts.Add(host, uris);
                }
                ret = uris[uri] as Uri;
                if (ret == null) {
                    if (uriHostsCount > uriCacheMax) {
                        uriHosts.Clear();
                        uriHostsCount = 0;
                    }
                    ret = new Uri(new Uri("http://" + host), uri);
                    uris[uri] = ret;
                }
            }

            return ret;
        }

        #endregion

        #region Listener

        private TcpListener listener;

        private bool stop;

        /// <summary>
        ///     Gets a value indicating whether the server is currently listening for connections.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        ///     Occurs when the server is started.
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        ///     Occurs when the server is about to stop.
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        ///     Occurs when the server is stopped.
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        ///     Starts listening for connections.
        /// </summary>
        public void Start() {
            if (IsRunning)
                return;
            ListenerThread = new Thread(DoListen);
            listener = new TcpListener(localAddress, port);
            listener.Start();

            Port = ((IPEndPoint)listener.LocalEndpoint).Port;

            IsRunning = true;

            Started?.Invoke(this, e: null);

            Log.WriteLine("Server running at " + ServerUri);

            ListenerThread.Start();
        }

        /// <summary>
        ///     Stops listening for connections.
        /// </summary>
        public void Stop() {
            if (!IsRunning)
                return;
            Log.WriteLine("Server stopping");
            stop = true;
            listener?.Stop();

            Stopping?.Invoke(this, e: null);

            try {
                JoinListener();
            }
            catch (MemberAccessException) { }
            catch (NotImplementedException) { }

            Log.WriteLine("Server stopped");

            Stopped?.Invoke(this, e: null);
        }

        private void JoinListener() { ListenerThread.Join(); }

        private void DoListen() {
            try {
                while (!stop) {
                    HttpClient client;
                    try {
                        client = new HttpClient(listener.AcceptSocket(), this);
                    }
                    catch (IOException) {
                        continue;
                    }
                    catch (SocketException) {
                        continue;
                    }
                    client.Disconnected += client_Disconnected;
                    ClientConnected?.Invoke(this, new ClientEventArgs(client));
                    if (LogConnections)
                        Log.WriteLine("Connected: " + client.RemoteAddress);
                }
            }
#if !DEBUG
			catch(SocketException e)
			{
				Log.WriteLine("Error: " + e.ToString());
			}
#endif
            finally {
                stop = false;
                listener.Stop();
                listener = null;
                IsRunning = false;
            }
        }

        #endregion

        #region Client Events

        private readonly Timer idleTimer;

        internal event EventHandler OneHertzTick;

        private void TimerCallback(object state) {
            OneHertzTick?.Invoke(this, e: null);
        }

        /// <summary>
        ///     Represents an event which occurs when the client's state changes.
        /// </summary>
        public delegate void ClientEventHandler(object sender, ClientEventArgs e);

        /// <summary>
        ///     Occurs when a client connects to the server.
        /// </summary>
        public event ClientEventHandler ClientConnected;

        /// <summary>
        ///     Occurs when a client is disconnected from the server.
        /// </summary>
        public event ClientEventHandler ClientDisconnected;

        private void client_Disconnected(object sender, EventArgs e) {
            var client = sender as HttpClient;
            if (LogConnections)
                Log.WriteLine("Disconnected: " + client?.RemoteAddress);
            ClientDisconnected?.Invoke(this, new ClientEventArgs(client));
        }

        /// <summary>
        ///     Represents an event which occurs when an HTTP request is received.
        /// </summary>
        public delegate void RequestEventHandler(object sender, RequestEventArgs e);

        /// <summary>
        ///     Occurs when any request is received, valid or invalid.
        /// </summary>
        public event RequestEventHandler RequestReceived;

        /// <summary>
        ///     Occurs when a valid request to which a response can be made is received.
        /// </summary>
        public event RequestEventHandler ValidRequestReceived;

        /// <summary>
        ///     Occurs when an invalid request to which no response other than an error can be made is received.
        /// </summary>
        public event RequestEventHandler InvalidRequestReceived;

        /// <summary>
        ///     Gets or sets an <see cref="IAuthenticator" /> object responsible for authenticating all requests.
        /// </summary>
        public IAuthenticator Authenticator { get; set; }

        internal void OnRequestReceived(HttpClient client, HttpRequest request) {
            var args = new RequestEventArgs(client, request);

            RequestReceived?.Invoke(this, args);
            if (request.IsValidRequest) {
                if (LogRequests)
                    Log.WriteLine("Request: " + client.RemoteAddress + " " + request.Uri.OriginalString);
                ValidRequestReceived?.Invoke(this, args);
            }
            else {
                InvalidRequestReceived?.Invoke(this, args);
            }
        }

        #endregion

        #region Logging

        private static TextWriter InitializeLog() {
            TextWriter log;
            // Initialize the log to output to the console if it is available on the platform, otherwise initialize to null stream writer.
            try {
                log = GetConsoleLog();
            }
            catch (MemberAccessException) {
                log = TextWriter.Null;
            }
            catch (NotImplementedException) {
                log = TextWriter.Null;
            }
            return log;
        }

        private static TextWriter GetConsoleLog() { return Console.Out; }

        /// <summary>
        ///     Gets or sets the <see cref="TextWriter" /> to which to write logs.
        /// </summary>
        public TextWriter Log { get; set; } = InitializeLog();

        #endregion
    }
}
