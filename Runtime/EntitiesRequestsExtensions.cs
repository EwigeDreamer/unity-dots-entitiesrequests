using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Entry points for systems that take writer or reader cards of a request type.
    /// Cards are taken in OnCreate and disposed in OnDestroy. Every card is taken from a system
    /// state, so its fence access is always declared; there is no card without a fence.
    /// </summary>
    public static class EntitiesRequestsExtensions
    {
        /// <summary>
        /// Takes a writer card for <typeparamref name="T"/> (Burst path, from an ISystem).
        /// Creates the bank on the first request and declares write access to
        /// <see cref="RequestSingleton{T}"/> so that the owner can complete this system's jobs
        /// before merging the private buffer.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <param name="capacity">Initial capacity of the writer's private buffer.</param>
        /// <returns>A writer card.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestWriter<T> GetRequestWriter<T>(this ref SystemState state, int capacity = 64) where T : unmanaged
        {
            state.GetComponentTypeHandle<RequestSingleton<T>>();
            var bank = EntitiesRequestsHelper.GetOrCreateBank<T>(ref state);
            return new RequestWriter<T>(bank, Allocator.Persistent, capacity);
        }

        /// <summary>
        /// Takes a reader card for <typeparamref name="T"/> (Burst path, from an ISystem).
        /// Creates the bank on the first request and declares read access to
        /// <see cref="RequestSingleton{T}"/>, so that the owner waits for this system's jobs
        /// before merging into the shared read buffer.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <returns>A reader card.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestReader<T> GetRequestReader<T>(this ref SystemState state) where T : unmanaged
        {
            state.GetComponentTypeHandle<RequestSingleton<T>>(true);
            var bank = EntitiesRequestsHelper.GetOrCreateBank<T>(ref state);
            return new RequestReader<T>(bank, Allocator.Persistent);
        }

        /// <summary>
        /// Takes a writer card for <typeparamref name="T"/> from a managed system.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="systemBase">The system base instance.</param>
        /// <param name="capacity">Initial capacity of the writer's private buffer.</param>
        /// <returns>A writer card.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestWriter<T> GetRequestWriter<T>(this SystemBase systemBase, int capacity = 64) where T : unmanaged
        {
            return GetRequestWriter<T>(ref systemBase.CheckedStateRef, capacity);
        }

        /// <summary>
        /// Takes a reader card for <typeparamref name="T"/> from a managed system.
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="systemBase">The system base instance.</param>
        /// <returns>A reader card.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestReader<T> GetRequestReader<T>(this SystemBase systemBase) where T : unmanaged
        {
            return GetRequestReader<T>(ref systemBase.CheckedStateRef);
        }
    }
}
