using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Updates all request owners at the end of the simulation frame, after the late simulation
    /// systems have run, so that every writer of the simulation phase is already done.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(LateSimulationSystemGroup))]
    public sealed partial class RequestSystemGroup : ComponentSystemGroup { }
}
