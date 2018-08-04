using IllusionInjector.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionInjector.Utilities
{
    class BlockingStream : Stream
    {
        public BlockingStream(Stream bstr)
        {
            BaseStream = bstr;
        }

        public Stream BaseStream { get; set; }

        private bool _open = true;
        public bool Open {
            get
            {
                return CanWrite;
            }
            set
            {
                if (!_open)
                    throw new InvalidOperationException("Blocking stream has already been closed!");
                else
                    _open = value;
            }
        }

        private bool canReadOverride = true;
        public override bool CanRead => BaseStream.CanRead && canReadOverride;

        public override bool CanSeek => BaseStream.CanSeek;

        public override bool CanWrite => BaseStream.CanWrite && _open;

        public override long Length => BaseStream.Length;

        public override long Position { get => BaseStream.Position; set => BaseStream.Position = value; }

        public override void Flush()
        {
            BaseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = 0;
            while (read < count && Open)
            {
                read += BaseStream.Read(buffer, read, count-read);
            }

            if (read == 0)
            {
                canReadOverride = false;
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return BaseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            BaseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStream.Write(buffer, offset, count);
        }

        public override string ToString()
        {
            return $"{base.ToString()} ({BaseStream?.ToString()})";
        }
    }
}
