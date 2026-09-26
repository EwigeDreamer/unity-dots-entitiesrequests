using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Shared heap state of a request bank for <typeparamref name="T"/>.
    /// Allocates and owns the read buffer and every writer buffer; card blocks are allocated by
    /// clients from their own allocators and only wired up here. Never allocates itself — its block
    /// belongs to RequestBank.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    internal unsafe struct BankData<T> : IDisposable where T : unmanaged
    {
        /// <summary>Allocator of every field below. Guaranteed to equal the owning bank allocator.</summary>
        internal AllocatorManager.AllocatorHandle _allocator;

        /// <summary>Registry of live cards (writers and readers).</summary>
        internal UnsafeList<IntPtr>* _cards;

        /// <summary>Registry of writer buffers owned by the bank. Merged and freed by the bank.</summary>
        internal UnsafeList<IntPtr>* _writeBuffers;

        /// <summary>Shared read buffer, aliased by every reader card.</summary>
        internal UnsafeList<T>* _readBuffer;

        /// <summary>
        /// Allocates the bank fields. The bank block itself is allocated by <see cref="RequestBank{T}"/>,
        /// which constructs this value and copies it into that block.
        /// </summary>
        internal BankData(AllocatorManager.AllocatorHandle allocator, int readCapacity)
        {
            _allocator = allocator;
            _cards = CreateList<IntPtr>(allocator, 4);
            _writeBuffers = CreateList<IntPtr>(allocator, 4);
            _readBuffer = CreateList<T>(allocator, readCapacity);
        }

        /// <summary>
        /// Wires up a client-allocated writer card: allocates its private buffer, fills the card
        /// fields and remembers both the card and the buffer.
        /// </summary>
        internal void RegisterWriter(CardData<T>* card, int capacity)
        {
            var buffer = CreateList<T>(_allocator, capacity);

            card->_bank = (BankData<T>*)UnsafeUtility.AddressOf(ref this);
            card->_buffer = buffer;
            card->_isValid = true;

            _cards->Add((IntPtr)card);
            _writeBuffers->Add((IntPtr)buffer);
        }

        /// <summary>Wires up a client-allocated reader card pointing at the shared read buffer.</summary>
        internal void RegisterReader(CardData<T>* card)
        {
            card->_bank = (BankData<T>*)UnsafeUtility.AddressOf(ref this);
            card->_buffer = _readBuffer;
            card->_isValid = true;

            _cards->Add((IntPtr)card);
        }

        /// <summary>
        /// Forgets the card and frees its buffer when the card is a writer.
        /// The card block itself belongs to the client and is not touched here.
        /// </summary>
        internal void Unregister(CardData<T>* card)
        {
            for (var i = _cards->Length - 1; i >= 0; i--)
            {
                if ((CardData<T>*)_cards->Ptr[i] != card)
                {
                    continue;
                }

                _cards->RemoveAtSwapBack(i);
                break;
            }

            for (var i = _writeBuffers->Length - 1; i >= 0; i--)
            {
                var buffer = (UnsafeList<T>*)_writeBuffers->Ptr[i];
                if (buffer != card->_buffer)
                {
                    continue;
                }

                FreeList(buffer, _allocator);
                _writeBuffers->RemoveAtSwapBack(i);
                break;
            }
        }

        /// <summary>
        /// Appends every pending writer buffer into the read buffer, then clears them.
        /// Must be called in a job-free window.
        /// </summary>
        internal void Merge()
        {
            var readBuffer = _readBuffer;
            for (var i = 0; i < _writeBuffers->Length; i++)
            {
                var buffer = (UnsafeList<T>*)_writeBuffers->Ptr[i];
                var length = buffer->Length;
                if (length == 0)
                {
                    continue;
                }

                readBuffer->AddRange(buffer->Ptr, length);
                buffer->Clear();
            }
        }

        /// <summary>
        /// Invalidates every card, frees all buffers and both registries, then the read buffer.
        /// The bank block itself is freed by its owner.
        /// </summary>
        public void Dispose()
        {
            var allocator = _allocator;

            for (var i = 0; i < _cards->Length; i++)
            {
                ((CardData<T>*)_cards->Ptr[i])->_isValid = false;
            }

            for (var i = 0; i < _writeBuffers->Length; i++)
            {
                FreeList((UnsafeList<T>*)_writeBuffers->Ptr[i], allocator);
            }

            FreeList(_writeBuffers, allocator);
            FreeList(_cards, allocator);
            FreeList(_readBuffer, allocator);

            _writeBuffers = null;
            _cards = null;
            _readBuffer = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static UnsafeList<U>* CreateList<U>(AllocatorManager.AllocatorHandle allocator, int capacity) where U : unmanaged
        {
            var size = UnsafeUtility.SizeOf<UnsafeList<U>>();
            var alignment = UnsafeUtility.AlignOf<UnsafeList<U>>();
            var listPtr = (UnsafeList<U>*)AllocatorManager.Allocate(allocator, size, alignment, 1);
            *listPtr = new UnsafeList<U>(capacity, allocator);
            return listPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FreeList<U>(UnsafeList<U>* list, AllocatorManager.AllocatorHandle allocator) where U : unmanaged
        {
            list->Dispose();
            var size = UnsafeUtility.SizeOf<UnsafeList<U>>();
            var alignment = UnsafeUtility.AlignOf<UnsafeList<U>>();
            AllocatorManager.Free(allocator, list, size, alignment, 1);
        }
    }
}
