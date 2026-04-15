using Unity.Entities;

namespace ED.DOTS.EntitiesRequests
{
    [CreateBefore(typeof(SimulationSystemGroup))]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public sealed partial class RequestSystemGroup : ComponentSystemGroup { }
}