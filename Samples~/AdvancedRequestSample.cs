using ED.DOTS.EntitiesRequests;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

// Register the request type. The source generator emits its owner system into this assembly.
[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Samples.AdvancedRequest))]

namespace ED.DOTS.EntitiesRequests.Samples
{
    /// <summary>Request used by the advanced parallel write example.</summary>
    public struct AdvancedRequest
    {
        /// <summary>Index of the request inside the scheduled batch.</summary>
        public int Index;
    }

    /// <summary>
    /// Schedules a parallel write on every P press. The system itself stays managed: reading legacy
    /// <see cref="Input"/> is not Burst compatible, while the job that actually writes the requests
    /// is, so the hot path keeps its Burst compilation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation
                       | WorldSystemFilterFlags.ServerSimulation
                       | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AdvancedRequestSenderSystem : ISystem
    {
        private const int RequestCount = 1000;

        private RequestWriter<AdvancedRequest> _writer;

        public void OnCreate(ref SystemState state)
        {
            _writer = state.GetRequestWriter<AdvancedRequest>(RequestCount);
        }

        public void OnDestroy(ref SystemState state)
        {
            _writer.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                // WriteNoResize never grows the buffer, so reserve capacity before scheduling the job.
                _writer.EnsureCapacity(RequestCount);

                var job = new ParallelWriteJob { Writer = _writer.AsParallelWriter() };
                state.Dependency = job.Schedule(RequestCount, 64, state.Dependency);
                Debug.Log($"[Advanced] Scheduled a parallel write of {RequestCount} requests.");
            }
        }

        /// <summary>Writes one request per index through the writer's parallel view.</summary>
        [BurstCompile]
        private struct ParallelWriteJob : IJobParallelFor
        {
            public RequestWriter<AdvancedRequest>.ParallelWriter Writer;

            public void Execute(int index)
            {
                Writer.WriteNoResize(new AdvancedRequest { Index = index });
            }
        }
    }

    /// <summary>
    /// Reads whatever the previous tick merged and logs the summary. The reader is Burst compiled:
    /// iterating the read span and clearing the buffer are both Burst safe.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation
                       | WorldSystemFilterFlags.ServerSimulation
                       | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AdvancedRequestReceiverSystem : ISystem
    {
        private RequestReader<AdvancedRequest> _reader;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _reader = state.GetRequestReader<AdvancedRequest>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _reader.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var count = 0;
            var min = int.MaxValue;
            var max = int.MinValue;

            foreach (var request in _reader.Read())
            {
                count++;
                if (request.Index < min) min = request.Index;
                if (request.Index > max) max = request.Index;
            }

            _reader.Clear();

            if (count > 0)
            {
                Debug.Log($"[Advanced] Received {count} requests. Min index: {min}, Max index: {max}");
            }
        }
    }
}
