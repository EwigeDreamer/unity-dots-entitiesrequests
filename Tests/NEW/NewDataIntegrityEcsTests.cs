using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tmp.Tests.DataIntegrityEcsRequest))]

namespace ED.DOTS.EntitiesRequests.Tmp.Tests
{
    /// <summary>Request type of the data integrity ECS fixture.</summary>
    public struct DataIntegrityEcsRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Data integrity of a parallel write performed from a system, in a world. Ported from
    /// <c>DataIntegrityTests</c> ECS case.
    /// </summary>
    [TestFixture]
    public sealed class NewDataIntegrityEcsTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(DataIntegrityWriterSystem));
            systems.Add(typeof(DataIntegrityReaderSystem));
            systems.Add(typeof(DataIntegrityEcsRequest_RequestSystem));
        }

        [Test]
        public void InSystem_ParallelWrite_DataIntegrity()
        {
            UpdateWorld(1);
            Assert.That(World.GetExistingSystemManaged<DataIntegrityReaderSystem>().ReceivedSet.Count, Is.EqualTo(0));

            UpdateWorld(1);

            var received = World.GetExistingSystemManaged<DataIntegrityReaderSystem>().ReceivedSet;
            Assert.That(received.Count, Is.EqualTo(DataIntegrityWriterSystem.RequestCount));
            for (var i = 0; i < DataIntegrityWriterSystem.RequestCount; i++)
            {
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");
            }
        }

        [DisableAutoCreation]
        public partial class DataIntegrityWriterSystem : SystemBase
        {
            public const int RequestCount = 800;

            private RequestWriter<DataIntegrityEcsRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<DataIntegrityEcsRequest>(RequestCount);
                _writer.EnsureCapacity(RequestCount);
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter() };
                Dependency = job.Schedule(RequestCount, 64, Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class DataIntegrityReaderSystem : SystemBase
        {
            private RequestReader<DataIntegrityEcsRequest> _reader;

            public NativeHashSet<int> ReceivedSet;

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<DataIntegrityEcsRequest>();
                ReceivedSet = new NativeHashSet<int>(1000, Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                ReceivedSet.Dispose();
                _reader.Dispose();
            }

            protected override void OnUpdate()
            {
                ReceivedSet.Clear();
                foreach (var request in _reader.Read())
                {
                    ReceivedSet.Add(request.Value);
                }
                _reader.Clear();
            }
        }

        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<DataIntegrityEcsRequest>.ParallelWriter Writer;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new DataIntegrityEcsRequest { Value = index });
            }
        }
    }
}
