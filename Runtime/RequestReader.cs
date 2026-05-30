using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Provides read access to requests of type <typeparamref name="T"/>.
    /// Can be safely cached across frames; always reads from the shared read buffer.
    /// Must call <see cref="Clear"/> after processing to remove requests from the read buffer.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    [BurstCompile]
    [NativeContainer]
    public unsafe struct RequestReader<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly RequestsData<T>* _data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<RequestReader<T>>();
#endif

        internal RequestReader(in Requests<T> requests)
        {
            _data = requests.GetUnsafeData();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(Allocator.Persistent);
            CollectionHelper.SetStaticSafetyId<RequestReader<T>>(ref m_Safety, ref s_staticSafetyId.Data);
            if (UnsafeUtility.IsNativeContainerType<T>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        /// <summary>
        /// Returns an iterator that can be used in a foreach loop to read requests from the read buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RequestIterator Read()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var readBuffer = _data->GetReadBuffer();
            return new RequestIterator(readBuffer);
        }

        /// <summary>
        /// Clears all requests from the read buffer. Must be called explicitly after processing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            var readBuffer = _data->GetReadBuffer();
            readBuffer->Clear();
        }

        /// <summary>
        /// Iterator struct that enables enumeration over requests in the read buffer.
        /// </summary>
        [BurstCompile]
        public readonly struct RequestIterator
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly NativeRequestBuffer<T>* _readBuffer;

            internal RequestIterator(NativeRequestBuffer<T>* readBuffer)
            {
                _readBuffer = readBuffer;
            }

            /// <summary>
            /// Returns an enumerator that iterates over requests in the read buffer.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator GetEnumerator() => new Enumerator(_readBuffer);
        }

        /// <summary>
        /// Enumerator for iterating over requests in the read buffer.
        /// </summary>
        [BurstCompile]
        public struct Enumerator : IEnumerator<T>
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly T* _ptr;
            private readonly int _length;
            private int _index;

            internal Enumerator(NativeRequestBuffer<T>* buffer)
            {
                var listPtr = buffer->_listPtr;
                _ptr = listPtr->Ptr;
                _length = listPtr->Length;
                _index = -1;
            }

            /// <summary>
            /// Moves to the next element.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                _index++;
                return _index < _length;
            }

            /// <summary>
            /// Gets the current element.
            /// </summary>
            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _ptr[_index];
            }

            object IEnumerator.Current => Current;

            /// <summary>
            /// Resets the enumerator to the beginning.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                _index = -1;
            }

            /// <summary>
            /// Disposes the enumerator.
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}