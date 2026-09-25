using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests.Managed
{
    /// <summary>
    /// Two independent request types living in one world, each with its own bank, owner, writer and
    /// reader. The counts and payloads differ on purpose, so any cross-contamination between the two
    /// banks would break the assertions.
    /// </summary>
    [TestFixture]
    public sealed class NewMultiTypeTests : RequestTestBase
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
            Assert.That(World.GetExistingSystemManaged<ReaderOneSystem>().ReceivedCount, Is.EqualTo(0));
            Assert.That(World.GetExistingSystemManaged<ReaderTwoSystem>().ReceivedCount, Is.EqualTo(0));

            UpdateWorld(1);

            var readerOne = World.GetExistingSystemManaged<ReaderOneSystem>();
            var readerTwo = World.GetExistingSystemManaged<ReaderTwoSystem>();

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
        public partial class WriterOneSystem : SystemBase
        {
            public const int RequestCount = 3;
            public const int WrittenValue = 11;

            private RequestWriter<TestRequest_1> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<TestRequest_1>();
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new TestRequest_1 { Value = WrittenValue });
                }
            }
        }

        [DisableAutoCreation]
        public partial class ReaderOneSystem : SystemBase
        {
            private RequestReader<TestRequest_1> _reader;

            public int ReceivedCount;
            public int ReceivedSum;

            protected override void OnCreate() => _reader = this.GetRequestReader<TestRequest_1>();
            protected override void OnDestroy() => _reader.Dispose();

            protected override void OnUpdate()
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
        public partial class WriterTwoSystem : SystemBase
        {
            public const int RequestCount = 5;
            public const int WrittenValue = 22;

            private RequestWriter<TestRequest_2> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<TestRequest_2>();
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new TestRequest_2 { Value = WrittenValue });
                }
            }
        }

        [DisableAutoCreation]
        public partial class ReaderTwoSystem : SystemBase
        {
            private RequestReader<TestRequest_2> _reader;

            public int ReceivedCount;
            public int ReceivedSum;

            protected override void OnCreate() => _reader = this.GetRequestReader<TestRequest_2>();
            protected override void OnDestroy() => _reader.Dispose();

            protected override void OnUpdate()
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
