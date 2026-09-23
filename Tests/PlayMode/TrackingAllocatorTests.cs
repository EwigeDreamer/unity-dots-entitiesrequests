using System;
using ED.DOTS.EntitiesRequests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// Sanity tests for <see cref="TrackingAllocator"/>, the test-only allocator used by the
    /// use-after-free regression tests. Ensures the detector itself tracks liveness, counts
    /// and poisoning correctly before it is trusted to catch the real bug.
    /// </summary>
    [TestFixture]
    public class TrackingAllocatorTests
    {
        [Test]
        public unsafe void TracksAllocateAndFree()
        {
            var helper = new AllocatorHelper<TrackingAllocator>(AllocatorManager.Persistent);
            ref var alloc = ref helper.Allocator;
            alloc.Initialize(AllocatorManager.Persistent);

            Assert.That(alloc.AllocCount, Is.EqualTo(0));
            Assert.That(alloc.FreeCount, Is.EqualTo(0));
            Assert.That(alloc.DoubleFreeCount, Is.EqualTo(0));

            const int size = 64;
            const int alignment = 16;

            var p1 = AllocatorManager.Allocate(alloc.Handle, size, alignment, 1);
            var p2 = AllocatorManager.Allocate(alloc.Handle, size, alignment, 1);

            Assert.That(alloc.AllocCount, Is.EqualTo(2));
            Assert.That(alloc.IsLive((IntPtr)p1), Is.True);
            Assert.That(alloc.IsLive((IntPtr)p2), Is.True);

            AllocatorManager.Free(alloc.Handle, p1, size, alignment, 1);

            Assert.That(alloc.FreeCount, Is.EqualTo(1));
            Assert.That(alloc.DoubleFreeCount, Is.EqualTo(0));
            Assert.That(alloc.IsLive((IntPtr)p1), Is.False);
            Assert.That(alloc.IsLive((IntPtr)p2), Is.True);

            AllocatorManager.Free(alloc.Handle, p2, size, alignment, 1);

            Assert.That(alloc.FreeCount, Is.EqualTo(2));
            Assert.That(alloc.DoubleFreeCount, Is.EqualTo(0));
            Assert.That(alloc.IsLive((IntPtr)p2), Is.False);

            alloc.Dispose();
            helper.Dispose();
        }

        [Test]
        public unsafe void PoisonsFreedMemory()
        {
            var helper = new AllocatorHelper<TrackingAllocator>(AllocatorManager.Persistent);
            ref var alloc = ref helper.Allocator;
            alloc.Initialize(AllocatorManager.Persistent);

            const int size = 64;
            const int alignment = 16;

            var p = AllocatorManager.Allocate(alloc.Handle, size, alignment, 1);
            AllocatorManager.Free(alloc.Handle, p, size, alignment, 1);

            // The allocator poisons freed memory with 0xDD before delegating the free.
            // Reading it here is intentional: it verifies the poison marker is in place.
            Assert.That(((byte*)p)[0], Is.EqualTo(0xDD));
            Assert.That(((byte*)p)[size - 1], Is.EqualTo(0xDD));

            alloc.Dispose();
            helper.Dispose();
        }
    }
}
