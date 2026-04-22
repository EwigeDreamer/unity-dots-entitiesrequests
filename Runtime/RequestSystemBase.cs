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
            // This call registers a read access to the RequestSingleton<T> component with the ECS dependency system.
            // Requesting a read-only ComponentTypeHandle informs the scheduler that this system will read the singleton.
            // Combined with the write declaration in the writer systems, this creates a proper dependency chain:
            // all writer systems' jobs will complete before this system's OnUpdate runs.
            // This ensures that when we call Requests.Update() and clear the write buffer, no pending write jobs are still using it.
            // The handle is not stored because we only need the dependency registration side effect.
            GetComponentTypeHandle<RequestSingleton<T>>(true);
            
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