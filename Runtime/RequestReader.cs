using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using EntitiesRequests.Internal;
using System.Collections.Generic;

namespace EntitiesRequests
{
    [NativeContainer]
    [NativeContainerIsReadOnly]
    public unsafe struct RequestReader<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private RequestsData<T>* buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
#endif

        internal RequestReader(in Requests<T> requests)
        {
            buffer = requests.GetBuffer();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(requests.m_Safety);
            var ash = requests.m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref ash);
            m_Safety = ash;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RequestIterator<T> Read()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return new RequestIterator<T>(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            buffer->ClearReadBuffer();
        }
    }

    public unsafe struct RequestIterator<T> where T : unmanaged
    {
        private readonly RequestsData<T>* buffer;

        internal RequestIterator(RequestsData<T>* buffer)
        {
            this.buffer = buffer;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(buffer->readBuffer);
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly UnsafeList<T> readBuffer;
            private int index;

            internal Enumerator(in UnsafeList<T> readBuffer)
            {
                this.readBuffer = readBuffer;
                index = -1;
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => readBuffer[index];
            }

            object System.Collections.IEnumerator.Current => Current;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                index++;
                return index < readBuffer.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                index = -1;
            }

            public void Dispose() { }
        }
    }
}

namespace EntitiesRequests.LowLevel.Unsafe
{
    public unsafe struct UnsafeRequestReader<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private RequestsData<T>* buffer;

        internal UnsafeRequestReader(in UnsafeRequests<T> requests)
        {
            buffer = requests.buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeRequestIterator<T> Read()
        {
            return new UnsafeRequestIterator<T>(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            buffer->ClearReadBuffer();
        }
    }

    public unsafe struct UnsafeRequestIterator<T> where T : unmanaged
    {
        private readonly RequestsData<T>* buffer;

        internal UnsafeRequestIterator(RequestsData<T>* buffer)
        {
            this.buffer = buffer;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(buffer->readBuffer);
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly UnsafeList<T> readBuffer;
            private int index;

            internal Enumerator(in UnsafeList<T> readBuffer)
            {
                this.readBuffer = readBuffer;
                index = -1;
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => readBuffer[index];
            }

            object System.Collections.IEnumerator.Current => Current;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                index++;
                return index < readBuffer.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                index = -1;
            }

            public void Dispose() { }
        }
    }
}