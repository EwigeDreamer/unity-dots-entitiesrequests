using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests.Unmanaged
{
    /// <summary>
    /// Unmanaged mirror of the managed parallel writer fixture: the user systems are
    /// <see cref="ISystem"/> and every system method is Burst compiled, while the request type and the
    /// generated owner are shared with the managed set.
    /// </summary>
    [TestFixture]
    public sealed class NewParallelWriterEcsTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(ParallelWriterTestSystem));
            systems.Add(typeof(ParallelReaderTestSystem));
            systems.Add(typeof(TestRequest_1_RequestSystem));
        }

        [Test]
        public void ParallelWrite_InSystem_And_ReadInSystem_Works()
        {
            UpdateWorld(1);
            Assert.That(GetTestSystem<ParallelReaderTestSystem>().ReceivedCount, Is.EqualTo(0));

            UpdateWorld(1);
            Assert.That(GetTestSystem<ParallelReaderTestSystem>().ReceivedCount,
                Is.EqualTo(ParallelWriterTestSystem.RequestCount));
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct ParallelWriterTestSystem : ISystem
        {
            public const int RequestCount = 100;

            private RequestWriter<TestRequest_1> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                _writer = state.GetRequestWriter<TestRequest_1>(RequestCount);
                _writer.EnsureCapacity(RequestCount);
            }

            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                _writer.Dispose();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter() };
                state.Dependency = job.Schedule(RequestCount, 8, state.Dependency);
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct ParallelReaderTestSystem : ISystem
        {
            private RequestReader<TestRequest_1> _reader;

            public int ReceivedCount;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                _reader = state.GetRequestReader<TestRequest_1>();
            }

            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                _reader.Dispose();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                ReceivedCount = _reader.Read().Length;
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
