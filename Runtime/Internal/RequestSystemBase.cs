using Unity.Burst;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Internal
{
    [BurstCompile]
    [UpdateInGroup(typeof(RequestSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public unsafe abstract partial class RequestSystemBase<T> : SystemBase where T : unmanaged
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
            SystemAPI.GetSingleton<RequestSingleton<T>>().requests.Update();
        }

        [BurstCompile]
        protected override void OnDestroy()
        {
            if (SystemAPI.TryGetSingleton<RequestSingleton<T>>(out var singleton))
            {
                singleton.requests.Dispose();
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestSingleton<T>>());
            }
        }

        public RequestWriter<T> GetRequestWriter()
        {
            return SystemAPI.GetSingleton<RequestSingleton<T>>().requests.GetWriter();
        }

        public RequestReader<T> GetRequestReader()
        {
            return SystemAPI.GetSingleton<RequestSingleton<T>>().requests.GetReader();
        }
    }
}