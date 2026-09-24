using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tmp.Tests.RaceConditionRequest))]

namespace ED.DOTS.EntitiesRequests.Tmp.Tests
{
    /// <summary>Request type of the race condition fixture.</summary>
    public struct RaceConditionRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Several parallel writer systems and one synchronous writer feeding the same bank for many
    /// frames. Ported from <c>ParallelWriteRaceConditionTest</c>. Both cases only require the run to
    /// complete without a safety or race failure; the reader keeps the shared buffer from growing.
    /// </summary>
    [TestFixture]
    public sealed class NewRaceConditionTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(RaceConditionRequest_RequestSystem));
            systems.Add(typeof(ParallelWriterSystem));
            systems.Add(typeof(AnotherParallelWriterSystem));
            systems.Add(typeof(SingleWriterSystem));
            systems.Add(typeof(ReaderSystem));
        }

        [Test]
        public void MixedSyncAndParallelWritingToSameBuffer_Works()
        {
            UpdateWorld(10);
            CompleteJobs();
        }

        [Test]
        public void TwoIndependentParallelSystemsWritingToSameBuffer_Works()
        {
            UpdateWorld(10);
            CompleteJobs();
        }

        [DisableAutoCreation]
        public partial class ParallelWriterSystem : SystemBase
        {
            public const int RequestCount = 100;

            private RequestWriter<RaceConditionRequest> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<RaceConditionRequest>(RequestCount);
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter() };
                Dependency = job.Schedule(RequestCount, 32, Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class AnotherParallelWriterSystem : SystemBase
        {
            public const int RequestCount = 100;

            private RequestWriter<RaceConditionRequest> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<RaceConditionRequest>(RequestCount);
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter() };
                Dependency = job.Schedule(RequestCount, 32, Dependency);
            }
        }

        [DisableAutoCreation]
        public partial class SingleWriterSystem : SystemBase
        {
            private RequestWriter<RaceConditionRequest> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<RaceConditionRequest>();
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                _writer.Write(new RaceConditionRequest { Value = -1 });
            }
        }

        [DisableAutoCreation]
        public partial class ReaderSystem : SystemBase
        {
            private RequestReader<RaceConditionRequest> _reader;

            protected override void OnCreate() => _reader = this.GetRequestReader<RaceConditionRequest>();
            protected override void OnDestroy() => _reader.Dispose();

            protected override void OnUpdate()
            {
                foreach (var _ in _reader.Read())
                {
                }
                _reader.Clear();
            }
        }

        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<RaceConditionRequest>.ParallelWriter Writer;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new RaceConditionRequest { Value = index });
            }
        }
    }
}
