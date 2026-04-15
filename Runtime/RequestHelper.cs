using EntitiesRequests.Internal;
using Unity.Collections;
using Unity.Entities;

namespace EntitiesRequests
{
    public static class RequestHelper
    {
        public static RequestWriter<T> GetRequestWriter<T>(this ref SystemState state)
            where T : unmanaged
        {
            return GetOrCreateSingleton<T>(ref state).requests.GetWriter();
        }

        public static RequestWriter<T> GetRequestWriter<T>(this SystemBase systemBase)
            where T : unmanaged
        {
            return GetRequestWriter<T>(ref systemBase.CheckedStateRef);
        }

        public static RequestWriter<T> GetRequestWriter<T>(this EntityManager entityManager)
            where T : unmanaged
        {
            return GetOrCreateSingleton<T>(entityManager).requests.GetWriter();
        }

        public static RequestReader<T> GetRequestReader<T>(this ref SystemState state)
            where T : unmanaged
        {
            return GetOrCreateSingleton<T>(ref state).requests.GetReader();
        }

        public static RequestReader<T> GetRequestReader<T>(this SystemBase systemBase)
            where T : unmanaged
        {
            return GetRequestReader<T>(ref systemBase.CheckedStateRef);
        }

        public static RequestReader<T> GetRequestReader<T>(this EntityManager entityManager)
            where T : unmanaged
        {
            return GetOrCreateSingleton<T>(entityManager).requests.GetReader();
        }

        static RequestSingleton<T> GetOrCreateSingleton<T>(EntityManager entityManager)
            where T : unmanaged
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<RequestSingleton<T>>());
            if (query.TryGetSingleton<RequestSingleton<T>>(out var singleton)) return singleton;
            var requests = new Requests<T>(512, Allocator.Persistent);
            singleton = new RequestSingleton<T> { requests = requests };
            entityManager.CreateSingleton(singleton);
            return singleton;
        }

        static RequestSingleton<T> GetOrCreateSingleton<T>(ref SystemState state)
            where T : unmanaged
        {
            var query = state.GetEntityQuery(ComponentType.ReadWrite<RequestSingleton<T>>());
            if (query.TryGetSingleton<RequestSingleton<T>>(out var singleton)) return singleton;

            var requests = new Requests<T>(512, Allocator.Persistent);
            singleton = new RequestSingleton<T> { requests = requests };
            state.EntityManager.CreateSingleton(singleton);
            return singleton;
        }
    }
}