using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Net3_Proxy
{
    public static class Extensions
    {
        public static T GetCustomAttribute<T>(this ParameterInfo element) where T : Attribute
            => (T)GetCustomAttribute(element, typeof(T));
        public static T GetCustomAttribute<T>(this MethodInfo element) where T : Attribute
            => (T)GetCustomAttribute(element, typeof(T));
        public static T GetCustomAttribute<T>(this ConstructorInfo element) where T : Attribute
            => (T)GetCustomAttribute(element, typeof(T));
        public static T GetCustomAttribute<T>(this Type element) where T : Attribute
            => (T)GetCustomAttribute(element, typeof(T));

        public static Attribute GetCustomAttribute(this MemberInfo element, Type attributeType)
            => Attribute.GetCustomAttribute(element, attributeType);
        public static Attribute GetCustomAttribute(this ConstructorInfo element, Type attributeType)
            => Attribute.GetCustomAttribute(element, attributeType);
        public static Attribute GetCustomAttribute(this ParameterInfo element, Type attributeType) 
            => Attribute.GetCustomAttribute(element, attributeType);
        public static Attribute GetCustomAttribute(this Type element, Type attributeType)
            => Attribute.GetCustomAttribute(element, attributeType);

        public static StringBuilder Clear(this StringBuilder sb)
            => sb.Remove(0, sb.Length);

        public static bool HasFlag<E>(this E e, E o) where E : Enum
        {
            var ei = Convert.ToUInt64(e);
            var oi = Convert.ToUInt64(o);
            return (ei & oi) == oi;
        }
    }

    public static class DirectoryInfoExtensions
    {
        public static IEnumerable<FileInfo> EnumerateFiles(this DirectoryInfo self)
        {
            return self.EnumerateFiles("*", SearchOption.TopDirectoryOnly);
        }

        public static IEnumerable<FileInfo> EnumerateFiles(this DirectoryInfo self, string searchPattern)
        {
            return self.EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly);
        }

        public static IEnumerable<FileInfo> EnumerateFiles(this DirectoryInfo self, string searchPattern, SearchOption searchOption)
        {
            if (searchPattern == null)
            {
                throw new ArgumentNullException(nameof(searchPattern));
            }
            return CreateEnumerateFilesIterator(self, searchPattern, searchOption);
        }


        private static IEnumerable<FileInfo> CreateEnumerateFilesIterator(DirectoryInfo self, string searchPattern, SearchOption searchOption)
        {
            foreach (string fileName in Directory.GetFiles(self.FullName, searchPattern, searchOption))
                yield return new FileInfo(fileName);
            yield break;
        }
    }

    public static class StreamExtensions
    {
        [ComVisible(false)]
        [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
        public static Task CopyToAsync(this Stream src, Stream destination) => CopyToAsync(src, destination, 81920);

        [ComVisible(false)]
        [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
        public static Task CopyToAsync(this Stream src, Stream destination, int bufferSize) => CopyToAsync(src, destination, bufferSize, CancellationToken.None);

        [ComVisible(false)]
        [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
        public static Task CopyToAsync(this Stream src, Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Positive number required.");
            }
            if (!src.CanRead && !src.CanWrite)
            {
                throw new ObjectDisposedException(null, "Cannot access a closed Stream.");
            }
            if (!destination.CanRead && !destination.CanWrite)
            {
                throw new ObjectDisposedException("destination", "Cannot access a closed Stream.");
            }
            if (!src.CanRead)
            {
                throw new NotSupportedException("Stream does not support reading.");
            }
            if (!destination.CanWrite)
            {
                throw new NotSupportedException("Stream does not support writing.");
            }
            return CopyToAsyncInternal(src, destination, bufferSize, cancellationToken);
        }

        private static async Task CopyToAsyncInternal(Stream src, Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = await src.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            }
        }

        [ComVisible(false)]
        [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
        public static Task<int> ReadAsync(this Stream src, byte[] buffer, int offset, int count)
        {
            return ReadAsync(src, buffer, offset, count, CancellationToken.None);
        }

        [ComVisible(false)]
        [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
        public static Task<int> ReadAsync(this Stream src, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                return BeginEndReadAsync(src, buffer, offset, count);
            }
            return new Task<int>(() => 0, cancellationToken);
        }

        private static Task<int> BeginEndReadAsync(Stream src, byte[] buffer, int offset, int count)
            => Task<int>.Factory.FromAsync(
                (byte[] buffer_, int offset_, int count_, AsyncCallback callback, object state) =>
                    src.BeginRead(buffer_, offset_, count_, callback, state),
                (IAsyncResult asyncResult) => src.EndRead(asyncResult), 
                buffer, 
                offset, 
                count,
                new object());

        [ComVisible(false)]
        [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
        public static Task WriteAsync(this Stream src, byte[] buffer, int offset, int count)
        {
            return WriteAsync(src, buffer, offset, count, CancellationToken.None);
        }

        [ComVisible(false)]
        [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
        public static Task WriteAsync(this Stream src, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                return BeginEndWriteAsync(src, buffer, offset, count);
            }
            return new Task<int>(() => 0, cancellationToken);
        }

        private static Task BeginEndWriteAsync(Stream src, byte[] buffer, int offset, int count)
            => Task.Factory.FromAsync(
                (byte[] buffer_, int offset_, int count_, AsyncCallback callback, object state) =>
                    src.BeginWrite(buffer_, offset_, count_, callback, state),
                (IAsyncResult asyncResult) => src.EndWrite(asyncResult),
                buffer,
                offset,
                count,
                new object());

    }

    public static class SemaphoreSlimExtesnions
    { // TODO: finish the WaitAsync members
        /*public static Task WaitAsync(this SemaphoreSlim self)
        {
            return null;
        }*/
    }
}
