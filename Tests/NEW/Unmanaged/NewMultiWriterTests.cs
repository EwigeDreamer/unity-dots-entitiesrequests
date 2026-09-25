using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests.Unmanaged
{
    /// <summary>
    /// Unmanaged mirror of the managed multi writer fixture: the user systems are
    /// <see cref="ISystem"/> and every system method is Burst compiled, while the request type and the
    /// generated owner are shared with the managed set. Writer parameters are configured after setup,
    /// before the first update, because the harness creates systems in batch.
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

            GetTestSystem<SyncWriterSystem>().WriterId = 1;
            GetTestSystem<SyncWriterSystem>().RequestCount = writerCount;

            GetTestSystem<ParallelWriterSystem>().WriterId = 2;
            GetTestSystem<ParallelWriterSystem>().RequestCount = writerCount;

            UpdateWorld(2);

            ref var reader = ref GetTestSystem<ReaderSystem>();
            Assert.That(reader.ReceivedCount, Is.EqualTo(writerCount * 2));
            for (var i = 0; i < writerCount; i++)
            {
                Assert.IsTrue(reader.ReceivedValues.Contains(1 * 10000 + i));
                Assert.IsTrue(reader.ReceivedValues.Contains(2 * 10000 + i));
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct SyncWriterSystem : ISystem
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<TaggedTestRequest> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                _writer = state.GetRequestWriter<TaggedTestRequest>();
            }

            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                _writer.Dispose();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new TaggedTestRequest { Value = i, WriterId = WriterId });
                }
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct ParallelWriterSystem : ISystem
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<TaggedTestRequest> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                _writer = state.GetRequestWriter<TaggedTestRequest>();
            }

            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                _writer.Dispose();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                _writer.EnsureCapacity(RequestCount);
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter(), WriterId = WriterId };
                state.Dependency = job.Schedule(RequestCount, 32, state.Dependency);
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct ReaderSystem : ISystem
        {
            private RequestReader<TaggedTestRequest> _reader;

            public NativeHashSet<int> ReceivedValues;
            public int ReceivedCount;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                _reader = state.GetRequestReader<TaggedTestRequest>();
                ReceivedValues = new NativeHashSet<int>(10000, Allocator.Persistent);
            }

            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                ReceivedValues.Dispose();
                _reader.Dispose();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
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
