using Unity.Collections;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Helper methods for managing <see cref="RequestSingleton{T}"/> entities.
    /// Can be used directly when finer control over singleton creation is needed.
    /// </summary>
    public static class EntitiesRequestsHelper
    {
        /// <summary>
        /// Gets the existing singleton component or creates a new entity with a fresh request container.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <returns>The singleton component.</returns>
        public static RequestSingleton<T> GetOrCreateSingleton<T>(ref SystemState state) where T : unmanaged
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<RequestSingleton<T>>();
            var query = builder.Build(ref state);
            if (query.TryGetSingleton<RequestSingleton<T>>(out var singleton))
                return singleton;
            var requests = new Requests<T>(512, Allocator.Persistent);
            singleton = new RequestSingleton<T> { Requests = requests };
            state.EntityManager.CreateSingleton(singleton);
            return singleton;
        }

        /// <summary>
        /// Gets the existing singleton component or creates a new entity with a fresh request container.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="entityManager">The entity manager.</param>
        /// <returns>The singleton component.</returns>
        public static RequestSingleton<T> GetOrCreateSingleton<T>(EntityManager entityManager) where T : unmanaged
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<RequestSingleton<T>>();
            var query = builder.Build(entityManager);
            if (query.TryGetSingleton<RequestSingleton<T>>(out var singleton))
                return singleton;
            var requests = new Requests<T>(512, Allocator.Persistent);
            singleton = new RequestSingleton<T> { Requests = requests };
            entityManager.CreateSingleton(singleton);
            return singleton;
        }
    }
}