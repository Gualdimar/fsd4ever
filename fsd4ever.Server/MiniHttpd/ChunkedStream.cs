using System.Globalization;
using System.IO;
using System.Text;

namespace fsd4ever.Server.MiniHttpd {
    internal class ChunkedStream : ImmediateResponseStream {
        public ChunkedStream(Stream outputStream) : base(outputStream) { }

        public override void Write(byte[] buffer, int offset, int count) {
            var lengthLine = Encoding.UTF8.GetBytes(count.ToString("x", CultureInfo.InvariantCulture) + "\r\n");
            OutputStream.Write(lengthLine, offset: 0, count: lengthLine.Length);
            base.Write(buffer, offset, count, flush: false);
            OutputStream.Write(new byte[] {
                                   13,
                                   10
                               },
                               offset: 0,
                               count: 2);
            OutputStream.Flush();
        }
    }
}
