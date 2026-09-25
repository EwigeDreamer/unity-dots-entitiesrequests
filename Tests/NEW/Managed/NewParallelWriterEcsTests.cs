using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests.Managed
{
    /// <summary>
    /// Parallel write from a system and read from a system, in a world. Ported from
    /// <c>ParallelWriterTests</c> ECS case.
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
            Assert.That(World.GetExistingSystemManaged<ParallelReaderTestSystem>().ReceivedCount, Is.EqualTo(0));

            UpdateWorld(1);
            Assert.That(World.GetExistingSystemManaged<ParallelReaderTestSystem>().ReceivedCount,
                Is.EqualTo(ParallelWriterTestSystem.RequestCount));
        }

        [DisableAutoCreation]
        public partial class ParallelWriterTestSystem : SystemBase
        {
            public const int RequestCount = 100;

            private RequestWriter<TestRequest_1> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<TestRequest_1>(RequestCount);
                _writer.EnsureCapacity(RequestCount);
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter() };
                Dependency = job.Schedule(RequestCount, 8, Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class ParallelReaderTestSystem : SystemBase
        {
            private RequestReader<TestRequest_1> _reader;

            public int ReceivedCount { get; private set; }

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<TestRequest_1>();
            }

            protected override void OnDestroy()
            {
                _reader.Dispose();
            }

            protected override void OnUpdate()
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
