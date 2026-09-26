using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Owner logic of a request bank, invoked by the generated per-type request system.
    /// Uses only raw <see cref="SystemState"/>/<see cref="EntityManager"/> APIs: SystemAPI is
    /// unavailable outside a system, and it cannot be used by generated code either, because source
    /// generators do not see each other's output and the calls would stay unrewritten at runtime.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public static class RequestOwner<T> where T : unmanaged
    {
        /// <summary>
        /// Declares the write fence on the bank marker and waits for the bank to exist.
        /// The write declaration is what makes owner jobs order after every writer and reader:
        /// writers and readers declare read access to the marker, and only a write declaration on our
        /// side pulls in both fences — the write fence and every read fence.
        /// </summary>
        /// <param name="state">Reference to the system state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnCreate(ref SystemState state)
        {
            // Resolving the handle declares write access to the marker on this system's state; the
            // handle itself is unused. The whole mechanism is described in RequestSingleton<T> remarks.
            state.GetComponentTypeHandle<RequestSingleton<T>>();
            state.RequireForUpdate<RequestSingleton<T>>();
        }

        /// <summary>
        /// Schedules the merge job with the system dependency. That dependency already combines every
        /// writer and reader job that declared access to the marker, so no main-thread wait is needed.
        /// The scheduled job becomes the marker's write fence, which orders later readers after the merge.
        /// </summary>
        /// <param name="state">Reference to the system state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnUpdate(ref SystemState state)
        {
            var bank = state.GetEntityQuery(ComponentType.ReadOnly<RequestSingleton<T>>())
                .GetSingleton<RequestSingleton<T>>().Bank;

            state.Dependency = new MergeRequestsJob<T> { Bank = bank }.Schedule(state.Dependency);
        }

        /// <summary>
        /// Cuts the entry point and closes the bank. Destroying the marker entity is a structural change
        /// and therefore a sync point: it completes every in-flight job, a merge job included, before
        /// the bank buffers are freed. When the marker is absent, this is a silent no-op.
        /// </summary>
        /// <param name="state">Reference to the system state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnDestroy(ref SystemState state)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<RequestSingleton<T>>());
            if (!query.TryGetSingletonEntity<RequestSingleton<T>>(out var entity))
            {
                return;
            }

            var bank = query.GetSingleton<RequestSingleton<T>>().Bank;
            state.EntityManager.DestroyEntity(entity);
            bank.Dispose();
        }
    }
}
