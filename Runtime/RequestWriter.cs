using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Provides write access to requests of type <typeparamref name="T"/> using a dedicated private buffer.
    /// Each RequestWriter instance owns its own buffer and registers it with the central Requests container.
    /// Must be disposed explicitly (typically in system OnDestroy) to unregister and free the buffer.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    [BurstCompile]
    [NativeContainer]
    [NativeContainerIsAtomicWriteOnly]
    public unsafe struct RequestWriter<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly RequestsData<T>* _data;

        [NativeDisableUnsafePtrRestriction]
        private NativeRequestBuffer<T>* _buffer;

        private readonly Allocator _allocator;

        /// <summary>
        /// Creates a new RequestWriter with its own private buffer, registers it, and prepares for writing.
        /// </summary>
        /// <param name="requests">The parent Requests container.</param>
        /// <param name="initialCapacity">Initial capacity for the private buffer.</param>
        /// <param name="allocator">Allocator used for the private buffer.</param>
        internal RequestWriter(in Requests<T> requests, int initialCapacity, Allocator allocator)
        {
            _data = requests._data;
            _allocator = allocator;

            // Allocate and initialize the private buffer
            var size = UnsafeUtility.SizeOf<NativeRequestBuffer<T>>();
            var alignment = UnsafeUtility.AlignOf<NativeRequestBuffer<T>>();
            _buffer = (NativeRequestBuffer<T>*)UnsafeUtility.MallocTracked(size, alignment, _allocator, 1);
            UnsafeUtility.MemClear(_buffer, size);
            var tempBuffer = new NativeRequestBuffer<T>(initialCapacity, _allocator);
            UnsafeUtility.CopyStructureToPtr(ref tempBuffer, _buffer);

            // Register this buffer with the central data
            _data->RegisterWriteBuffer(_buffer);
        }

        /// <summary>
        /// Writes a request into the private buffer. The buffer will grow if necessary.
        /// </summary>
        /// <param name="value">Request data to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(in T value)
        {
            _buffer->Write(value);
        }

        /// <summary>
        /// Writes a request without checking capacity.
        /// Ensure the buffer has sufficient capacity before calling this method (via EnsureCapacity).
        /// </summary>
        /// <param name="value">Request data to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNoResize(in T value)
        {
            _buffer->WriteNoResize(value);
        }

        /// <summary>
        /// Ensures the private buffer has at least the specified capacity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int capacity)
        {
            _buffer->EnsureCapacity(capacity);
        }

        /// <summary>
        /// Returns a parallel writer that can be used to write requests from multiple threads.
        /// The parallel writer captures the private buffer of this RequestWriter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(_buffer);
        }

        /// <summary>
        /// Unregisters and disposes the private buffer. Must be called to avoid memory leaks.
        /// After disposal, this writer cannot be used.
        /// </summary>
        public void Dispose()
        {
            if (_buffer == null)
                return;

            _data->UnregisterWriteBuffer(_buffer);
            _buffer->Dispose();
            UnsafeUtility.FreeTracked(_buffer, _allocator);
            _buffer = null;
        }

        /// <summary>
        /// Provides parallel write access to requests.
        /// Suitable for use in <see cref="Unity.Jobs.IJobParallelFor"/> and similar.
        /// This writer is thread-safe and uses atomic operations internally.
        /// </summary>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public unsafe struct ParallelWriter
        {
            private UnsafeList<T>.ParallelWriter _parallelWriter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            internal ParallelWriter(NativeRequestBuffer<T>* writeBuffer)
            {
                _parallelWriter = writeBuffer->_listPtr->AsParallelWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = writeBuffer->m_Safety;
                AtomicSafetyHandle.UseSecondaryVersion(ref m_Safety);
                AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            }

            /// <summary>
            /// Writes a request without checking capacity.
            /// Ensure buffer capacity is sufficient before using this method.
            /// </summary>
            /// <param name="value">Request data to write.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteNoResize(in T value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                _parallelWriter.AddNoResize(value);
            }
        }
    }
}