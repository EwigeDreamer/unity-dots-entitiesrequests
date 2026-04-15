using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using EntitiesRequests.Internal;

namespace EntitiesRequests
{
    [NativeContainer]
    [NativeContainerIsAtomicWriteOnly]
    public unsafe struct RequestWriter<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private RequestsData<T>* buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
#endif

        internal RequestWriter(in Requests<T> requests)
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
        public void Write(in T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            buffer->Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNoResize(in T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            buffer->WriteNoResize(value);
        }
    }
}

namespace EntitiesRequests.LowLevel.Unsafe
{
    public unsafe struct UnsafeRequestWriter<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private RequestsData<T>* buffer;

        internal UnsafeRequestWriter(in UnsafeRequests<T> requests)
        {
            buffer = requests.buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(in T value)
        {
            buffer->Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNoResize(in T value)
        {
            buffer->WriteNoResize(value);
        }
    }
}