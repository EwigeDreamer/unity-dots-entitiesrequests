using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using ED.DOTS.EntitiesRequests;
using Unity.Burst;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.ParallelTestRequest))]

namespace ED.DOTS.EntitiesRequests.Tests
{
    public struct ParallelTestRequest
    {
        public int Value;
    }

    [TestFixture]
    public class ParallelWriterTests : ECSTestBase
    {
        protected override void RegisterRequestSystems(World world)
        {
            GetOrAddRequestSystem<ParallelTestRequest_RequestSystem>();
        }

        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<ParallelTestRequest>.ParallelWriter Writer;
            public int BaseValue;

            public void Execute(int index)
            {
                var value = BaseValue + index;
                Writer.WriteNoResize(new ParallelTestRequest { Value = value });
            }
        }

        [Test]
        public unsafe void ParallelWrite_ThenRead_AllRequestsReceived()
        {
            const int requestCount = 1000;

            var requests = new Requests<ParallelTestRequest>(requestCount, Allocator.Persistent);
            var writer = requests.GetWriter();

            // Ensure capacity for parallel writes without resize
            requests.EnsureCapacity(requestCount);

            var parallelWriter = writer.AsParallelWriter();

            var job = new ParallelWriteJob
            {
                Writer = parallelWriter,
                BaseValue = 0
            };
            var handle = job.Schedule(requestCount, 64);
            handle.Complete();

            // After writing, call Update to move writes to read buffer
            requests.Update();

            var reader = requests.GetReader();
            int count = 0;
            foreach (var req in reader.Read())
            {
                count++;
            }

            Assert.AreEqual(requestCount, count);
            requests.Dispose();
        }

        [Test]
        public unsafe void ParallelWriteAndRead_Concurrently_BeforeUpdate_NoSafetyException()
        {
            const int requestCount = 100;
            var requests = new Requests<ParallelTestRequest>(requestCount, Allocator.Persistent);
            requests.EnsureCapacity(requestCount);

            var writer = requests.GetWriter();
            var parallelWriter = writer.AsParallelWriter();

            // Schedule parallel write job
            var job = new ParallelWriteJob { Writer = parallelWriter, BaseValue = 0 };
            var handle = job.Schedule(requestCount, 64);

            var reader = requests.GetReader();

            // Reading while write job is running should not throw (different buffers)
            Assert.DoesNotThrow(() =>
            {
                int count = 0;
                foreach (var _ in reader.Read())
                {
                    count++;
                }
                // Read buffer is empty because Update hasn't been called
                Assert.AreEqual(0, count);
            });

            handle.Complete();
            requests.Dispose();
        }

        [Test]
        public void ParallelWrite_InSystem_And_ReadInSystem_Works()
        {
            var writerSystem = GetOrAddSystemToSimulationManaged<ParallelWriterTestSystem>();
            var readerSystem = GetOrAddSystemToSimulationManaged<ParallelReaderTestSystem>();

            // First frame: writer schedules job and writes requests
            UpdateWorld(1);
            // Events not yet available for reading (Update hasn't been called? Actually RequestSystemBase updates after CompleteDependency)
            // In our test, RequestSystemGroup updates at end of frame, so after first frame reader sees nothing.
            Assert.AreEqual(0, readerSystem.ReceivedCount);

            // Second frame: reader should see the requests written in first frame
            UpdateWorld(1);
            Assert.AreEqual(ParallelWriterTestSystem.RequestCount, readerSystem.ReceivedCount);
        }

        // --- Test systems for ECS integration ---

        [DisableAutoCreation]
        public partial class ParallelWriterTestSystem : SystemBase
        {
            public const int RequestCount = 100;
            private RequestWriter<ParallelTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<ParallelTestRequest>();
                EntityManager.EnsureRequestBufferCapacity<ParallelTestRequest>(RequestCount);
            }

            protected override void OnUpdate()
            {
                var parallelWriter = _writer.AsParallelWriter();
                var job = new ParallelWriteJob
                {
                    Writer = parallelWriter,
                    BaseValue = 0
                };
                Dependency = job.Schedule(RequestCount, 8, Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class ParallelReaderTestSystem : SystemBase
        {
            private RequestReader<ParallelTestRequest> _reader;
            public int ReceivedCount { get; private set; }

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<ParallelTestRequest>();
            }

            protected override void OnUpdate()
            {
                ReceivedCount = 0;
                foreach (var req in _reader.Read())
                {
                    ReceivedCount++;
                }
                _reader.Clear();
            }
        }
    }
}