using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Client handle of a writer card: allocates the card block with the bank allocator and
    /// registers it. Disposal is silent even when the card was never created.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public unsafe struct RequestWriter<T> : IDisposable where T : unmanaged
    {
        private CardData<T>* _data;

        /// <summary>Allocator that allocated the card block, used to free it on Dispose.</summary>
        private AllocatorManager.AllocatorHandle _allocator;

        /// <summary>
        /// Creates and registers a writer card. When the bank is not created the card is not
        /// allocated at all: every operation becomes a logged no-op and disposal does nothing.
        /// </summary>
        public RequestWriter(RequestBank<T> bank, int capacity = 64)
        {
            var bankData = bank.Data;
            if (bankData == null)
            {
                _data = null;
                _allocator = default;
                return;
            }

            _allocator = bankData->_allocator;
            _data = (CardData<T>*)AllocatorManager.Allocate(_allocator, UnsafeUtility.SizeOf<CardData<T>>(), UnsafeUtility.AlignOf<CardData<T>>(), 1);
            bankData->RegisterWriter(_data, capacity);
        }

        /// <summary>True while the bank is alive; false once the bank has been disposed.</summary>
        public bool IsValid => _data != null && _data->_isValid;

        /// <summary>Writes a request into the private buffer, growing it when needed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(in T value)
        {
            if (!IsValid)
            {
                Debug.LogError("[Requests] RequestWriter.Write ignored: the card is not valid.");
                return;
            }

            _data->_buffer->Add(value);
        }

        /// <summary>Grows the private buffer to at least the given capacity. Never shrinks.</summary>
        public void EnsureCapacity(int capacity)
        {
            if (!IsValid)
            {
                Debug.LogError("[Requests] RequestWriter.EnsureCapacity ignored: the card is not valid.");
                return;
            }

            if (_data->_buffer->Capacity < capacity)
            {
                _data->_buffer->SetCapacity(capacity);
            }
        }

        /// <summary>Parallel access to the private buffer of this writer.</summary>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(_data);
        }

        /// <summary>
        /// Unregisters the card, frees its buffer and then the card block itself.
        /// Silent when the card is absent.
        /// </summary>
        public void Dispose()
        {
            if (_data == null)
            {
                return;
            }

            if (_data->_isValid)
            {
                _data->_bank->Unregister(_data);
            }

            AllocatorManager.Free(_allocator, _data, UnsafeUtility.SizeOf<CardData<T>>(), UnsafeUtility.AlignOf<CardData<T>>(), 1);
            _data = null;
        }

        /// <summary>
        /// Multiple threads may write through this writer without resizing the buffer.
        /// Requests are dropped with a log once the card is not valid.
        /// </summary>
        public unsafe struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction]
            private CardData<T>* _data;

            internal ParallelWriter(CardData<T>* data)
            {
                _data = data;
            }

            /// <summary>
            /// Writes a request without checking capacity; capacity must be reserved beforehand.
            /// The parallel writer is built from the heap descriptor on every call, never from a copy.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteNoResize(in T value)
            {
                if (_data == null || !_data->_isValid)
                {
                    Debug.LogError("[Requests] ParallelWriter.WriteNoResize ignored: the card is not valid.");
                    return;
                }

                _data->_buffer->AsParallelWriter().AddNoResize(value);
            }
        }
    }
}
