using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests;
using NUnit.Framework;
using Unity.Entities;
using CollisionOwnerOne = ED.DOTS.EntitiesRequests.Tests.CollisionOne.CollisionRequest_RequestSystem;
using CollisionOwnerTwo = ED.DOTS.EntitiesRequests.Tests.CollisionTwo.CollisionRequest_RequestSystem;
using CollisionRequestOne = ED.DOTS.EntitiesRequests.Tests.CollisionOne.CollisionRequest;
using CollisionRequestTwo = ED.DOTS.EntitiesRequests.Tests.CollisionTwo.CollisionRequest;

namespace ED.DOTS.EntitiesRequests.Tests.Managed
{
    /// <summary>
    /// Regression guard for the generated file name. Two request types sharing the same short name in
    /// different namespaces are registered in this one assembly (see CollisionRequests). While the hint
    /// name was derived from the short name alone, the generator emitted two sources under the same
    /// "CollisionRequest_RequestSystem.g.cs" hint and this whole assembly failed to compile. The fixture
    /// therefore proves two things at once: the assembly compiles, and the two generated owners — which
    /// also share the short name and differ only by namespace — stay isolated at runtime.
    /// </summary>
    [TestFixture]
    public sealed class CollisionTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(WriterOneSystem));
            systems.Add(typeof(ReaderOneSystem));
            systems.Add(typeof(WriterTwoSystem));
            systems.Add(typeof(ReaderTwoSystem));
            systems.Add(typeof(CollisionOwnerOne));
            systems.Add(typeof(CollisionOwnerTwo));
        }

        [Test]
        public void SameShortNameInDifferentNamespaces_WorksAndStaysIsolated()
        {
            Assert.IsTrue(TryGetBank<CollisionRequestOne>(out _), "the bank of the first type must exist");
            Assert.IsTrue(TryGetBank<CollisionRequestTwo>(out _), "the bank of the second type must exist");

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

            private RequestWriter<CollisionRequestOne> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<CollisionRequestOne>();
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new CollisionRequestOne { Value = WrittenValue });
                }
            }
        }

        [DisableAutoCreation]
        public partial class ReaderOneSystem : SystemBase
        {
            private RequestReader<CollisionRequestOne> _reader;

            public int ReceivedCount;
            public int ReceivedSum;

            protected override void OnCreate() => _reader = this.GetRequestReader<CollisionRequestOne>();
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

            private RequestWriter<CollisionRequestTwo> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<CollisionRequestTwo>();
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new CollisionRequestTwo { Value = WrittenValue });
                }
            }
        }

        [DisableAutoCreation]
        public partial class ReaderTwoSystem : SystemBase
        {
            private RequestReader<CollisionRequestTwo> _reader;

            public int ReceivedCount;
            public int ReceivedSum;

            protected override void OnCreate() => _reader = this.GetRequestReader<CollisionRequestTwo>();
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
