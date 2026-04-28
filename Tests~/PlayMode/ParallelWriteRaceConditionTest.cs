using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using ED.DOTS.EntitiesRequests;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.RaceTestRequest))]

namespace ED.DOTS.EntitiesRequests.Tests
{
    public struct RaceTestRequest
    {
        public int Value;
    }

    [TestFixture]
    public class ParallelWriteRaceConditionTest : ECSTestBase
    {
        protected override void RegisterRequestSystems(World world)
        {
            GetOrAddRequestSystem<RaceTestRequest_RequestSystem>();
        }

        // System that writes in parallel using IJobParallelFor
        [DisableAutoCreation]
        public partial class ParallelWriterSystem : SystemBase
        {
            private RequestWriter<RaceTestRequest> _writer;
            public const int RequestCount = 100;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<RaceTestRequest>(RequestCount);
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                var parallelWriter = _writer.AsParallelWriter();
                var job = new ParallelWriteJob { Writer = parallelWriter };
                Dependency = job.Schedule(RequestCount, 32, Dependency);
            }

            [BurstCompile]
            private struct ParallelWriteJob : IJobParallelFor
            {
                public RequestWriter<RaceTestRequest>.ParallelWriter Writer;
                public void Execute(int index) => Writer.WriteNoResize(new RaceTestRequest { Value = index });
            }
        }

        // Another independent parallel writer system
        [DisableAutoCreation]
        public partial class AnotherParallelWriterSystem : SystemBase
        {
            private RequestWriter<RaceTestRequest> _writer;
            public const int RequestCount = 100;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<RaceTestRequest>(RequestCount);
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                var parallelWriter = _writer.AsParallelWriter();
                var job = new ParallelWriteJob { Writer = parallelWriter };
                Dependency = job.Schedule(RequestCount, 32, Dependency);
            }

            [BurstCompile]
            private struct ParallelWriteJob : IJobParallelFor
            {
                public RequestWriter<RaceTestRequest>.ParallelWriter Writer;
                public void Execute(int index) => Writer.WriteNoResize(new RaceTestRequest { Value = index });
            }
        }

        // System that writes synchronously (without a job)
        [DisableAutoCreation]
        public partial class SingleWriterSystem : SystemBase
        {
            private RequestWriter<RaceTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<RaceTestRequest>();
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                _writer.Write(new RaceTestRequest { Value = -1 });
            }
        }

        // Reader system to consume and clear requests, preventing buffer accumulation
        [DisableAutoCreation]
        public partial class RaceTestRequestReaderSystem : SystemBase
        {
            private RequestReader<RaceTestRequest> _reader;

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<RaceTestRequest>();
            }

            protected override void OnUpdate()
            {
                foreach (var _ in _reader.Read()) { }
                _reader.Clear();
            }
        }

        [Test]
        public void MixedSyncAndParallelWritingToSameBuffer_Works()
        {
            var parallelSystem = GetOrAddSystemToSimulationManaged<ParallelWriterSystem>();
            var singleSystem = GetOrAddSystemToSimulationManaged<SingleWriterSystem>();
            var readerSystem = GetOrAddSystemToSimulationManaged<RaceTestRequestReaderSystem>();

            UpdateWorld(10);
            CompleteJobs();
        }

        [Test]
        public void TwoIndependentParallelSystemsWritingToSameBuffer_Works()
        {
            var systemA = GetOrAddSystemToSimulationManaged<ParallelWriterSystem>();
            var systemB = GetOrAddSystemToSimulationManaged<AnotherParallelWriterSystem>();
            var readerSystem = GetOrAddSystemToSimulationManaged<RaceTestRequestReaderSystem>();

            UpdateWorld(10);
            CompleteJobs();
        }
    }
}