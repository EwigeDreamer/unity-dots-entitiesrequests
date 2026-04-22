using Unity.Burst;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Base system for managing requests of type <typeparamref name="T"/>.
    /// Updates the request container each frame, moving pending writes to the read buffer.
    /// Does not automatically clear the read buffer; this must be done by the consuming system.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    [BurstCompile]
    [UpdateInGroup(typeof(RequestSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation
                       | WorldSystemFilterFlags.ServerSimulation
                       | WorldSystemFilterFlags.LocalSimulation)]
    public abstract unsafe partial class RequestSystemBase<T> : SystemBase where T : unmanaged
    {
        [BurstCompile]
        protected override void OnCreate()
        {
            RequireForUpdate<RequestSingleton<T>>();
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            CompleteDependency();
            
            if (SystemAPI.TryGetSingleton<RequestSingleton<T>>(out var singleton))
            {
                singleton.Requests.Update();
            }
        }

        [BurstCompile]
        protected override void OnDestroy()
        {
            if (SystemAPI.TryGetSingleton<RequestSingleton<T>>(out var singleton))
            {
                singleton.Requests.Dispose();
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestSingleton<T>>());
            }
        }
    }
}