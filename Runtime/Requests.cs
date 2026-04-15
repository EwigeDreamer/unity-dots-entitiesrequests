using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using EntitiesRequests.LowLevel.Unsafe;
using EntitiesRequests.Internal;
using Unity.Burst;

namespace EntitiesRequests
{
    [NativeContainer]
    public struct Requests<T> : IDisposable where T : unmanaged
    {
        private UnsafeRequests<T> container;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<Requests<T>>();
#endif

        public Requests(int initialCapacity, Allocator allocator)
        {
            container = new UnsafeRequests<T>(initialCapacity, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<Requests<T>>(ref m_Safety, ref s_staticSafetyId.Data);
            if (UnsafeUtility.IsNativeContainerType<T>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => container.IsCreated;
        }

        internal readonly unsafe RequestsData<T>* GetBuffer() => container.buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            container.Update();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearReadBuffer()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            container.ClearReadBuffer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RequestWriter<T> GetWriter()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif
            return new RequestWriter<T>(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RequestReader<T> GetReader()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif
            return new RequestReader<T>(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            container.Dispose();
        }
    }
}