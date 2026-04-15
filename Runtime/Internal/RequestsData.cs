using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests.Internal
{
    /// <summary>
    /// Container for request buffers with fixed roles:
    /// - writeBuffer: always for writing new requests (via RequestWriter)
    /// - readBuffer: always for reading (via RequestReader)
    /// Update() moves all items from writeBuffer to readBuffer, then clears writeBuffer.
    /// </summary>
    public struct RequestsData<T> : IDisposable where T : unmanaged
    {
        internal UnsafeList<T> writeBuffer;
        internal UnsafeList<T> readBuffer;

        public RequestsData(int initialCapacity, Allocator allocator)
        {
            writeBuffer = new UnsafeList<T>(initialCapacity, allocator);
            readBuffer = new UnsafeList<T>(initialCapacity, allocator);
        }

        /// <summary>
        /// Moves all pending writes to the read buffer and clears the write buffer.
        /// Called automatically by RequestSystemBase&lt;T&gt; every frame.
        /// </summary>
        public void Update()
        {
            // Ensure read buffer has enough capacity for the new items (with doubling)
            var requiredCapacity = readBuffer.Length + writeBuffer.Length;
            if (readBuffer.Capacity < requiredCapacity)
            {
                var newCapacity = readBuffer.Capacity;
                while (newCapacity < requiredCapacity) newCapacity *= 2;
                readBuffer.SetCapacity(newCapacity);
            }

            // Efficiently append all items from writeBuffer to readBuffer
            readBuffer.AddRange(writeBuffer);
            
            // Clear write buffer (keep capacity)
            writeBuffer.Clear();
        }

        /// <summary>
        /// Clears the read buffer. Called explicitly by RequestReader&lt;T&gt;.
        /// </summary>
        public void ClearReadBuffer()
        {
            readBuffer.Clear();
        }

        public void Write(in T value)
        {
            writeBuffer.Add(value);
        }

        public void WriteNoResize(in T value)
        {
            if (writeBuffer.Length == writeBuffer.Capacity)
                throw new InvalidOperationException("Request write buffer capacity exceeded. Consider increasing initial capacity or using Write() instead.");
            writeBuffer.AsParallelWriter().AddNoResize(value);
        }

        public void Dispose()
        {
            if (writeBuffer.IsCreated) writeBuffer.Dispose();
            if (readBuffer.IsCreated) readBuffer.Dispose();
        }
    }
}