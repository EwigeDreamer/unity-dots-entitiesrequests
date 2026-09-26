using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Handle of a request bank: allocates the bank block and owns the allocator used for it.
    /// Entry point of the core — merge and teardown; cards are constructed from this handle.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public unsafe struct RequestBank<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private BankData<T>* _data;

        /// <summary>Allocator of the bank block. Guaranteed to equal the allocator of every bank field.</summary>
        private AllocatorManager.AllocatorHandle _allocator;

        /// <summary>
        /// Allocates the bank block; <see cref="BankData{T}"/> allocates its own fields.
        /// The bank is never allocated twice and never allocates itself.
        /// </summary>
        public RequestBank(AllocatorManager.AllocatorHandle allocator, int readCapacity = 64)
        {
            var size = UnsafeUtility.SizeOf<BankData<T>>();
            var alignment = UnsafeUtility.AlignOf<BankData<T>>();
            var data = (BankData<T>*)AllocatorManager.Allocate(allocator, size, alignment, 1);
            *data = new BankData<T>(allocator, readCapacity);

            _data = data;
            _allocator = allocator;
        }

        /// <summary>True when the bank block is allocated.</summary>
        public bool IsCreated => _data != null;

        /// <summary>Internal access to the bank state, used by card constructors.</summary>
        internal BankData<T>* Data => _data;

        /// <summary>Moves every pending write into the read buffer. Must run in a job-free window.</summary>
        public void Merge()
        {
            if (_data == null)
            {
                Debug.LogError("[Requests] RequestBank.Merge ignored: the bank is not created.");
                return;
            }

            _data->Merge();
        }

        /// <summary>
        /// Disposes the bank state and frees the bank block. Card blocks stay alive until their
        /// handles are disposed, so a forgotten card leaks only its own small block.
        /// </summary>
        public void Dispose()
        {
            if (_data == null)
            {
                return;
            }

            _data->Dispose();
            AllocatorManager.Free(_allocator, _data, UnsafeUtility.SizeOf<BankData<T>>(), UnsafeUtility.AlignOf<BankData<T>>(), 1);
            _data = null;
        }
    }
}
