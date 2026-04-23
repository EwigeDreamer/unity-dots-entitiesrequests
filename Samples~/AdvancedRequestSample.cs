using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using ED.DOTS.EntitiesRequests;

// Register the request type for source generation
[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Samples.AdvancedRequest))]

namespace ED.DOTS.EntitiesRequests.Samples
{
    /// <summary>
    /// Request structure for the advanced parallel write example.
    /// </summary>
    public struct AdvancedRequest
    {
        public int Index;
    }

    /// <summary>
    /// Unmanaged system that schedules a parallel batch job to write requests when P is pressed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AdvancedRequestSenderSystem : ISystem
    {
        private RequestWriter<AdvancedRequest> _writer;
        private const int RequestCount = 1000;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _writer = state.GetRequestWriter<AdvancedRequest>();
            state.EnsureRequestBufferCapacity<AdvancedRequest>(RequestCount);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                var parallelWriter = _writer.AsParallelWriter();
                var job = new ParallelWriteBatchJob
                {
                    Writer = parallelWriter
                };

                state.Dependency = job.ScheduleParallel(RequestCount, 64, state.Dependency);
                Debug.Log($"[Sender] Scheduled parallel batch job to write {RequestCount} requests.");
            }
        }

        /// <summary>
        /// Parallel batch job that writes requests using RequestWriter.ParallelWriter.
        /// </summary>
        [BurstCompile]
        private struct ParallelWriteBatchJob : IJobParallelForBatch
        {
            public RequestWriter<AdvancedRequest>.ParallelWriter Writer;

            public void Execute(int startIndex, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    int index = startIndex + i;
                    Writer.WriteNoResize(new AdvancedRequest { Index = index });
                }
            }
        }
    }

    /// <summary>
    /// System that reads AdvancedRequest in the next frame and logs summary.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AdvancedRequestSenderSystem))]
    public partial struct AdvancedRequestReceiverSystem : ISystem
    {
        private RequestReader<AdvancedRequest> _reader;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _reader = state.GetRequestReader<AdvancedRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int count = 0;
            int min = int.MaxValue;
            int max = int.MinValue;

            foreach (var req in _reader.Read())
            {
                count++;
                if (req.Index < min) min = req.Index;
                if (req.Index > max) max = req.Index;
            }

            if (count > 0)
            {
                Debug.Log($"[Receiver] Received {count} AdvancedRequests. Min index: {min}, Max index: {max}");
                _reader.Clear();
            }
        }
    }
}