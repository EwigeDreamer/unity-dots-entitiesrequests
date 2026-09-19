using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using ED.DOTS.EntitiesRequests;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.JobTestRequest))]

namespace ED.DOTS.EntitiesRequests.Tests
{
    public struct JobTestRequest
    {
        public int Value;
    }

    [TestFixture]
    public class RequestReaderJobTests : ECSTestBase
    {
        protected override void RegisterRequestSystems(World world)
        {
            GetOrAddRequestSystem<JobTestRequest_RequestSystem>();
        }

        [DisableAutoCreation]
        public partial class WriterSystem : SystemBase
        {
            private RequestWriter<JobTestRequest> _writer;
            public const int RequestCount = 100;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<JobTestRequest>(RequestCount);
            }

            protected override void OnDestroy()
            {
                _writer.Dispose();
            }

            protected override void OnUpdate()
            {
                for (int i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new JobTestRequest { Value = i });
                }
            }
        }

        [DisableAutoCreation]
        public partial class ReaderSystem : SystemBase
        {
            private RequestReader<JobTestRequest> _reader;
            public int ReceivedCount = -1;
            public int Sum = -1;

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<JobTestRequest>();
            }

            protected override void OnUpdate()
            {
                // Schedule two dummy parallel jobs to simulate work.
                var dummyJob = new DummyParallelJob();
                var handle1 = dummyJob.Schedule(10, 2, Dependency);
                var handle2 = dummyJob.Schedule(10, 2, handle1);

                // Schedule final read job that uses RequestReader.
                var readJob = new FinalReadJob
                {
                    Reader = _reader,
                    SumResult = new NativeArray<int>(1, Allocator.TempJob),
                    CountResult = new NativeArray<int>(1, Allocator.TempJob)
                };
                var finalHandle = readJob.Schedule(handle2);

                finalHandle.Complete();

                ReceivedCount = readJob.CountResult[0];
                Sum = readJob.SumResult[0];
                readJob.CountResult.Dispose();
                readJob.SumResult.Dispose();
            }

            [BurstCompile]
            private struct DummyParallelJob : IJobParallelFor
            {
                public void Execute(int index) { }
            }

            [BurstCompile]
            private struct FinalReadJob : IJob
            {
                public RequestReader<JobTestRequest> Reader;
                public NativeArray<int> SumResult;
                public NativeArray<int> CountResult;

                public void Execute()
                {
                    int sum = 0;
                    int count = 0;
                    foreach (var req in Reader.Read())
                    {
                        sum += req.Value;
                        count++;
                    }
                    SumResult[0] = sum;
                    CountResult[0] = count;
                    Reader.Clear();
                }
            }
        }

        [Test]
        public void RequestReader_UsedInIJob_ThrowsInvalidOperationException()
        {
            var writer = GetOrAddSystemToSimulationManaged<WriterSystem>();
            var reader = GetOrAddSystemToSimulationManaged<ReaderSystem>();

            // First frame: writer writes, reader schedules jobs.
            UpdateWorld(1);
        }
    }
}