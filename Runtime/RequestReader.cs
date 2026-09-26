using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Client handle of a reader card: allocates the card block with the client allocator and
    /// registers it with the bank. Reads from the shared read buffer, which must be cleared after
    /// processing. Disposal is silent even when the card was never created.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public unsafe struct RequestReader<T> : IDisposable where T : unmanaged
    {
        private CardData<T>* _data;

        /// <summary>Allocator that allocated the card block, used to free it on Dispose.</summary>
        private AllocatorManager.AllocatorHandle _allocator;

        /// <summary>
        /// Creates and registers a reader card. The card block is allocated with
        /// <paramref name="allocator"/>: the client owns the block and frees it on disposal,
        /// independently of the bank. When the bank is not created the card is not allocated at all —
        /// every operation becomes a logged no-op and disposal does nothing.
        /// </summary>
        /// <param name="bank">Bank of the request type; its read buffer is aliased by the card.</param>
        /// <param name="allocator">Allocator of the card block; must outlive the card.</param>
        public RequestReader(RequestBank<T> bank, AllocatorManager.AllocatorHandle allocator)
        {
            var bankData = bank.Data;
            if (bankData == null)
            {
                _data = null;
                _allocator = default;
                return;
            }

            _allocator = allocator;
            _data = (CardData<T>*)AllocatorManager.Allocate(_allocator, UnsafeUtility.SizeOf<CardData<T>>(), UnsafeUtility.AlignOf<CardData<T>>(), 1);
            bankData->RegisterReader(_data);
        }

        /// <summary>True while the bank is alive; false once the bank has been disposed.</summary>
        public bool IsValid => _data != null && _data->_isValid;

        /// <summary>Requests currently accumulated in the read buffer.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> Read()
        {
            if (!IsValid)
            {
                Debug.LogError("[Requests] RequestReader.Read ignored: the card is not valid.");
                return ReadOnlySpan<T>.Empty;
            }

            return _data->_buffer->AsReadOnlySpan();
        }

        /// <summary>Clears the read buffer. Must be called explicitly after processing.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (!IsValid)
            {
                Debug.LogError("[Requests] RequestReader.Clear ignored: the card is not valid.");
                return;
            }

            _data->_buffer->Clear();
        }

        /// <summary>Unregisters the card and frees the card block itself. Silent when the card is absent.</summary>
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
    }
}
