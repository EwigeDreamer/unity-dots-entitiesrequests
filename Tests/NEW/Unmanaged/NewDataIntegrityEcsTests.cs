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
    /// Unmanaged mirror of the managed data integrity fixture: the user systems are
    /// <see cref="ISystem"/> and every system method is Burst compiled, while the request type and the
    /// generated owner are shared with the managed set.
    /// </summary>
    [TestFixture]
    public sealed class NewDataIntegrityEcsTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(DataIntegrityWriterSystem));
            systems.Add(typeof(DataIntegrityReaderSystem));
            systems.Add(typeof(TestRequest_1_RequestSystem));
        }

        [Test]
        public void InSystem_ParallelWrite_DataIntegrity()
        {
            UpdateWorld(1);
            Assert.That(GetTestSystem<DataIntegrityReaderSystem>().ReceivedSet.Count, Is.EqualTo(0));

            UpdateWorld(1);

            var received = GetTestSystem<DataIntegrityReaderSystem>().ReceivedSet;
            Assert.That(received.Count, Is.EqualTo(DataIntegrityWriterSystem.RequestCount));
            for (var i = 0; i < DataIntegrityWriterSystem.RequestCount; i++)
            {
                Assert.IsTrue(received.Contains(i), $"Missing value {i}");
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct DataIntegrityWriterSystem : ISystem
        {
            public const int RequestCount = 800;

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
                state.Dependency = job.Schedule(RequestCount, 64, state.Dependency);
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct DataIntegrityReaderSystem : ISystem
        {
            private RequestReader<TestRequest_1> _reader;

            public NativeHashSet<int> ReceivedSet;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                _reader = state.GetRequestReader<TestRequest_1>();
                ReceivedSet = new NativeHashSet<int>(1000, Allocator.Persistent);
            }

            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                ReceivedSet.Dispose();
                _reader.Dispose();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
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
            public RequestWriter<TestRequest_1>.ParallelWriter Writer;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new TestRequest_1 { Value = index });
            }
        }
    }
}
