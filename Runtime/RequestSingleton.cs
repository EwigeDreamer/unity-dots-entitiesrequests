using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Singleton component that publishes the bank of request type <typeparamref name="T"/>
    /// and doubles as the fence marker: writers and readers declare read access to it, the owner
    /// declares write. Its entity exists exactly while the bank is alive.
    /// </summary>
    /// <remarks>
    /// Access to the marker is declared implicitly: resolving the component type handle on a system
    /// state (<c>GetComponentTypeHandle</c>, with the read-only argument when the access is a read)
    /// registers the dependency as a side effect, so the returned handle is intentionally unused.
    /// Writers and readers both declare read access, and the owner declares write — that write
    /// declaration is what pulls in both fences: the owner's merge job is scheduled alone, after every
    /// job that declared access here, and becomes the marker's write fence that orders later readers
    /// after the merge. Writers declare read rather than write on purpose: each writer touches only its
    /// own private buffer, so nothing orders them against each other, and read access keeps their jobs
    /// parallel.
    /// </remarks>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public struct RequestSingleton<T> : IComponentData where T : unmanaged
    {
        /// <summary>Bank of this request type: the address book for cards and the owner of its buffers.</summary>
        public RequestBank<T> Bank;
    }
}
