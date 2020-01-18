using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security;
using System.Text;

namespace System
{
	// this is literally jus8t the decompilation of Mono's .NET 4 implementation

	/// <summary>Represents a typed weak reference, which references an object while still allowing that object to be reclaimed by garbage collection.</summary>
	/// <typeparam name="T">The type of the object referenced.</typeparam>
	// Token: 0x02000248 RID: 584
	[Serializable]
	public sealed class WeakReference<T> : ISerializable where T : class
	{
		/// <summary>Initializes a new instance of the <see cref="T:System.WeakReference`1" /> class that references the specified object.</summary>
		/// <param name="target">The object to reference, or <see langword="null" />.</param>
		// Token: 0x06001B8A RID: 7050 RVA: 0x00068700 File Offset: 0x00066900
		public WeakReference(T target) : this(target, false)
		{
		}

		/// <summary>Initializes a new instance of the <see cref="T:System.WeakReference`1" /> class that references the specified object and uses the specified resurrection tracking.</summary>
		/// <param name="target">The object to reference, or <see langword="null" />.</param>
		/// <param name="trackResurrection">
		///       <see langword="true" /> to track the object after finalization; <see langword="false" /> to track the object only until finalization.</param>
		// Token: 0x06001B8B RID: 7051 RVA: 0x0006870C File Offset: 0x0006690C
		public WeakReference(T target, bool trackResurrection)
		{
			this.trackResurrection = trackResurrection;
			GCHandleType type = trackResurrection ? GCHandleType.WeakTrackResurrection : GCHandleType.Weak;
			handle = GCHandle.Alloc(target, type);
		}

		// Token: 0x06001B8C RID: 7052 RVA: 0x00068740 File Offset: 0x00066940
		private WeakReference(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
			{
				throw new ArgumentNullException(nameof(info));
			}
			trackResurrection = info.GetBoolean("TrackResurrection");
			object value = info.GetValue("TrackedObject", typeof(T));
			GCHandleType type = trackResurrection ? GCHandleType.WeakTrackResurrection : GCHandleType.Weak;
			handle = GCHandle.Alloc(value, type);
		}

		/// <summary>Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo" /> object with all the data necessary to serialize the current <see cref="T:System.WeakReference`1" /> object.</summary>
		/// <param name="info">An object that holds all the data necessary to serialize or deserialize the current <see cref="T:System.WeakReference`1" /> object.</param>
		/// <param name="context">The location where serialized data is stored and retrieved.</param>
		/// <exception cref="T:System.ArgumentNullException">
		///         <paramref name="info" /> is <see langword="null" />. </exception>
		// Token: 0x06001B8D RID: 7053 RVA: 0x000687A4 File Offset: 0x000669A4
		[SecurityCritical]
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
			{
				throw new ArgumentNullException(nameof(info));
			}
			info.AddValue("TrackResurrection", trackResurrection);
			if (handle.IsAllocated)
			{
				info.AddValue("TrackedObject", handle.Target);
				return;
			}
			info.AddValue("TrackedObject", null);
		}

		/// <summary>Sets the target object that is referenced by this <see cref="T:System.WeakReference`1" /> object.</summary>
		/// <param name="target">The new target object.</param>
		// Token: 0x06001B8E RID: 7054 RVA: 0x00068800 File Offset: 0x00066A00
		public void SetTarget(T target)
		{
			handle.Target = target;
		}

		/// <summary>Tries to retrieve the target object that is referenced by the current <see cref="T:System.WeakReference`1" /> object.</summary>
		/// <param name="target">When this method returns, contains the target object, if it is available. This parameter is treated as uninitialized.</param>
		/// <returns>
		///     <see langword="true" /> if the target was retrieved; otherwise, <see langword="false" />.</returns>
		// Token: 0x06001B8F RID: 7055 RVA: 0x00068813 File Offset: 0x00066A13
		public bool TryGetTarget(out T target)
		{
			if (!handle.IsAllocated)
			{
				target = default;
				return false;
			}
			target = (T)handle.Target;
			return target != null;
		}

		/// <summary>Discards the reference to the target that is represented by the current <see cref="T:System.WeakReference`1" /> object.</summary>
		// Token: 0x06001B90 RID: 7056 RVA: 0x00068850 File Offset: 0x00066A50
		~WeakReference()
		{
			handle.Free();
		}

		// Token: 0x04000F59 RID: 3929
		private GCHandle handle;

		// Token: 0x04000F5A RID: 3930
		private bool trackResurrection;
	}
}
