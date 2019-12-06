using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPA.Config.Stores
{
    internal class GeneratedStore
    {
        private class Impl : IConfigStore
        {
            public WaitHandle SyncObject { get; private set; } = new AutoResetEvent(false);

            public ReaderWriterLockSlim WriteSyncObject => throw new NotImplementedException();

            public void ReadFrom(IConfigProvider provider)
            {
                throw new NotImplementedException();
            }

            public void WriteTo(IConfigProvider provider)
            {
                throw new NotImplementedException();
            }
        }

        public static T Create<T>() => (T)Create(typeof(T));

        public static IConfigStore Create(Type type)
        {

            return null;
        }

    }
}
