using System;
using System.IO;

namespace fsd4ever.Server.MiniHttpd {
    internal class ImmediateResponseStream : Stream {
        protected readonly Stream OutputStream;

        internal ImmediateResponseStream(Stream outputStream) { OutputStream = outputStream; }

        public long BytesSent { get; private set; }

        public override bool CanRead => false;

        public override bool CanWrite => true;

        public override bool CanSeek => false;

        public override long Length {
            get { throw new NotSupportedException(); }
        }

        public override long Position {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }

        public override void SetLength(long value) { throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count) { Write(buffer, offset, count, flush: true); }

        public void Write(byte[] buffer, int offset, int count, bool flush) {
            OutputStream.Write(buffer, offset, count);
            BytesSent += count;
        }
    }
}
