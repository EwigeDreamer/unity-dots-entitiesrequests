using Unity.Burst;
using Unity.Jobs;

namespace ED.DOTS.EntitiesRequests.Tmp
{
    /// <summary>
    /// Moves every pending write into the shared read buffer. Single-threaded and exclusive: the merge
    /// appends to the read buffer and may grow it. Ordering against writers and readers is given by the
    /// marker fence declared on the owner system, so the scheduler runs this job alone and after them.
    /// </summary>
    /// <typeparam name="T">Unmanaged request type.</typeparam>
    [BurstCompile]
    internal struct MergeRequestsJob<T> : IJob where T : unmanaged
    {
        /// <summary>Bank whose writer buffers are merged into the shared read buffer.</summary>
        public RequestBank<T> Bank;

        /// <inheritdoc/>
        public void Execute()
        {
            Bank.Merge();
        }
    }
}
