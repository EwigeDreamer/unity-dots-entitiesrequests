using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using ED.DOTS.EntitiesRequests.Internal;

namespace ED.DOTS.EntitiesRequests.LowLevel.Unsafe
{
    /// <summary>
    /// Unsafe version of Requests&lt;T&gt;. Manages raw memory for request buffers.
    /// </summary>
    public unsafe struct UnsafeRequests<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] internal RequestsData<T>* buffer;
        private readonly Allocator allocator;

        public UnsafeRequests(int initialCapacity, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob, Persistent or registered custom allocator", nameof(allocator));
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "InitialCapacity must be >= 0");
#endif

            var size = UnsafeUtility.SizeOf<RequestsData<T>>();
            buffer = (RequestsData<T>*)UnsafeUtility.MallocTracked(size, UnsafeUtility.AlignOf<RequestsData<T>>(), allocator, 1);
            UnsafeUtility.MemClear(buffer, size);

            var data = new RequestsData<T>(initialCapacity, allocator);
            UnsafeUtility.CopyStructureToPtr(ref data, buffer);

            this.allocator = allocator;
        }

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer != null;
        }

        public void Dispose()
        {
            CheckBuffer();
            buffer->Dispose();
            UnsafeUtility.FreeTracked(buffer, allocator);
            buffer = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            CheckBuffer();
            buffer->Update();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearReadBuffer()
        {
            CheckBuffer();
            buffer->ClearReadBuffer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeRequestWriter<T> GetWriter()
        {
            CheckBuffer();
            return new UnsafeRequestWriter<T>(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeRequestReader<T> GetReader()
        {
            CheckBuffer();
            return new UnsafeRequestReader<T>(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckBuffer()
        {
            if (buffer == null)
                throw new InvalidOperationException("UnsafeRequests: buffer is not created");
        }
    }
}