using ED.DOTS.EntitiesRequests;
using Unity.Collections;
using UnityEngine;

namespace ED.DOTS.EntitiesRequests.Samples
{
    /// <summary>
    /// Drives a request bank by hand, without ECS. The core is a plain heap structure, so it can be
    /// used from any MonoBehaviour: W writes, R merges and reads, then clears.
    /// </summary>
    public class ManualRequestSample : MonoBehaviour
    {
        private RequestBank<int> _bank;
        private RequestWriter<int> _writer;
        private RequestReader<int> _reader;
        private int _counter;

        private void Start()
        {
            // The bank owns its buffers; each card is allocated from the allocator the caller passes
            // and freed when the card is disposed, independently of the bank.
            _bank = new RequestBank<int>(Allocator.Persistent, 128);
            _writer = new RequestWriter<int>(_bank, Allocator.Persistent, 128);
            _reader = new RequestReader<int>(_bank, Allocator.Persistent);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                _writer.Write(_counter);
                Debug.Log($"[Manual] Wrote a request: {_counter}");
                _counter++;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                // Merge moves every pending write into the shared read buffer. It must run in a
                // job-free window; a MonoBehaviour update is one.
                _bank.Merge();

                var sum = 0;
                var count = 0;
                foreach (var value in _reader.Read())
                {
                    Debug.Log($"[Manual] Read a request: {value}");
                    sum += value;
                    count++;
                }

                Debug.Log($"[Manual] Merged {count} requests, sum = {sum}");

                // The read buffer is shared and persists until cleared.
                _reader.Clear();
            }
        }

        private void OnDestroy()
        {
            // Disposal order does not matter: a disposed bank invalidates its cards silently.
            _writer.Dispose();
            _reader.Dispose();
            _bank.Dispose();
        }
    }
}
