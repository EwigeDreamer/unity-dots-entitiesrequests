using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using ED.DOTS.EntitiesRequests;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.MultiSystemTestRequest))]

namespace ED.DOTS.EntitiesRequests.Tests
{
    public struct MultiSystemTestRequest
    {
        public int Value;
        public int WriterId;
    }

    [TestFixture]
    public class MultiSystemMultiWriterTests : ECSTestBase
    {
        protected override void RegisterRequestSystems(World world)
        {
            GetOrAddRequestSystem<MultiSystemTestRequest_RequestSystem>();
        }

        // --- Explicit system types for each writer ---

        [DisableAutoCreation]
        public partial class WriterSystem1 : SystemBase
        {
            public int WriterId = 1;
            public int RequestCount = 500;
            private RequestWriter<MultiSystemTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<MultiSystemTestRequest>(RequestCount);
            }
            protected override void OnDestroy() => _writer.Dispose();
            protected override void OnUpdate()
            {
                for (int i = 0; i < RequestCount; i++)
                    _writer.Write(new MultiSystemTestRequest { Value = i, WriterId = WriterId });
            }
        }

        [DisableAutoCreation]
        public partial class WriterSystem2 : SystemBase
        {
            public int WriterId = 2;
            public int RequestCount = 200;
            private RequestWriter<MultiSystemTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<MultiSystemTestRequest>(RequestCount);
            }
            protected override void OnDestroy() => _writer.Dispose();
            protected override void OnUpdate()
            {
                for (int i = 0; i < RequestCount; i++)
                    _writer.Write(new MultiSystemTestRequest { Value = i, WriterId = WriterId });
            }
        }

        [DisableAutoCreation]
        public partial class WriterSystem3 : SystemBase
        {
            public int WriterId = 3;
            public int RequestCount = 300;
            private RequestWriter<MultiSystemTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<MultiSystemTestRequest>(RequestCount);
            }
            protected override void OnDestroy() => _writer.Dispose();
            protected override void OnUpdate()
            {
                for (int i = 0; i < RequestCount; i++)
                    _writer.Write(new MultiSystemTestRequest { Value = i, WriterId = WriterId });
            }
        }

        [DisableAutoCreation]
        public partial class ParallelWriterSystem1 : SystemBase
        {
            public int WriterId = 10;
            public int RequestCount = 300;
            private RequestWriter<MultiSystemTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<MultiSystemTestRequest>(RequestCount);
                _writer.EnsureCapacity(RequestCount);
            }
            protected override void OnDestroy() => _writer.Dispose();
            protected override void OnUpdate()
            {
                var parallelWriter = _writer.AsParallelWriter();
                var job = new ParallelWriteJob { Writer = parallelWriter, WriterId = WriterId };
                Dependency = job.Schedule(RequestCount, 32, Dependency);
            }

            [BurstCompile]
            private struct ParallelWriteJob : IJobParallelFor
            {
                public RequestWriter<MultiSystemTestRequest>.ParallelWriter Writer;
                public int WriterId;
                public void Execute(int index)
                {
                    Writer.WriteNoResize(new MultiSystemTestRequest { Value = index, WriterId = WriterId });
                }
            }
        }

        [DisableAutoCreation]
        public partial class ParallelWriterSystem2 : SystemBase
        {
            public int WriterId = 20;
            public int RequestCount = 300;
            private RequestWriter<MultiSystemTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<MultiSystemTestRequest>(RequestCount);
                _writer.EnsureCapacity(RequestCount);
            }
            protected override void OnDestroy() => _writer.Dispose();
            protected override void OnUpdate()
            {
                var parallelWriter = _writer.AsParallelWriter();
                var job = new ParallelWriteJob { Writer = parallelWriter, WriterId = WriterId };
                Dependency = job.Schedule(RequestCount, 32, Dependency);
            }

            [BurstCompile]
            private struct ParallelWriteJob : IJobParallelFor
            {
                public RequestWriter<MultiSystemTestRequest>.ParallelWriter Writer;
                public int WriterId;
                public void Execute(int index)
                {
                    Writer.WriteNoResize(new MultiSystemTestRequest { Value = index, WriterId = WriterId });
                }
            }
        }

        // Shared reader system (uses NativeHashSet)
        [DisableAutoCreation]
        public partial class TestReaderSystem : SystemBase
        {
            public NativeHashSet<int> ReceivedValues;
            public int ReceivedCount;
            private RequestReader<MultiSystemTestRequest> _reader;

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<MultiSystemTestRequest>();
                ReceivedValues = new NativeHashSet<int>(10000, Allocator.Persistent);
            }
            protected override void OnDestroy() => ReceivedValues.Dispose();
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

        // --- Tests ---

        [Test]
        public void MultipleWriters_IndependentBufferExpansion()
        {
            var writer1 = new WriterSystem1 { WriterId = 1, RequestCount = 500 };
            var writer2 = new WriterSystem2 { WriterId = 2, RequestCount = 200 };
            AddExistingSystemToSimulationManaged(writer1);
            AddExistingSystemToSimulationManaged(writer2);
            var reader = GetOrAddSystemToSimulationManaged<TestReaderSystem>();

            UpdateWorld(1);
            UpdateWorld(1);

            Assert.AreEqual(writer1.RequestCount + writer2.RequestCount, reader.ReceivedCount);
            for (int i = 0; i < writer1.RequestCount; i++)
                Assert.IsTrue(reader.ReceivedValues.Contains(1 * 10000 + i));
            for (int i = 0; i < writer2.RequestCount; i++)
                Assert.IsTrue(reader.ReceivedValues.Contains(2 * 10000 + i));
        }

        [Test]
        public void ReadBufferExpansion_WithManyWriters()
        {
            var writer1 = new WriterSystem1 { WriterId = 1, RequestCount = 200 };
            var writer2 = new WriterSystem2 { WriterId = 2, RequestCount = 200 };
            var writer3 = new WriterSystem3 { WriterId = 3, RequestCount = 200 };
            AddExistingSystemToSimulationManaged(writer1);
            AddExistingSystemToSimulationManaged(writer2);
            AddExistingSystemToSimulationManaged(writer3);
            var reader = GetOrAddSystemToSimulationManaged<TestReaderSystem>();

            UpdateWorld(1);
            UpdateWorld(1);

            Assert.AreEqual(600, reader.ReceivedCount);
            for (int w = 1; w <= 3; w++)
                for (int v = 0; v < 200; v++)
                    Assert.IsTrue(reader.ReceivedValues.Contains(w * 10000 + v));
        }

        [Test]
        public void WriterDispose_RemovesBufferFromRegistry()
        {
            var writer1 = new WriterSystem1 { WriterId = 1, RequestCount = 50 };
            var writer2 = new WriterSystem2 { WriterId = 2, RequestCount = 50 };
            AddExistingSystemToSimulationManaged(writer1);
            AddExistingSystemToSimulationManaged(writer2);
            var reader = GetOrAddSystemToSimulationManaged<TestReaderSystem>();

            // First frame: both writers write, data moved to read buffer
            UpdateWorld(1);
    
            // Destroy writer1 system after first frame
            DestroySystemManaged<WriterSystem1>();
    
            // Second frame: reader clears read buffer (old 100 requests are read and cleared)
            UpdateWorld(1);
            
            Assert.AreEqual(100, reader.ReceivedCount);
            
            // Third frame: writer2 writes its 50, they become available
            UpdateWorld(1);
    
            Assert.AreEqual(50, reader.ReceivedCount);
            
            for (int i = 0; i < 50; i++)
                Assert.IsTrue(reader.ReceivedValues.Contains(2 * 10000 + i));
            for (int i = 0; i < 50; i++)
                Assert.IsFalse(reader.ReceivedValues.Contains(1 * 10000 + i));
        }
    }
}