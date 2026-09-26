using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// Request type of the data integrity core fixture. No ECS registration is needed.
    /// </summary>
    public struct DataIntegrityCoreRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Data integrity of sequential and parallel writes, outside of any world. Ported from
    /// <c>DataIntegrityTests</c> core cases. Values are collected into a set, so parallel write
    /// order does not matter.
    /// </summary>
    [TestFixture]
    public sealed class DataIntegrityCoreTests
    {
        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<DataIntegrityCoreRequest>.ParallelWriter Writer;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new DataIntegrityCoreRequest { Value = index });
            }
        }

        [BurstCompile]
        private struct ParallelForBatchWriteJob : IJobParallelForBatch
        {
            public RequestWriter<DataIntegrityCoreRequest>.ParallelWriter Writer;

            public void Execute(int startIndex, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Writer.WriteNoResize(new DataIntegrityCoreRequest { Value = startIndex + i });
                }
            }
        }

        [Test]
        public void SequentialWrite_DataIntegrity()
        {
            const int requestCount = 500;

            var bank = new RequestBank<DataIntegrityCoreRequest>(Allocator.Persistent, requestCount);
            var writer = new RequestWriter<DataIntegrityCoreRequest>(bank, Allocator.Persistent, requestCount);
            var reader = new RequestReader<DataIntegrityCoreRequest>(bank, Allocator.Persistent);

            for (var i = 0; i < requestCount; i++)
            {
                writer.Write(new DataIntegrityCoreRequest { Value = i });
            }

            bank.Merge();

            var received = new HashSet<int>();
            foreach (var request in reader.Read())
            {
                received.Add(request.Value);
            }

            Assert.That(received.Count, Is.EqualTo(requestCount));
            for (var i = 0; i < requestCount; i++)
            {
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");
            }

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }

        [Test]
        public void ParallelWrite_DataIntegrity()
        {
            const int requestCount = 1000;

            var bank = new RequestBank<DataIntegrityCoreRequest>(Allocator.Persistent, requestCount);
            var writer = new RequestWriter<DataIntegrityCoreRequest>(bank, Allocator.Persistent, requestCount);
            var reader = new RequestReader<DataIntegrityCoreRequest>(bank, Allocator.Persistent);
            writer.EnsureCapacity(requestCount);

            var job = new ParallelWriteJob { Writer = writer.AsParallelWriter() };
            job.Schedule(requestCount, 64).Complete();

            bank.Merge();

            var received = new HashSet<int>();
            foreach (var request in reader.Read())
            {
                received.Add(request.Value);
            }

            Assert.That(received.Count, Is.EqualTo(requestCount));
            for (var i = 0; i < requestCount; i++)
            {
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");
            }

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }

        [Test]
        public void ScheduleParallel_DataIntegrity()
        {
            const int totalCount = 1000;
            const int batchSize = 64;

            var bank = new RequestBank<DataIntegrityCoreRequest>(Allocator.Persistent, totalCount);
            var writer = new RequestWriter<DataIntegrityCoreRequest>(bank, Allocator.Persistent, totalCount);
            var reader = new RequestReader<DataIntegrityCoreRequest>(bank, Allocator.Persistent);
            writer.EnsureCapacity(totalCount);

            var job = new ParallelForBatchWriteJob { Writer = writer.AsParallelWriter() };
            job.ScheduleParallel(totalCount, batchSize).Complete();

            bank.Merge();

            var received = new HashSet<int>();
            foreach (var request in reader.Read())
            {
                received.Add(request.Value);
            }

            Assert.That(received.Count, Is.EqualTo(totalCount));
            for (var i = 0; i < totalCount; i++)
            {
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");
            }

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }
    }
}
