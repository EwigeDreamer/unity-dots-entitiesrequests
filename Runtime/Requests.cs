using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// A thread-safe native container for request buffers with fixed roles.
    /// Provides <see cref="RequestWriter{T}"/> and <see cref="RequestReader{T}"/> for inter-system request messaging.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    [BurstCompile]
    [NativeContainer]
    public unsafe struct Requests<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        internal RequestsData<T>* _data;

        private readonly Allocator _allocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="Requests{T}"/> struct.
        /// </summary>
        /// <param name="initialCapacity">Initial capacity of internal request buffers.</param>
        /// <param name="allocator">Allocator to use for all internal allocations.</param>
        public Requests(int initialCapacity, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob, Persistent or registered custom allocator", nameof(allocator));
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "InitialCapacity must be >= 0");
#endif

            var size = UnsafeUtility.SizeOf<RequestsData<T>>();
            var alignment = UnsafeUtility.AlignOf<RequestsData<T>>();
            _data = (RequestsData<T>*)UnsafeUtility.MallocTracked(size, alignment, allocator, 1);
            UnsafeUtility.MemClear(_data, size);

            var data = new RequestsData<T>(initialCapacity, allocator);
            UnsafeUtility.CopyStructureToPtr(ref data, _data);

            _allocator = allocator;
        }

        /// <summary>
        /// Gets a value indicating whether this container has been allocated.
        /// </summary>
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data != null;
        }

        /// <summary>
        /// Updates the internal buffers: moves all pending writes to the read buffer and clears the write buffer.
        /// Call this once per frame to make written requests available for reading.
        /// Normally called automatically by RequestSystemBase&lt;T&gt;.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            CheckData();
            _data->Update();
        }

        /// <summary>
        /// Clears the read buffer. Must be called explicitly by the consuming system after processing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearReadBuffer()
        {
            CheckData();
            _data->ClearReadBuffer();
        }

        /// <summary>
        /// Returns a writer for publishing requests.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RequestWriter<T> GetWriter()
        {
            CheckData();
            return new RequestWriter<T>(this);
        }

        /// <summary>
        /// Returns a reader for consuming published requests.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RequestReader<T> GetReader()
        {
            CheckData();
            return new RequestReader<T>(this);
        }

        /// <summary>
        /// Ensures that both internal buffers have at least the specified capacity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int capacity)
        {
            CheckData();
            _data->EnsureCapacity(capacity);
        }

        /// <summary>
        /// Releases all resources held by this container.
        /// </summary>
        public void Dispose()
        {
            if (_data == null)
                return;

            _data->Dispose();
            UnsafeUtility.FreeTracked(_data, _allocator);
            _data = null;
        }

        /// <summary>
        /// Internal accessor for the underlying unsafe requests pointer.
        /// </summary>
        internal RequestsData<T>* GetUnsafeData()
        {
            return _data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckData()
        {
            if (_data == null)
                throw new InvalidOperationException("Requests has not been allocated or has been disposed.");
        }
    }
}