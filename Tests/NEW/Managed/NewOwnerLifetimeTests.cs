using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests.Managed
{
    /// <summary>
    /// The generated owner dying <b>first</b>, while writer and reader cards are still alive — the
    /// order that used to crash the client build. The owner must close the bank, invalidate the live
    /// cards and disappear; disposing those cards over the closed bank must not touch freed memory,
    /// and the world must keep running afterwards.
    /// </summary>
    [TestFixture]
    public sealed class NewOwnerLifetimeTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(OwnerWriterSystem));
            systems.Add(typeof(OwnerReaderSystem));
            systems.Add(typeof(TestRequest_1_RequestSystem));
        }

        [Test]
        public void OwnerDestroyedFirst_ClosesBankAndInvalidatesLiveCards()
        {
            Assert.IsTrue(TryGetBank<TestRequest_1>(out _), "setup must create the bank");
            Assert.IsTrue(World.GetExistingSystemManaged<OwnerWriterSystem>().IsCardValid);
            Assert.IsTrue(World.GetExistingSystemManaged<OwnerReaderSystem>().IsCardValid);

            DestroyUnmanagedTestSystem<TestRequest_1_RequestSystem>();

            Assert.That(World.GetExistingSystem<TestRequest_1_RequestSystem>(), Is.EqualTo(SystemHandle.Null),
                "the destroyed owner must be gone");
            Assert.IsFalse(TryGetBank<TestRequest_1>(out _), "the owner must have closed the bank");
            Assert.IsFalse(World.GetExistingSystemManaged<OwnerWriterSystem>().IsCardValid,
                "the writer card must be invalidated");
            Assert.IsFalse(World.GetExistingSystemManaged<OwnerReaderSystem>().IsCardValid,
                "the reader card must be invalidated");

            // Disposing the cards over the closed bank is the path that used to crash: it must not
            // touch freed bank memory, and both systems must be gone afterwards.
            DestroyManagedTestSystem<OwnerWriterSystem>();
            DestroyManagedTestSystem<OwnerReaderSystem>();

            Assert.IsNull(World.GetExistingSystemManaged<OwnerWriterSystem>(), "the destroyed writer must be gone");
            Assert.IsNull(World.GetExistingSystemManaged<OwnerReaderSystem>(), "the destroyed reader must be gone");

            // The world must survive the owner's death: it now holds no request systems at all.
            UpdateWorld(1);
        }

        [DisableAutoCreation]
        public partial class OwnerWriterSystem : SystemBase
        {
            private RequestWriter<TestRequest_1> _writer;

            public bool IsCardValid => _writer.IsValid;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<TestRequest_1>();
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                _writer.Write(new TestRequest_1 { Value = 1 });
            }
        }

        [DisableAutoCreation]
        public partial class OwnerReaderSystem : SystemBase
        {
            private RequestReader<TestRequest_1> _reader;

            public bool IsCardValid => _reader.IsValid;

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
                _reader.Clear();
            }
        }
    }
}
