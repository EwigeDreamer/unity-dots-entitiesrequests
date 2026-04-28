using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Manages multiple writer buffers and a single read buffer for request type <typeparamref name="T"/>.
    /// - Each writer buffer is created and registered by a RequestWriter instance.
    /// - Update() copies all pending requests from every registered writer buffer into the read buffer,
    ///   then clears each writer buffer.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public unsafe struct RequestsData<T> : IDisposable where T : unmanaged
    {
        // List of pointers to writer buffers (each owned by a RequestWriter)
        private UnsafeList<IntPtr> _writeBufferPtrs;

        // Single read buffer (owned by RequestsData)
        [NativeDisableUnsafePtrRestriction]
        private NativeRequestBuffer<T>* _readBuffer;

        private readonly Allocator _allocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestsData{T}"/> struct.
        /// </summary>
        /// <param name="initialCapacity">Initial capacity for the read buffer and for the list of writer pointers.</param>
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

            // Initialize list of writer buffer pointers (stores IntPtr to NativeRequestBuffer<T>*)
            _writeBufferPtrs = new UnsafeList<IntPtr>(4, allocator);

            // Allocate and initialize read buffer
            var size = UnsafeUtility.SizeOf<NativeRequestBuffer<T>>();
            var alignment = UnsafeUtility.AlignOf<NativeRequestBuffer<T>>();
            _readBuffer = (NativeRequestBuffer<T>*)UnsafeUtility.MallocTracked(size, alignment, allocator, 1);
            UnsafeUtility.MemClear(_readBuffer, size);
            var tempRead = new NativeRequestBuffer<T>(initialCapacity, allocator);
            UnsafeUtility.CopyStructureToPtr(ref tempRead, _readBuffer);
        }

        /// <summary>
        /// Registers a writer buffer (pointer) so that its contents will be copied during Update.
        /// </summary>
        /// <param name="bufferPtr">Pointer to the writer buffer to register.</param>
        public void RegisterWriteBuffer(NativeRequestBuffer<T>* bufferPtr)
        {
            _writeBufferPtrs.Add((IntPtr)bufferPtr);
        }

        /// <summary>
        /// Unregisters a writer buffer. The buffer itself is not disposed here – the caller owns it.
        /// </summary>
        /// <param name="bufferPtr">Pointer to the writer buffer to unregister.</param>
        public void UnregisterWriteBuffer(NativeRequestBuffer<T>* bufferPtr)
        {
            for (int i = _writeBufferPtrs.Length - 1; i >= 0; i--)
            {
                if (_writeBufferPtrs[i] == (IntPtr)bufferPtr)
                {
                    _writeBufferPtrs.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Copies all pending requests from every registered writer buffer into the read buffer,
        /// then clears each writer buffer.
        /// Called automatically by RequestSystemBase&lt;T&gt; every frame.
        /// </summary>
        public void Update()
        {
            // Iterate over all registered writer buffers
            for (int i = 0; i < _writeBufferPtrs.Length; i++)
            {
                var writerBuffer = (NativeRequestBuffer<T>*)_writeBufferPtrs[i];
                int writeLen = writerBuffer->_listPtr->Length;
                if (writeLen > 0)
                {
                    // Ensure read buffer has enough capacity
                    _readBuffer->EnsureCapacity(_readBuffer->_listPtr->Length + writeLen);
                    // Append all elements from writer buffer to read buffer
                    _readBuffer->_listPtr->AddRange(*writerBuffer->_listPtr);
                    // Clear the writer buffer (keep capacity)
                    writerBuffer->Clear();
                }
            }
        }

        /// <summary>
        /// Clears the read buffer. Called explicitly by RequestReader&lt;T&gt; after processing.
        /// </summary>
        public void ClearReadBuffer()
        {
            _readBuffer->Clear();
        }

        /// <summary>
        /// Returns a pointer to the read buffer.
        /// </summary>
        public NativeRequestBuffer<T>* GetReadBuffer()
        {
            return _readBuffer;
        }

        /// <summary>
        /// Disposes the read buffer and the list of writer pointers.
        /// Does not dispose individual writer buffers – they are owned by RequestWriter instances.
        /// </summary>
        public void Dispose()
        {
            if (_readBuffer != null)
            {
                _readBuffer->Dispose();
                UnsafeUtility.FreeTracked(_readBuffer, _allocator);
                _readBuffer = null;
            }

            if (_writeBufferPtrs.IsCreated)
                _writeBufferPtrs.Dispose();
        }
    }
}