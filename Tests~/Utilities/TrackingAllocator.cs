using System;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// Test-only allocator that tracks live blocks and poisons freed memory, so use-after-free
    /// and double-free can be detected deterministically in PlayMode tests. The backing memory
    /// is delegated to a parent allocator; this allocator only records pointer -> size and counts.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct TrackingAllocator : AllocatorManager.IAllocator
    {
        public AllocatorManager.AllocatorHandle Handle { get => m_handle; set => m_handle = value; }
        public Allocator ToAllocator => m_handle.ToAllocator;
        public bool IsCustomAllocator => m_handle.IsCustomAllocator;

        internal AllocatorManager.AllocatorHandle m_handle;
        internal AllocatorManager.AllocatorHandle m_parent;

        // Live blocks: pointer -> size in bytes. Allocated from Persistent, never from itself.
        internal UnsafeParallelHashMap<IntPtr, int> m_live;

        public int AllocCount;
        public int FreeCount;
        public int DoubleFreeCount;

        public void Initialize(AllocatorManager.AllocatorHandle parent)
        {
            m_parent = parent;
            m_live = new UnsafeParallelHashMap<IntPtr, int>(64, Allocator.Persistent);
            AllocCount = 0;
            FreeCount = 0;
            DoubleFreeCount = 0;
        }

        public unsafe int Try(ref AllocatorManager.Block block)
        {
            bool isFree = block.Range.Pointer != IntPtr.Zero && block.Bytes == 0;

            if (isFree)
            {
                var ptr = block.Range.Pointer;
                if (m_live.TryGetValue(ptr, out var size))
                {
                    UnsafeUtility.MemSet((void*)ptr, 0xDD, size);
                    m_live.Remove(ptr);
                    ++FreeCount;
                }
                else
                {
                    ++DoubleFreeCount;
                }
            }

            var temp = block.Range.Allocator;
            block.Range.Allocator = m_parent;
            var error = AllocatorManager.Try(ref block);
            block.Range.Allocator = temp;
            if (error != 0)
                return error;

            if (block.Range.Pointer != IntPtr.Zero && !isFree)
            {
                m_live.TryAdd(block.Range.Pointer, (int)block.Bytes);
                ++AllocCount;
            }

            return 0;
        }

        public bool IsLive(IntPtr ptr) => m_live.ContainsKey(ptr);

        /// <summary>Blocks that are still alive: allocated and not yet freed.</summary>
        public int LiveCount => m_live.IsCreated ? m_live.Count() : 0;

        public AllocatorManager.TryFunction Function => Try;

        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        public static unsafe int Try(IntPtr state, ref AllocatorManager.Block block)
            => ((TrackingAllocator*)state)->Try(ref block);

        public void Dispose()
        {
            if (m_live.IsCreated)
                m_live.Dispose();
            m_handle.Dispose();
        }
    }
}
