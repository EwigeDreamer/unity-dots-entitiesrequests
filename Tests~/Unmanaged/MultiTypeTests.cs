using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tests.Unmanaged
{
    /// <summary>
    /// Unmanaged mirror of the multi type fixture: two independent request types living in one world,
    /// each with its own bank, owner, unmanaged writer and reader. Every system method is Burst
    /// compiled. The counts and payloads differ on purpose, so any cross-contamination between the two
    /// banks would break the assertions.
    /// </summary>
    [TestFixture]
    public sealed class MultiTypeTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(WriterOneSystem));
            systems.Add(typeof(ReaderOneSystem));
            systems.Add(typeof(WriterTwoSystem));
            systems.Add(typeof(ReaderTwoSystem));
            systems.Add(typeof(TestRequest_1_RequestSystem));
            systems.Add(typeof(TestRequest_2_RequestSystem));
        }

        [Test]
        public void TwoIndependentTypes_InOneWorld_DoNotInterfere()
        {
            Assert.IsTrue(TryGetBank<TestRequest_1>(out _), "the bank of the first type must exist");
            Assert.IsTrue(TryGetBank<TestRequest_2>(out _), "the bank of the second type must exist");

            UpdateWorld(1);
            Assert.That(GetTestSystem<ReaderOneSystem>().ReceivedCount, Is.EqualTo(0));
            Assert.That(GetTestSystem<ReaderTwoSystem>().ReceivedCount, Is.EqualTo(0));

            UpdateWorld(1);

            ref var readerOne = ref GetTestSystem<ReaderOneSystem>();
            ref var readerTwo = ref GetTestSystem<ReaderTwoSystem>();

            Assert.That(readerOne.ReceivedCount, Is.EqualTo(WriterOneSystem.RequestCount),
                "the first reader must see only the requests of the first type");
            Assert.That(readerOne.ReceivedSum, Is.EqualTo(WriterOneSystem.RequestCount * WriterOneSystem.WrittenValue));

            Assert.That(readerTwo.ReceivedCount, Is.EqualTo(WriterTwoSystem.RequestCount),
                "the second reader must see only the requests of the second type");
            Assert.That(readerTwo.ReceivedSum, Is.EqualTo(WriterTwoSystem.RequestCount * WriterTwoSystem.WrittenValue));

            Assert.That(readerOne.ReceivedCount, Is.Not.EqualTo(readerTwo.ReceivedCount),
                "the two types intentionally write different counts");
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct WriterOneSystem : ISystem
        {
            public const int RequestCount = 3;
            public const int WrittenValue = 11;

            private RequestWriter<TestRequest_1> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TestRequest_1>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new TestRequest_1 { Value = WrittenValue });
                }
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct ReaderOneSystem : ISystem
        {
            private RequestReader<TestRequest_1> _reader;

            public int ReceivedCount;
            public int ReceivedSum;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _reader = state.GetRequestReader<TestRequest_1>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _reader.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                ReceivedCount = 0;
                ReceivedSum = 0;
                foreach (var request in _reader.Read())
                {
                    ReceivedCount++;
                    ReceivedSum += request.Value;
                }
                _reader.Clear();
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct WriterTwoSystem : ISystem
        {
            public const int RequestCount = 5;
            public const int WrittenValue = 22;

            private RequestWriter<TestRequest_2> _writer;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TestRequest_2>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new TestRequest_2 { Value = WrittenValue });
                }
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct ReaderTwoSystem : ISystem
        {
            private RequestReader<TestRequest_2> _reader;

            public int ReceivedCount;
            public int ReceivedSum;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _reader = state.GetRequestReader<TestRequest_2>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _reader.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                ReceivedCount = 0;
                ReceivedSum = 0;
                foreach (var request in _reader.Read())
                {
                    ReceivedCount++;
                    ReceivedSum += request.Value;
                }
                _reader.Clear();
            }
        }
    }
}
