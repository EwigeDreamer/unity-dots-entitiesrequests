using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests.Unmanaged
{
    /// <summary>
    /// Unmanaged mirror of the managed owner lifetime fixture: the generated owner dies <b>first</b>,
    /// while unmanaged writer and reader cards are still alive. The owner must close the bank,
    /// invalidate the live cards and disappear; disposing those cards over the closed bank must not
    /// touch freed memory, and the world must keep running afterwards.
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
            Assert.IsTrue(GetTestSystem<OwnerWriterSystem>().IsCardValid);
            Assert.IsTrue(GetTestSystem<OwnerReaderSystem>().IsCardValid);

            DestroyUnmanagedTestSystem<TestRequest_1_RequestSystem>();

            Assert.That(World.GetExistingSystem<TestRequest_1_RequestSystem>(), Is.EqualTo(SystemHandle.Null),
                "the destroyed owner must be gone");
            Assert.IsFalse(TryGetBank<TestRequest_1>(out _), "the owner must have closed the bank");
            Assert.IsFalse(GetTestSystem<OwnerWriterSystem>().IsCardValid,
                "the writer card must be invalidated");
            Assert.IsFalse(GetTestSystem<OwnerReaderSystem>().IsCardValid,
                "the reader card must be invalidated");

            // Disposing the cards over the closed bank is the path that used to crash: it must not
            // touch freed bank memory, and both systems must be gone afterwards.
            DestroyUnmanagedTestSystem<OwnerWriterSystem>();
            DestroyUnmanagedTestSystem<OwnerReaderSystem>();

            Assert.That(World.GetExistingSystem<OwnerWriterSystem>(), Is.EqualTo(SystemHandle.Null),
                "the destroyed writer must be gone");
            Assert.That(World.GetExistingSystem<OwnerReaderSystem>(), Is.EqualTo(SystemHandle.Null),
                "the destroyed reader must be gone");

            // The world must survive the owner's death: it now holds no request systems at all.
            UpdateWorld(1);
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct OwnerWriterSystem : ISystem
        {
            private RequestWriter<TestRequest_1> _writer;

            public bool IsCardValid => _writer.IsValid;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _writer = state.GetRequestWriter<TestRequest_1>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _writer.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                _writer.Write(new TestRequest_1 { Value = 1 });
            }
        }

        [DisableAutoCreation]
        [BurstCompile]
        public partial struct OwnerReaderSystem : ISystem
        {
            private RequestReader<TestRequest_1> _reader;

            public bool IsCardValid => _reader.IsValid;

            [BurstCompile]
            public void OnCreate(ref SystemState state) => _reader = state.GetRequestReader<TestRequest_1>();

            [BurstCompile]
            public void OnDestroy(ref SystemState state) => _reader.Dispose();

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                _reader.Clear();
            }
        }
    }
}
