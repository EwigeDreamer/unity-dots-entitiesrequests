using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using ED.DOTS.EntitiesRequests;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.MultiWriterTestRequest))]

namespace ED.DOTS.EntitiesRequests.Tests
{
    public struct MultiWriterTestRequest
    {
        public int Value;
        public int WriterId;
    }

    [TestFixture]
    public class MultiWriterIntegrationTests : ECSTestBase
    {
        protected override void RegisterRequestSystems(World world)
        {
            GetOrAddRequestSystem<MultiWriterTestRequest_RequestSystem>();
        }

        // A helper writer system that can be instantiated with different IDs and counts
        [DisableAutoCreation]
        public partial class TestWriterSystem : SystemBase
        {
            public int WriterId;
            public int RequestCount;
            private RequestWriter<MultiWriterTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<MultiWriterTestRequest>(RequestCount);
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                for (int i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new MultiWriterTestRequest { Value = i, WriterId = WriterId });
                }
            }
        }

        // Helper parallel writer system using IJobParallelFor
        [DisableAutoCreation]
        public partial class TestParallelWriterSystem : SystemBase
        {
            public int WriterId;
            public int RequestCount;
            private RequestWriter<MultiWriterTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<MultiWriterTestRequest>(RequestCount);
                _writer.EnsureCapacity(RequestCount);
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                var parallelWriter = _writer.AsParallelWriter();
                var job = new ParallelWriteJob
                {
                    Writer = parallelWriter,
                    WriterId = WriterId,
                    RequestCount = RequestCount
                };
                Dependency = job.Schedule(RequestCount, 32, Dependency);
            }

            [BurstCompile]
            private struct ParallelWriteJob : IJobParallelFor
            {
                public RequestWriter<MultiWriterTestRequest>.ParallelWriter Writer;
                public int WriterId;
                public int RequestCount;

                public void Execute(int index)
                {
                    Writer.WriteNoResize(new MultiWriterTestRequest { Value = index, WriterId = WriterId });
                }
            }
        }

        // Reader system that accumulates all requests into a set
        [DisableAutoCreation]
        public partial class TestReaderSystem : SystemBase
        {
            public NativeHashSet<int> ReceivedValues;
            public int ReceivedCount;

            private RequestReader<MultiWriterTestRequest> _reader;

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<MultiWriterTestRequest>();
                ReceivedValues = new NativeHashSet<int>(10000, Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                ReceivedValues.Dispose();
            }

            protected override void OnUpdate()
            {
                ReceivedValues.Clear();
                ReceivedCount = 0;
                foreach (var req in _reader.Read())
                {
                    ReceivedCount++;
                    ReceivedValues.Add(req.WriterId * 10000 + req.Value);
                }
                _reader.Clear();
            }
        }

        [Test]
        public void MixedSyncAndParallelWriting_AllRequestsReceived()
        {
            var syncWriter = new TestWriterSystem();
            var parallelWriter = new TestParallelWriterSystem();

            syncWriter.WriterId = 1;
            syncWriter.RequestCount = 300;
            parallelWriter.WriterId = 2;
            parallelWriter.RequestCount = 300;

            AddExistingSystemToSimulationManaged(syncWriter);
            AddExistingSystemToSimulationManaged(parallelWriter);

            var reader = GetOrAddSystemToSimulationManaged<TestReaderSystem>();

            UpdateWorld(1);
            UpdateWorld(1);

            Assert.AreEqual(600, reader.ReceivedCount);
            for (int i = 0; i < 300; i++)
            {
                Assert.IsTrue(reader.ReceivedValues.Contains(1 * 10000 + i));
                Assert.IsTrue(reader.ReceivedValues.Contains(2 * 10000 + i));
            }
        }
    }
}