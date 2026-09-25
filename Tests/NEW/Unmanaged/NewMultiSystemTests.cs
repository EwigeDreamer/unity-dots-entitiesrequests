using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests.Unmanaged
{
    /// <summary>
    /// Unmanaged mirror of the managed multi system fixture: several independent unmanaged writer
    /// systems feed the same bank, with per-test request counts. Parameters are configured after setup,
    /// before the first update. One case destroys a writer mid-run through
    /// <see cref="RequestTestBase.DestroyUnmanagedTestSystem{T}"/> to verify that its buffer leaves the
    /// bank registry and its data does not return.
    /// </summary>
    [TestFixture]
    public sealed class NewMultiSystemTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(WriterSystem1));
            systems.Add(typeof(WriterSystem2));
            systems.Add(typeof(WriterSystem3));
            systems.Add(typeof(ReaderSystem));
            systems.Add(typeof(TaggedTestRequest_RequestSystem));
        }

        [Test]
        public void MultipleWriters_IndependentBufferExpansion()
        {
            GetTestSystem<WriterSystem1>().WriterId = 1;
            GetTestSystem<WriterSystem1>().RequestCount = 500;

            GetTestSystem<WriterSystem2>().WriterId = 2;
            GetTestSystem<WriterSystem2>().RequestCount = 200;

            UpdateWorld(2);

            ref var reader = ref GetTestSystem<ReaderSystem>();
            Assert.That(reader.ReceivedCount,
                Is.EqualTo(GetTestSystem<WriterSystem1>().RequestCount + GetTestSystem<WriterSystem2>().RequestCount));
            for (var i = 0; i < 500; i++)
            {
                Assert.IsTrue(reader.ReceivedValues.Contains(1 * 10000 + i));
            }

            for (var i = 0; i < 200; i++)
            {
                Assert.IsTrue(reader.ReceivedValues.Contains(2 * 10000 + i));
            }
        }

        [Test]
        public void ReadBufferExpansion_WithManyWriters()
        {
            const int count = 200;

            GetTestSystem<WriterSystem1>().WriterId = 1;
            GetTestSystem<WriterSystem1>().RequestCount = count;

            GetTestSystem<WriterSystem2>().WriterId = 2;
            GetTestSystem<WriterSystem2>().RequestCount = count;

            GetTestSystem<WriterSystem3>().WriterId = 3;
            GetTestSystem<WriterSystem3>().RequestCount = count;

            UpdateWorld(2);

            ref var reader = ref GetTestSystem<ReaderSystem>();
            Assert.That(reader.ReceivedCount, Is.EqualTo(count * 3));
            for (var w = 1; w <= 3; w++)
            {
                for (var v = 0; v < count; v++)
                {
                    Assert.IsTrue(reader.ReceivedValues.Contains(w * 10000 + v));
                }
            }
        }

        [Test]
        public void WriterDispose_RemovesBufferFromRegistry()
        {
            const int count = 50;

            GetTestSystem<WriterSystem1>().WriterId = 1;
            GetTestSystem<WriterSystem1>().RequestCount = count;

            GetTestSystem<WriterSystem2>().WriterId = 2;
            GetTestSystem<WriterSystem2>().RequestCount = count;

            UpdateWorld(1);

            DestroyUnmanagedTestSystem<WriterSystem1>();
            Assert.That(World.GetExistingSystem<WriterSystem1>(), Is.EqualTo(SystemHandle.Null),
                "the destroyed writer must be gone");

            UpdateWorld(1);
            Assert.That(GetTestSystem<ReaderSystem>().ReceivedCount, Is.EqualTo(count * 2));

            UpdateWorld(1);

            ref var reader = ref GetTestSystem<ReaderSystem>();
            Assert.That(reader.ReceivedCount, Is.EqualTo(count));
            for (var i = 0; i < count; i++)
            {
                Assert.IsTrue(reader.ReceivedValues.Contains(2 * 10000 + i));
            }

            for (var i = 0; i < count; i++)
            {
                Assert.IsFalse(reader.ReceivedValues.Contains(1 * 10000 + i));
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct WriterSystem1 : ISystem
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<TaggedTestRequest> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TaggedTestRequest>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

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
        public partial struct WriterSystem2 : ISystem
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<TaggedTestRequest> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TaggedTestRequest>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

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
        public partial struct WriterSystem3 : ISystem
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<TaggedTestRequest> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TaggedTestRequest>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

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
    }
}
