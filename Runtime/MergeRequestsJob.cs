using Unity.Burst;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Moves every pending write into the shared read buffer. Single-threaded and exclusive: the merge
    /// appends to the read buffer and may grow it. Ordering against writers and readers is given by the
    /// marker fence declared on the owner system, so the scheduler runs this job alone and after them.
    /// <para>
    /// The type is public only so that the source generator can emit
    /// <c>[assembly: RegisterGenericJobType(typeof(MergeRequestsJob&lt;T&gt;))]</c> for every registered
    /// request type, which is what makes scheduling from Burst work. The bank field is internal, so the
    /// job cannot be built and scheduled from outside the core.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    [BurstCompile]
    public struct MergeRequestsJob<T> : IJob where T : unmanaged
    {
        /// <summary>Bank whose writer buffers are merged into the shared read buffer.</summary>
        internal RequestBank<T> Bank;

        /// <inheritdoc/>
        public void Execute()
        {
            Bank.Merge();
        }
    }
}
