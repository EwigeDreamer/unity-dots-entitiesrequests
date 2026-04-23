using ED.DOTS.EntitiesRequests;
using Unity.Entities;
using UnityEngine;

// Register the request type for source generation
[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Samples.BasicRequest))]

namespace ED.DOTS.EntitiesRequests.Samples
{
    /// <summary>
    /// Simple request structure used in the basic example.
    /// </summary>
    public struct BasicRequest
    {
        public int Value;
    }

    /// <summary>
    /// System that sends a BasicRequest every time the Space key is pressed.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class BasicRequestSenderSystem : SystemBase
    {
        private RequestWriter<BasicRequest> _writer;

        protected override void OnCreate()
        {
            // Cache the writer for performance
            _writer = this.GetRequestWriter<BasicRequest>();
        }

        protected override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                int frameCount = UnityEngine.Time.frameCount;
                _writer.Write(new BasicRequest { Value = frameCount });
                Debug.Log($"[Sender] Sent BasicRequest with Value = {frameCount}");
            }
        }
    }

    /// <summary>
    /// System that reads BasicRequest and logs them to the console.
    /// Must clear the read buffer after processing.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BasicRequestSenderSystem))]
    public partial class BasicRequestReceiverSystem : SystemBase
    {
        private RequestReader<BasicRequest> _reader;

        protected override void OnCreate()
        {
            // Cache the reader
            _reader = this.GetRequestReader<BasicRequest>();
        }

        protected override void OnUpdate()
        {
            // Read all requests received this frame
            foreach (var req in _reader.Read())
            {
                Debug.Log($"[Receiver] Received BasicRequest with Value = {req.Value}");
            }
            // Explicitly clear the read buffer after processing
            _reader.Clear();
        }
    }
}