using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests.Tests.Unmanaged
{
    /// <summary>
    /// Unmanaged mirror of the managed race condition fixture: several parallel writer systems and one
    /// synchronous writer feed the same bank for many frames, all Burst compiled. Both cases only
    /// require the run to complete without a safety or race failure; the reader keeps the shared buffer
    /// from growing.
    /// </summary>
    [TestFixture]
    public sealed class RaceConditionTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(ParallelWriterSystem));
            systems.Add(typeof(AnotherParallelWriterSystem));
            systems.Add(typeof(SingleWriterSystem));
            systems.Add(typeof(ReaderSystem));
            systems.Add(typeof(TestRequest_1_RequestSystem));
        }

        [Test]
        public void MixedSyncAndParallelWritingToSameBuffer_Works()
        {
            UpdateWorld(10);
            CompleteJobs();
        }

        [Test]
        public void TwoIndependentParallelSystemsWritingToSameBuffer_Works()
        {
            UpdateWorld(10);
            CompleteJobs();
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct ParallelWriterSystem : ISystem
        {
            public const int RequestCount = 100;

            private RequestWriter<TestRequest_1> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TestRequest_1>(RequestCount);

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter() };
                state.Dependency = job.Schedule(RequestCount, 32, state.Dependency);
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct AnotherParallelWriterSystem : ISystem
        {
            public const int RequestCount = 100;

            private RequestWriter<TestRequest_1> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TestRequest_1>(RequestCount);

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter() };
                state.Dependency = job.Schedule(RequestCount, 32, state.Dependency);
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct SingleWriterSystem : ISystem
        {
            private RequestWriter<TestRequest_1> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TestRequest_1>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                _writer.Write(new TestRequest_1 { Value = -1 });
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct ReaderSystem : ISystem
        {
            private RequestReader<TestRequest_1> _reader;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _reader = state.GetRequestReader<TestRequest_1>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _reader.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                foreach (var _ in _reader.Read())
                {
                }
                _reader.Clear();
            }
        }

        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<TestRequest_1>.ParallelWriter Writer;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new TestRequest_1 { Value = index });
            }
        }
    }
}
