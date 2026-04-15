using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Internal
{
    public unsafe struct RequestSingleton<T> : IComponentData where T : unmanaged
    {
        public Requests<T> requests;
    }
}