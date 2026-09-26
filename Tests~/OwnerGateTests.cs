using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// The bank gate: a registered request type whose generated owner is not part of the world must
    /// not get a bank. Such a bank would never be merged and never closed, because the owner is the
    /// only one who does either, so the helper refuses it and hands out an uncreated bank, which
    /// turns every card into a logged no-op.
    /// <para>
    /// The card is taken from a Burst compiled <c>OnCreate</c> — the protocol allows cards there and
    /// nowhere else — so the gate runs on the real path, error log included, and the log is expected.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class OwnerGateTests : RequestTestBase
    {
        private const string OwnerMissingMessage =
            "[Requests] Request bank not created: the owner system of the request type is not present in this world.";

        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            // Nothing on purpose: the generated owner of TestRequest_1 is left out of the world, and
            // the writer below is created by the test itself.
        }

        [Test]
        public void BankRefused_WhenOwnerNotPresentInWorld()
        {
            Assert.That(World.GetExistingSystem<TestRequest_1_RequestSystem>(), Is.EqualTo(SystemHandle.Null),
                "this fixture's setup must not create the owner");

            LogAssert.Expect(LogType.Error, OwnerMissingMessage);

            ref var system = ref GetTestSystem<OwnerMissingWriterSystem>();

            // Bridge contract, not gate behavior: the owner index must survive the hop from the
            // managed publisher in this assembly to the Burst reader in the package assembly.
            Assert.That(system.BurstReadOwnerIndex, Is.EqualTo(system.OwnerSystemTypeIndex),
                $"the bridge must hold the owner index when read from Burst (read {system.BurstReadOwnerIndex}, expected {system.OwnerSystemTypeIndex})");
            Assert.IsFalse(system.IsCardValid, "the card must be invalid when the bank was refused");
            Assert.IsFalse(TryGetBank<TestRequest_1>(out _), "no bank singleton may be created");
        }

        /// <summary>
        /// Takes a writer card in <c>OnCreate</c>, which is the only place the protocol allows it:
        /// a card taken later could allocate a fresh bank after the previous one died.
        /// </summary>
        [DisableAutoCreation]
        [BurstCompile]
        public partial struct OwnerMissingWriterSystem : ISystem
        {
            private RequestWriter<TestRequest_1> _writer;

            /// <summary>Owner index the bridge holds when read from Burst.</summary>
            public int BurstReadOwnerIndex;

            /// <summary>Owner index the type has in this run; the bridge value must match it.</summary>
            public int OwnerSystemTypeIndex;

            /// <summary>Validity of the card taken in <c>OnCreate</c>.</summary>
            public bool IsCardValid => _writer.IsValid;

            /// <inheritdoc/>
            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                var ownerIndex = RequestBridge<TestRequest_1>.Read();
                BurstReadOwnerIndex = ownerIndex.Value;
                OwnerSystemTypeIndex = TypeManager.GetSystemTypeIndex<TestRequest_1_RequestSystem>().Value;

                _writer = state.GetRequestWriter<TestRequest_1>();
            }

            /// <inheritdoc/>
            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                _writer.Dispose();
            }
        }
    }
}
