using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// System group that updates all request systems at the end of the simulation frame,
    /// after entity command buffer systems have been executed.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public sealed partial class RequestSystemGroup : ComponentSystemGroup { }
}