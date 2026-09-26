using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// Request type of the parallel writer core fixture. No ECS registration is needed: the bank is
    /// driven directly.
    /// </summary>
    public struct ParallelWriterCoreRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Parallel write into a single writer buffer, outside of any world. Ported from
    /// <c>ParallelWriterTests</c> core cases.
    /// </summary>
    [TestFixture]
    public sealed class ParallelWriterCoreTests
    {
        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<ParallelWriterCoreRequest>.ParallelWriter Writer;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new ParallelWriterCoreRequest { Value = index });
            }
        }

        [Test]
        public void ParallelWrite_ThenRead_AllRequestsReceived()
        {
            const int requestCount = 1000;

            var bank = new RequestBank<ParallelWriterCoreRequest>(Allocator.Persistent, requestCount);
            var writer = new RequestWriter<ParallelWriterCoreRequest>(bank, Allocator.Persistent, requestCount);
            var reader = new RequestReader<ParallelWriterCoreRequest>(bank, Allocator.Persistent);
            writer.EnsureCapacity(requestCount);

            var job = new ParallelWriteJob { Writer = writer.AsParallelWriter() };
            job.Schedule(requestCount, 64).Complete();

            bank.Merge();

            Assert.That(reader.Read().Length, Is.EqualTo(requestCount));

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }

        [Test]
        public void ParallelWriteAndRead_Concurrently_BeforeMerge_ReadsNothing()
        {
            const int requestCount = 100;

            var bank = new RequestBank<ParallelWriterCoreRequest>(Allocator.Persistent, requestCount);
            var writer = new RequestWriter<ParallelWriterCoreRequest>(bank, Allocator.Persistent, requestCount);
            var reader = new RequestReader<ParallelWriterCoreRequest>(bank, Allocator.Persistent);
            writer.EnsureCapacity(requestCount);

            var job = new ParallelWriteJob { Writer = writer.AsParallelWriter() };
            var handle = job.Schedule(requestCount, 64);

            // Reading the shared buffer while the write job is in flight returns nothing: the writes
            // still live in the private writer buffer until the merge.
            Assert.DoesNotThrow(() => Assert.That(reader.Read().Length, Is.EqualTo(0)));

            handle.Complete();
            bank.Merge();

            Assert.That(reader.Read().Length, Is.EqualTo(requestCount));

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }
    }
}
