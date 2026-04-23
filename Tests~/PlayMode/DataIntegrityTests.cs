using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using ED.DOTS.EntitiesRequests;
using Unity.Burst;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.DataIntegrityRequest))]

namespace ED.DOTS.EntitiesRequests.Tests
{
    public struct DataIntegrityRequest
    {
        public int Value;
    }

    [TestFixture]
    public class DataIntegrityTests : ECSTestBase
    {
        protected override void RegisterRequestSystems(World world)
        {
            GetOrAddRequestSystem<DataIntegrityRequest_RequestSystem>();
        }

        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<DataIntegrityRequest>.ParallelWriter Writer;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new DataIntegrityRequest { Value = index });
            }
        }

        [BurstCompile]
        private struct ParallelForBatchWriteJob : IJobParallelForBatch
        {
            public RequestWriter<DataIntegrityRequest>.ParallelWriter Writer;

            public void Execute(int startIndex, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    int index = startIndex + i;
                    Writer.WriteNoResize(new DataIntegrityRequest { Value = index });
                }
            }
        }

        [Test]
        public unsafe void SequentialWrite_DataIntegrity()
        {
            const int requestCount = 500;
            var requests = new Requests<DataIntegrityRequest>(requestCount, Allocator.Persistent);
            var writer = requests.GetWriter();

            for (int i = 0; i < requestCount; i++)
            {
                writer.Write(new DataIntegrityRequest { Value = i });
            }

            requests.Update();

            var reader = requests.GetReader();
            var received = new HashSet<int>();
            foreach (var req in reader.Read())
                received.Add(req.Value);

            Assert.AreEqual(requestCount, received.Count);
            for (int i = 0; i < requestCount; i++)
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");

            reader.Clear();
            requests.Dispose();
        }

        [Test]
        public unsafe void ParallelWrite_DataIntegrity()
        {
            const int requestCount = 1000;
            var requests = new Requests<DataIntegrityRequest>(requestCount, Allocator.Persistent);
            requests.EnsureCapacity(requestCount);

            var writer = requests.GetWriter();
            var parallelWriter = writer.AsParallelWriter();

            var job = new ParallelWriteJob { Writer = parallelWriter };
            var handle = job.Schedule(requestCount, 64);
            handle.Complete();

            requests.Update();

            var reader = requests.GetReader();
            var received = new HashSet<int>();
            foreach (var req in reader.Read())
                received.Add(req.Value);

            Assert.AreEqual(requestCount, received.Count);
            for (int i = 0; i < requestCount; i++)
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");

            reader.Clear();
            requests.Dispose();
        }

        [Test]
        public unsafe void ScheduleParallel_DataIntegrity()
        {
            const int totalCount = 1000;
            const int batchSize = 64;

            var requests = new Requests<DataIntegrityRequest>(totalCount, Allocator.Persistent);
            requests.EnsureCapacity(totalCount);

            var writer = requests.GetWriter();
            var parallelWriter = writer.AsParallelWriter();

            var job = new ParallelForBatchWriteJob { Writer = parallelWriter };
            var handle = job.ScheduleParallel(totalCount, batchSize);
            handle.Complete();

            requests.Update();

            var reader = requests.GetReader();
            var received = new HashSet<int>();
            foreach (var req in reader.Read())
                received.Add(req.Value);

            Assert.AreEqual(totalCount, received.Count);
            for (int i = 0; i < totalCount; i++)
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");

            reader.Clear();
            requests.Dispose();
        }

        [DisableAutoCreation]
        public partial class DataIntegrityWriterSystem : SystemBase
        {
            public const int RequestCount = 800;
            private RequestWriter<DataIntegrityRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<DataIntegrityRequest>();
                EntityManager.EnsureRequestBufferCapacity<DataIntegrityRequest>(RequestCount);
            }

            protected override void OnUpdate()
            {
                var parallelWriter = _writer.AsParallelWriter();
                var job = new ParallelWriteJob { Writer = parallelWriter };
                Dependency = job.Schedule(RequestCount, 64, Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class DataIntegrityReaderSystem : SystemBase
        {
            private RequestReader<DataIntegrityRequest> _reader;
            public NativeHashSet<int> ReceivedSet;

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<DataIntegrityRequest>();
                ReceivedSet = new NativeHashSet<int>(1000, Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                ReceivedSet.Dispose();
            }

            protected override void OnUpdate()
            {
                ReceivedSet.Clear();
                foreach (var req in _reader.Read())
                    ReceivedSet.Add(req.Value);
                _reader.Clear();
            }
        }

        [Test]
        public void InSystem_ParallelWrite_DataIntegrity()
        {
            var writerSystem = GetOrAddSystemToSimulationManaged<DataIntegrityWriterSystem>();
            var readerSystem = GetOrAddSystemToSimulationManaged<DataIntegrityReaderSystem>();

            UpdateWorld(1);
            Assert.AreEqual(0, readerSystem.ReceivedSet.Count);

            UpdateWorld(1);
            var received = readerSystem.ReceivedSet;
            int expectedCount = DataIntegrityWriterSystem.RequestCount;
            Assert.AreEqual(expectedCount, received.Count);

            for (int i = 0; i < expectedCount; i++)
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");
        }
    }
}