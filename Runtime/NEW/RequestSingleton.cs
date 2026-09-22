using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tmp
{
    /// <summary>
    /// Singleton component that publishes the bank of request type <typeparamref name="T"/>
    /// and doubles as the fence marker: writers declare write access to it, the owner reads it
    /// before merging. Its entity exists exactly while the bank is alive.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public struct RequestSingleton<T> : IComponentData where T : unmanaged
    {
        /// <summary>Bank of this request type: the address book for cards and the owner of its buffers.</summary>
        public RequestBank<T> Bank;
    }
}
