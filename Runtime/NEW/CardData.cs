using Unity.Collections.LowLevel.Unsafe;

namespace ED.DOTS.EntitiesRequests.Tmp
{
    /// <summary>
    /// Heap block describing a single request card. It is a pure container of foreign pointers:
    /// no constructor and no disposal — the block is allocated and freed by the client handle,
    /// while the fields are wired up by the bank during registration.
    /// Outlives the bank: on bank death only <see cref="_isValid"/> is cleared, so the owning
    /// handle can always read the card state without touching freed memory.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    internal unsafe struct CardData<T> where T : unmanaged
    {
        /// <summary>The bank that wired this card and owns the buffer.</summary>
        [NativeDisableUnsafePtrRestriction]
        internal BankData<T>* _bank;

        /// <summary>Writer card: its own buffer block. Reader card: aliases Bank-&gt;ReadBuffer.</summary>
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList<T>* _buffer;

        /// <summary>True while the bank is alive. Cleared by the bank during its disposal.</summary>
        internal bool _isValid;
    }
}
