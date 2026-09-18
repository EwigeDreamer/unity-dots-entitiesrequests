using System;
using ED.DOTS.EntitiesRequests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// Deterministic use-after-free regression tests. The shared <see cref="RequestsData{T}"/>
    /// block must not be freed while any writer still owns it (deferred-free contract).
    /// A tracking allocator observes whether the block is live at each step, so the test is
    /// red before the lifetime fix and green after it.
    /// </summary>
    [TestFixture]
    public class RequestsDeferredFreeTests
    {
        [Test]
        public unsafe void OwnerDispose_DoesNotFreeSharedBlock_WhileWriterAlive()
        {
            var helper = new AllocatorHelper<TrackingAllocator>(AllocatorManager.Persistent);
            ref var alloc = ref helper.Allocator;
            alloc.Initialize(AllocatorManager.Persistent);

            var requests = new Requests<LifetimeRequest>(16, alloc.Handle);
            var data = (IntPtr)requests.GetUnsafeData();
            Assert.That(alloc.IsLive(data), Is.True, "shared block must be live after creation");

            var writer = requests.GetWriter(16);

            requests.Dispose(); // owner closes first

            // Red before the fix: Requests.Dispose frees the shared block immediately,
            // even though a writer still holds a pointer to it.
            Assert.That(alloc.IsLive(data), Is.True,
                "use-after-free: shared block was freed while a writer still owns it");

            writer.Dispose(); // last writer unregisters

            Assert.That(alloc.IsLive(data), Is.False, "block must be freed after the last writer");
            Assert.That(alloc.DoubleFreeCount, Is.EqualTo(0));

            requests.Dispose(); // idempotent repeated owner dispose

            Assert.That(alloc.DoubleFreeCount, Is.EqualTo(0));

            alloc.Dispose();
            helper.Dispose();
        }

        [Test]
        public unsafe void WriterDisposeFirst_KeepsBlockUntilOwnerDispose()
        {
            var helper = new AllocatorHelper<TrackingAllocator>(AllocatorManager.Persistent);
            ref var alloc = ref helper.Allocator;
            alloc.Initialize(AllocatorManager.Persistent);

            var requests = new Requests<LifetimeRequest>(16, alloc.Handle);
            var data = (IntPtr)requests.GetUnsafeData();

            var writer = requests.GetWriter(16);
            writer.Dispose(); // writer unregisters first

            Assert.That(alloc.IsLive(data), Is.True,
                "block must stay live until the owner also disposes");

            requests.Dispose();

            Assert.That(alloc.IsLive(data), Is.False);
            Assert.That(alloc.DoubleFreeCount, Is.EqualTo(0));

            alloc.Dispose();
            helper.Dispose();
        }
    }
}
