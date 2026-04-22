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
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <returns>A <see cref="RequestWriter{T}"/> for publishing requests.</returns>
        public static RequestWriter<T> GetRequestWriter<T>(this ref SystemState state)
            where T : unmanaged
        {
            return EntitiesRequestsHelper.GetOrCreateSingleton<T>(ref state).Requests.GetWriter();
        }

        /// <summary>
        /// Gets a request writer for the specified request type.
        /// Creates a singleton entity with the request container if it does not exist.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="systemBase">The system base instance.</param>
        /// <returns>A <see cref="RequestWriter{T}"/> for publishing requests.</returns>
        public static RequestWriter<T> GetRequestWriter<T>(this SystemBase systemBase)
            where T : unmanaged
        {
            return GetRequestWriter<T>(ref systemBase.CheckedStateRef);
        }

        /// <summary>
        /// Gets a request writer for the specified request type.
        /// Creates a singleton entity with the request container if it does not exist.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="entityManager">The entity manager.</param>
        /// <returns>A <see cref="RequestWriter{T}"/> for publishing requests.</returns>
        public static RequestWriter<T> GetRequestWriter<T>(this EntityManager entityManager)
            where T : unmanaged
        {
            return EntitiesRequestsHelper.GetOrCreateSingleton<T>(entityManager).Requests.GetWriter();
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

        /// <summary>
        /// Ensures that the internal request buffers have at least the specified capacity.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <param name="capacity">Minimum capacity required.</param>
        public static unsafe void EnsureRequestBufferCapacity<T>(this ref SystemState state, int capacity)
            where T : unmanaged
        {
            var singleton = EntitiesRequestsHelper.GetOrCreateSingleton<T>(ref state);
            singleton.Requests.EnsureCapacity(capacity);
        }

        /// <summary>
        /// Ensures that the internal request buffers have at least the specified capacity.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="entityManager">The entity manager.</param>
        /// <param name="capacity">Minimum capacity required.</param>
        public static unsafe void EnsureRequestBufferCapacity<T>(this EntityManager entityManager, int capacity)
            where T : unmanaged
        {
            var singleton = EntitiesRequestsHelper.GetOrCreateSingleton<T>(entityManager);
            singleton.Requests.EnsureCapacity(capacity);
        }
    }
}