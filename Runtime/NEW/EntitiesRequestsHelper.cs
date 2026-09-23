using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tmp
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
        /// </summary>
        /// <typeparam name="T">Unmanaged request type.</typeparam>
        /// <param name="state">Reference to the system state.</param>
        /// <returns>The bank of this request type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestBank<T> GetOrCreateBank<T>(ref SystemState state) where T : unmanaged
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<RequestSingleton<T>>();
            var query = builder.Build(ref state);
            if (query.TryGetSingleton<RequestSingleton<T>>(out var singleton))
            {
                return singleton.Bank;
            }

            var bank = new RequestBank<T>(Allocator.Persistent, ReadCapacity);
            state.EntityManager.CreateSingleton(new RequestSingleton<T> { Bank = bank });
            return bank;
        }
    }
}
