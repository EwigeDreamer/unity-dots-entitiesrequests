using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Manages double-buffered request storage for type <typeparamref name="T"/> with fixed roles:
    /// - writeBuffer: always for writing new requests (via RequestWriter)
    /// - readBuffer: always for reading (via RequestReader)
    /// Update() moves all items from writeBuffer to readBuffer, then clears writeBuffer.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public unsafe struct RequestsData<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private NativeRequestBuffer<T>* _writeBuffer;

        [NativeDisableUnsafePtrRestriction]
        private NativeRequestBuffer<T>* _readBuffer;

        private readonly Allocator _allocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestsData{T}"/> struct with the specified capacity and allocator.
        /// </summary>
        /// <param name="initialCapacity">Initial capacity for both internal buffers.</param>
        /// <param name="allocator">Allocator to use for all internal allocations.</param>
        public RequestsData(int initialCapacity, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob, Persistent or registered custom allocator", nameof(allocator));
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "InitialCapacity must be >= 0");
#endif

            _allocator = allocator;

            // Allocate and initialize write buffer
            var size = UnsafeUtility.SizeOf<NativeRequestBuffer<T>>();
            var alignment = UnsafeUtility.AlignOf<NativeRequestBuffer<T>>();
            _writeBuffer = (NativeRequestBuffer<T>*)UnsafeUtility.MallocTracked(size, alignment, allocator, 1);
            UnsafeUtility.MemClear(_writeBuffer, size);
            var tempWrite = new NativeRequestBuffer<T>(initialCapacity, allocator);
            UnsafeUtility.CopyStructureToPtr(ref tempWrite, _writeBuffer);

            // Allocate and initialize read buffer
            _readBuffer = (NativeRequestBuffer<T>*)UnsafeUtility.MallocTracked(size, alignment, allocator, 1);
            UnsafeUtility.MemClear(_readBuffer, size);
            var tempRead = new NativeRequestBuffer<T>(initialCapacity, allocator);
            UnsafeUtility.CopyStructureToPtr(ref tempRead, _readBuffer);
        }

        /// <summary>
        /// Moves all pending writes to the read buffer and clears the write buffer.
        /// Called automatically by RequestSystemBase&lt;T&gt; every frame.
        /// </summary>
        public void Update()
        {
            var writeLen = _writeBuffer->_listPtr->Length;
            if (writeLen > 0)
            {
                _readBuffer->EnsureCapacity(_readBuffer->_listPtr->Length + writeLen);
                _readBuffer->_listPtr->AddRange(*_writeBuffer->_listPtr);
            }
            _writeBuffer->Clear();
        }

        /// <summary>
        /// Clears the read buffer. Called explicitly by RequestReader&lt;T&gt;.
        /// </summary>
        public void ClearReadBuffer()
        {
            _readBuffer->Clear();
        }

        /// <summary>
        /// Returns a reference to the buffer currently designated for writing.
        /// </summary>
        public NativeRequestBuffer<T>* GetWriteBuffer()
        {
            return _writeBuffer;
        }

        /// <summary>
        /// Returns a reference to the buffer currently designated for reading.
        /// </summary>
        public NativeRequestBuffer<T>* GetReadBuffer()
        {
            return _readBuffer;
        }

        /// <summary>
        /// Ensures that both internal buffers have at least the specified capacity.
        /// </summary>
        /// <param name="capacity">Minimum capacity required.</param>
        public void EnsureCapacity(int capacity)
        {
            _writeBuffer->EnsureCapacity(capacity);
            _readBuffer->EnsureCapacity(capacity);
        }

        /// <summary>
        /// Disposes both internal buffers and frees allocated memory.
        /// </summary>
        public void Dispose()
        {
            if (_writeBuffer != null)
            {
                _writeBuffer->Dispose();
                UnsafeUtility.FreeTracked(_writeBuffer, _allocator);
                _writeBuffer = null;
            }

            if (_readBuffer != null)
            {
                _readBuffer->Dispose();
                UnsafeUtility.FreeTracked(_readBuffer, _allocator);
                _readBuffer = null;
            }
        }
    }
}