using System;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     Provides data for events that are raised by the client of an HTTP transaction.
    /// </summary>
    public class ClientEventArgs : EventArgs {
        /// <summary>
        ///     Creates a new <see cref="ClientEventArgs" /> with the specified client.
        /// </summary>
        /// <param name="client">The client to which the event belongs.</param>
        public ClientEventArgs(HttpClient client) {
            HttpClient = client;
        }

        /// <summary>
        ///     Gets the client to which the event belongs.
        /// </summary>
        public HttpClient HttpClient { get; }
    }

    /// <summary>
    ///     Provides access to the information of the events that occur when a request is received.
    /// </summary>
    public class RequestEventArgs : ClientEventArgs {
        internal RequestEventArgs(HttpClient client, HttpRequest request) : base(client) { Request = request; }

        /// <summary>
        ///     Gets the request which the client sent.
        /// </summary>
        public HttpRequest Request { get; }

        /// <summary>
        ///     Gets or sets a value indicating whether the user has been authenticated for the transaction.
        /// </summary>
        public bool IsAuthenticated { get; set; } = true;
    }

    /// <summary>
    ///     Provides data to an event raised by an <see>HttpResponse</see> object.
    /// </summary>
    public class ResponseEventArgs : ClientEventArgs {
        internal ResponseEventArgs(HttpClient client, HttpResponse response) : this(client, response, contentLength: -1) { }

        internal ResponseEventArgs(HttpClient client, HttpResponse response, long contentLength) : base(client) {
            Response = response;
            ContentLength = contentLength;
        }

        /// <summary>
        ///     Gets the <see>HttpResponse</see> object that triggered this event.
        /// </summary>
        public HttpResponse Response { get; }

        /// <summary>
        ///     Gets the content length of the response. This value is negative when the content length is unknown.
        /// </summary>
        public long ContentLength { get; }
    }
}
