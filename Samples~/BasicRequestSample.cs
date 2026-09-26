using ED.DOTS.EntitiesRequests;
using Unity.Entities;
using UnityEngine;

// Register the request type. The source generator emits its owner system into this assembly.
[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Samples.BasicRequest))]

namespace ED.DOTS.EntitiesRequests.Samples
{
    /// <summary>Simple request used by the basic example.</summary>
    public struct BasicRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Writes a request on every Space press. The writer card is taken in <c>OnCreate</c> and
    /// disposed in <c>OnDestroy</c>; taking a card declares the fence access that lets the generated
    /// owner complete this system's jobs before merging.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation
                       | WorldSystemFilterFlags.ServerSimulation
                       | WorldSystemFilterFlags.LocalSimulation)]
    public partial class BasicRequestSenderSystem : SystemBase
    {
        private RequestWriter<BasicRequest> _writer;

        protected override void OnCreate()
        {
            _writer = this.GetRequestWriter<BasicRequest>(64);
        }

        protected override void OnDestroy()
        {
            _writer.Dispose();
        }

        protected override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var value = UnityEngine.Time.frameCount;
                _writer.Write(new BasicRequest { Value = value });
                Debug.Log($"[Basic] Sent a request with Value = {value}");
            }
        }
    }

    /// <summary>
    /// Reads the shared read buffer. The generated owner merges writes at the end of the simulation
    /// phase (<see cref="RequestSystemGroup"/>), so a reader placed in
    /// <see cref="SimulationSystemGroup"/> sees the requests of the previous tick, never the current
    /// one. The buffer is shared and persists until cleared, so it is cleared after processing.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation
                       | WorldSystemFilterFlags.ServerSimulation
                       | WorldSystemFilterFlags.LocalSimulation)]
    public partial class BasicRequestReceiverSystem : SystemBase
    {
        private RequestReader<BasicRequest> _reader;

        protected override void OnCreate()
        {
            _reader = this.GetRequestReader<BasicRequest>();
        }

        protected override void OnDestroy()
        {
            _reader.Dispose();
        }

        protected override void OnUpdate()
        {
            foreach (var request in _reader.Read())
            {
                Debug.Log($"[Basic] Received a request with Value = {request.Value}");
            }

            _reader.Clear();
        }
    }
}
