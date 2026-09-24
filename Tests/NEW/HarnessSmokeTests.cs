using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tmp.Tests.HarnessSmokeRequest))]

namespace ED.DOTS.EntitiesRequests.Tmp.Tests
{
    /// <summary>
    /// Request type of the harness smoke fixture. Its only purpose is to make the source generator
    /// emit an owner system for this fixture.
    /// </summary>
    public struct HarnessSmokeRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Verifies the engine contract the harness relies on: the root groups are created, the request
    /// group is nested into the simulation group and ordered after the late simulation group, and the
    /// generated owner is placed inside the request group. The pipeline test additionally runs one
    /// write/merge/read pass, so a green run proves the whole chain executes instead of silently
    /// doing nothing.
    /// </summary>
    [TestFixture]
    public sealed class HarnessSmokeTests : RequestTestBase
    {
        private const int WrittenValue = 7;

        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(HarnessSmokeRequest_RequestSystem));
            systems.Add(typeof(WriterSystem));
            systems.Add(typeof(ReaderSystem));
        }

        [Test]
        public void RequestSystemGroup_SitsInsideSimulationGroup()
        {
            var simulation = World.GetExistingSystemManaged<SimulationSystemGroup>();
            var requestGroup = World.GetExistingSystemManaged<RequestSystemGroup>();

            Assert.That(IndexOfManaged(simulation, requestGroup), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void RequestSystemGroup_OrderedAfterLateSimulationGroup()
        {
            var simulation = World.GetExistingSystemManaged<SimulationSystemGroup>();
            var late = World.GetExistingSystemManaged<LateSimulationSystemGroup>();
            var requestGroup = World.GetExistingSystemManaged<RequestSystemGroup>();

            var lateIndex = IndexOfManaged(simulation, late);
            var requestIndex = IndexOfManaged(simulation, requestGroup);

            Assert.That(lateIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(requestIndex, Is.GreaterThan(lateIndex));
        }

        [Test]
        public void GeneratedOwner_SitsInsideRequestSystemGroup()
        {
            var requestGroup = World.GetExistingSystemManaged<RequestSystemGroup>();
            var ownerTypeIndex = TypeManager.GetSystemTypeIndex<HarnessSmokeRequest_RequestSystem>();

            using var systems = requestGroup.GetAllSystems(Allocator.Temp);
            var found = false;
            for (var i = 0; i < systems.Length; i++)
            {
                if (World.Unmanaged.GetSystemTypeIndex(systems[i]) != ownerTypeIndex)
                {
                    continue;
                }

                found = true;
                break;
            }

            Assert.IsTrue(found, "the generated owner must be placed into RequestSystemGroup");
        }

        [Test]
        public void Pipeline_RunsWriteMergeReadPass()
        {
            UpdateWorld(1);
            Assert.That(GetTestSystem<ReaderSystem>().ReceivedCount, Is.EqualTo(0));

            UpdateWorld(1);
            Assert.That(GetTestSystem<ReaderSystem>().ReceivedCount, Is.EqualTo(1));
        }

        private static int IndexOfManaged(ComponentSystemGroup group, ComponentSystemBase system)
        {
            var managed = group.ManagedSystems;
            for (var i = 0; i < managed.Count; i++)
            {
                if (ReferenceEquals(managed[i], system))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Writes one request per update into its private buffer.</summary>
        [DisableAutoCreation]
        public partial struct WriterSystem : ISystem
        {
            private RequestWriter<HarnessSmokeRequest> _writer;

            /// <inheritdoc/>
            public void OnCreate(ref SystemState state)
            {
                _writer = state.GetRequestWriter<HarnessSmokeRequest>();
            }

            /// <inheritdoc/>
            public void OnDestroy(ref SystemState state)
            {
                _writer.Dispose();
            }

            /// <inheritdoc/>
            public void OnUpdate(ref SystemState state)
            {
                _writer.Write(new HarnessSmokeRequest { Value = WrittenValue });
            }
        }

        /// <summary>Counts the requests merged into the shared read buffer and clears it.</summary>
        [DisableAutoCreation]
        public partial struct ReaderSystem : ISystem
        {
            private RequestReader<HarnessSmokeRequest> _reader;

            /// <summary>Requests read during the last update.</summary>
            public int ReceivedCount;

            /// <inheritdoc/>
            public void OnCreate(ref SystemState state)
            {
                _reader = state.GetRequestReader<HarnessSmokeRequest>();
            }

            /// <inheritdoc/>
            public void OnDestroy(ref SystemState state)
            {
                _reader.Dispose();
            }

            /// <inheritdoc/>
            public void OnUpdate(ref SystemState state)
            {
                ReceivedCount = _reader.Read().Length;
                _reader.Clear();
            }
        }
    }
}
