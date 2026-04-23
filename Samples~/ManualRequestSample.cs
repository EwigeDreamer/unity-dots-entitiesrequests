using UnityEngine;
using Unity.Collections;
using ED.DOTS.EntitiesRequests;

namespace ED.DOTS.EntitiesRequests.Samples
{
    /// <summary>
    /// Example of manual request handling without ECS systems.
    /// Creates a Requests container, writes values on key press, and manually updates/reads/clears.
    /// </summary>
    public class ManualRequestSample : MonoBehaviour
    {
        private Requests<int> _requests;
        private RequestWriter<int> _writer;
        private RequestReader<int> _reader;
        private int _counter;

        private void Start()
        {
            _requests = new Requests<int>(64, Allocator.Persistent);
            _writer = _requests.GetWriter();
            _reader = _requests.GetReader();
            _counter = 0;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                _writer.Write(_counter);
                Debug.Log($"[Manual] Wrote request: {_counter}");
                _counter++;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                // Move pending writes to read buffer
                _requests.Update();

                int sum = 0;
                int count = 0;
                foreach (int value in _reader.Read())
                {
                    Debug.Log($"[Manual] Read request: {value}");
                    sum += value;
                    count++;
                }

                Debug.Log($"[Manual] Total after Update: {count} requests, sum = {sum}");

                // Explicitly clear the read buffer after processing
                _reader.Clear();
            }
        }

        private void OnDestroy()
        {
            if (_requests.IsCreated)
                _requests.Dispose();
        }
    }
}