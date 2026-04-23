using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Provides write access to requests of type <typeparamref name="T"/>.
    /// Can be safely cached across frames; always writes to the current active write buffer.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    [BurstCompile]
    [NativeContainer]
    [NativeContainerIsAtomicWriteOnly]
    public unsafe struct RequestWriter<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly RequestsData<T>* _data;

        internal RequestWriter(in Requests<T> requests)
        {
            _data = requests._data;
        }

        /// <summary>
        /// Writes a request into the current write buffer. The buffer will grow if necessary.
        /// </summary>
        /// <param name="value">Request data to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(in T value)
        {
            var writeBuffer = _data->GetWriteBuffer();
            writeBuffer->Write(value);
        }

        /// <summary>
        /// Writes a request without checking capacity.
        /// Ensure buffer capacity is sufficient before calling this method.
        /// </summary>
        /// <param name="value">Request data to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNoResize(in T value)
        {
            var writeBuffer = _data->GetWriteBuffer();
            writeBuffer->WriteNoResize(value);
        }

        /// <summary>
        /// Returns a parallel writer that can be used to write requests from multiple threads.
        /// The parallel writer captures the current write buffer at call time.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(_data->GetWriteBuffer());
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
            /// Ensure buffer capacity is sufficient before calling this method.
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