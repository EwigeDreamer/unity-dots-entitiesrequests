using System.Runtime.CompilerServices;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Extension methods for obtaining <see cref="RequestWriter{T}"/> and <see cref="RequestReader{T}"/>
    /// from ECS systems and entity managers.
    /// </summary>
    public static class EntitiesRequestsExtensions
    {
        /// <summary>
        /// Gets a request writer for the specified request type.
        /// Creates a singleton entity with the request container if it does not exist.
        /// The writer will have its own private buffer with the given initial capacity.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <param name="initialCapacity">Initial capacity of the writer's private buffer (default 64).</param>
        /// <returns>A <see cref="RequestWriter{T}"/> for publishing requests.</returns>
        public static RequestWriter<T> GetRequestWriter<T>(this ref SystemState state, int initialCapacity = 64)
            where T : unmanaged
        {
            // This call registers a write access to the RequestSingleton<T> component with the ECS dependency system.
            state.GetComponentTypeHandle<RequestSingleton<T>>();
            
            var singleton = EntitiesRequestsHelper.GetOrCreateSingleton<T>(ref state);
            return singleton.Requests.GetWriter(initialCapacity);
        }

        /// <summary>
        /// Gets a request writer for the specified request type.
        /// Creates a singleton entity with the request container if it does not exist.
        /// The writer will have its own private buffer with the given initial capacity.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="systemBase">The system base instance.</param>
        /// <param name="initialCapacity">Initial capacity of the writer's private buffer (default 64).</param>
        /// <returns>A <see cref="RequestWriter{T}"/> for publishing requests.</returns>
        public static RequestWriter<T> GetRequestWriter<T>(this SystemBase systemBase, int initialCapacity = 64)
            where T : unmanaged
        {
            return GetRequestWriter<T>(ref systemBase.CheckedStateRef, initialCapacity);
        }

        /// <summary>
        /// Gets a request writer for the specified request type.
        /// Creates a singleton entity with the request container if it does not exist.
        /// The writer will have its own private buffer with the given initial capacity.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="entityManager">The entity manager.</param>
        /// <param name="initialCapacity">Initial capacity of the writer's private buffer (default 64).</param>
        /// <returns>A <see cref="RequestWriter{T}"/> for publishing requests.</returns>
        public static RequestWriter<T> GetRequestWriter<T>(this EntityManager entityManager, int initialCapacity = 64)
            where T : unmanaged
        {
            entityManager.GetComponentTypeHandle<RequestSingleton<T>>(false);
            var singleton = EntitiesRequestsHelper.GetOrCreateSingleton<T>(entityManager);
            return singleton.Requests.GetWriter(initialCapacity);
        }

        /// <summary>
        /// Gets a request reader for the specified request type.
        /// Creates a singleton entity with the request container if it does not exist.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <returns>A <see cref="RequestReader{T}"/> for consuming requests.</returns>
        public static RequestReader<T> GetRequestReader<T>(this ref SystemState state)
            where T : unmanaged
        {
            return EntitiesRequestsHelper.GetOrCreateSingleton<T>(ref state).Requests.GetReader();
        }

        /// <summary>
        /// Gets a request reader for the specified request type.
        /// Creates a singleton entity with the request container if it does not exist.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="systemBase">The system base instance.</param>
        /// <returns>A <see cref="RequestReader{T}"/> for consuming requests.</returns>
        public static RequestReader<T> GetRequestReader<T>(this SystemBase systemBase)
            where T : unmanaged
        {
            return GetRequestReader<T>(ref systemBase.CheckedStateRef);
        }

        /// <summary>
        /// Gets a request reader for the specified request type.
        /// Creates a singleton entity with the request container if it does not exist.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="entityManager">The entity manager.</param>
        /// <returns>A <see cref="RequestReader{T}"/> for consuming requests.</returns>
        public static RequestReader<T> GetRequestReader<T>(this EntityManager entityManager)
            where T : unmanaged
        {
            return EntitiesRequestsHelper.GetOrCreateSingleton<T>(entityManager).Requests.GetReader();
        }
    }
}