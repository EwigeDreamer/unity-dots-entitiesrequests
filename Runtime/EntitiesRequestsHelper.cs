using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Creation and lookup of the request bank published through <see cref="RequestSingleton{T}"/>.
    /// </summary>
    public static class EntitiesRequestsHelper
    {
        /// <summary>Capacity of the shared read buffer. Writes grow as needed, the read buffer starts here.</summary>
        private const int ReadCapacity = 512;

        /// <summary>
        /// Returns the existing bank of <typeparamref name="T"/>, or creates one together with its
        /// singleton entity. The bank lives until its owner disposes it.
        /// <para>
        /// Creation is refused when this world cannot host the bank, that is when the request type
        /// was never registered (its owner index is unpublished), or when the generated owner is not
        /// part of this world. Both cases log an error and return an uncreated bank, which turns
        /// every card into a logged no-op instead of leaking a bank nobody would ever close.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <returns>The bank of this request type, or an uncreated one when creation is refused.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestBank<T> GetOrCreateBank<T>(ref SystemState state) where T : unmanaged
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<RequestSingleton<T>>();
            var query = builder.Build(ref state);
            if (query.TryGetSingleton<RequestSingleton<T>>(out var singleton))
            {
                return singleton.Bank;
            }

            var ownerIndex = RequestBridge<T>.Read();
            if (ownerIndex == SystemTypeIndex.Null)
            {
                Debug.LogError("[Requests] Request bank not created: the owner index of the request type was not published. The type is likely missing [assembly: RegisterRequest(typeof(...))].");
                return default;
            }

            if (state.WorldUnmanaged.GetExistingUnmanagedSystem(ownerIndex) == SystemHandle.Null)
            {
                Debug.LogError("[Requests] Request bank not created: the owner system of the request type is not present in this world.");
                return default;
            }

            var bank = new RequestBank<T>(Allocator.Persistent, ReadCapacity);
            state.EntityManager.CreateSingleton(new RequestSingleton<T> { Bank = bank });
            return bank;
        }
    }
}
