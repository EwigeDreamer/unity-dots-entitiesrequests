using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests.Managed
{
    /// <summary>
    /// One synchronous and one parallel writer feeding the same bank. Ported from
    /// <c>MultiWriterIntegrationTests</c>. Writer parameters are configured after setup, before the
    /// first update, because the harness creates systems in batch.
    /// </summary>
    [TestFixture]
    public sealed class NewMultiWriterTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(SyncWriterSystem));
            systems.Add(typeof(ParallelWriterSystem));
            systems.Add(typeof(ReaderSystem));
            systems.Add(typeof(TaggedTestRequest_RequestSystem));
        }

        [Test]
        public void MixedSyncAndParallelWriting_AllRequestsReceived()
        {
            const int writerCount = 300;

            var syncWriter = World.GetExistingSystemManaged<SyncWriterSystem>();
            syncWriter.WriterId = 1;
            syncWriter.RequestCount = writerCount;

            var parallelWriter = World.GetExistingSystemManaged<ParallelWriterSystem>();
            parallelWriter.WriterId = 2;
            parallelWriter.RequestCount = writerCount;

            UpdateWorld(2);

            var reader = World.GetExistingSystemManaged<ReaderSystem>();
            Assert.That(reader.ReceivedCount, Is.EqualTo(writerCount * 2));
            for (var i = 0; i < writerCount; i++)
            {
                Assert.IsTrue(reader.ReceivedValues.Contains(1 * 10000 + i));
                Assert.IsTrue(reader.ReceivedValues.Contains(2 * 10000 + i));
            }
        }

        [DisableAutoCreation]
        public partial class SyncWriterSystem : SystemBase
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<TaggedTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<TaggedTestRequest>();
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new TaggedTestRequest { Value = i, WriterId = WriterId });
                }
            }
        }

        [DisableAutoCreation]
        public partial class ParallelWriterSystem : SystemBase
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<TaggedTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<TaggedTestRequest>();
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                _writer.EnsureCapacity(RequestCount);
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter(), WriterId = WriterId };
                Dependency = job.Schedule(RequestCount, 32, Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class ReaderSystem : SystemBase
        {
            private RequestReader<TaggedTestRequest> _reader;

            public NativeHashSet<int> ReceivedValues;
            public int ReceivedCount;

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<TaggedTestRequest>();
                ReceivedValues = new NativeHashSet<int>(10000, Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                ReceivedValues.Dispose();
                _reader.Dispose();
            }

            protected override void OnUpdate()
            {
                ReceivedValues.Clear();
                ReceivedCount = 0;
                foreach (var request in _reader.Read())
                {
                    ReceivedCount++;
                    ReceivedValues.Add(request.WriterId * 10000 + request.Value);
                }
                _reader.Clear();
            }
        }

        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<TaggedTestRequest>.ParallelWriter Writer;
            public int WriterId;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new TaggedTestRequest { Value = index, WriterId = WriterId });
            }
        }
    }
}
