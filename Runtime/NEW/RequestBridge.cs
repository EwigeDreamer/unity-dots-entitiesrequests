using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tmp
{
    /// <summary>
    /// Primary <see cref="SharedStatic{T}"/> context shared by every request-to-owner bridge.
    /// </summary>
    internal struct RequestBridgeKey
    {
    }

    /// <summary>
    /// Per-request-type bridge carrying the <see cref="SystemTypeIndex"/> of the owner system the
    /// generator emitted for this type. <see cref="Publish"/> is public because the generated hook
    /// lives in the consumer assembly; the rest of the surface stays internal, so consumers never
    /// touch the shared static directly.
    /// <para>
    /// The index is published before the first world is created (runtime-init hook), which lets the
    /// bank helper tell from Burst whether the world it runs in hosts the owner at all.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    public static class RequestBridge<T> where T : unmanaged
    {
        /// <summary>
        /// Owner index of the type. The concrete primary context plus the request type as sub-context
        /// is the canonical Burst-compatible shape: the primary part is compile time computable, the
        /// sub-context is an open generic argument, so ILPP rewrites the call into the partially
        /// hashed form and Burst resolves one region per closed generic.
        /// </summary>
        private static readonly SharedStatic<SystemTypeIndex> OwnerIndex =
            SharedStatic<SystemTypeIndex>.GetOrCreate<RequestBridgeKey, T>();

        /// <summary>Publishes the owner system index; called once by the generated hook.</summary>
        /// <param name="ownerIndex">System type index of the generated owner.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Publish(SystemTypeIndex ownerIndex)
        {
            OwnerIndex.Data = ownerIndex;
        }

        /// <summary>
        /// Owner index of this request type, or <c>SystemTypeIndex.Null</c> while the hook has not
        /// published it. Read by the bank helper from Burst, so it must stay Burst-compatible.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SystemTypeIndex Read()
        {
            return OwnerIndex.Data;
        }
    }
}
