using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Singleton component that holds a <see cref="Requests{T}"/> container for request messaging.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public struct RequestSingleton<T> : IComponentData where T : unmanaged
    {
        /// <summary>
        /// The request container instance.
        /// </summary>
        public Requests<T> Requests;
    }
}