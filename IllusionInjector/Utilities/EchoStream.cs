using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IllusionInjector.Utilities
{
    public class EchoStream : MemoryStream
    {
        private ManualResetEvent m_dataReady = new ManualResetEvent(false);
        private byte[] m_buffer;
        private int m_offset;
        private int m_count;

        public override void Write(byte[] buffer, int offset, int count)
        {
            m_buffer = buffer;
            m_offset = offset;
            m_count = count;
            m_dataReady.Set();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (m_buffer == null)
            {
                // Block until the stream has some more data.
                m_dataReady.Reset();
                m_dataReady.WaitOne();
            }

            Buffer.BlockCopy(m_buffer, m_offset, buffer, offset, (count < m_count) ? count : m_count);
            m_buffer = null;
            return (count < m_count) ? count : m_count;
        }
    }
}
