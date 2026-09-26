using ED.DOTS.EntitiesRequests;

// Two request types that deliberately share the same short name ("CollisionRequest") but live in
// different namespaces, both registered in this one assembly. This is the case that used to break the
// source generator: the generated file name was derived from the short type name alone, so both types
// claimed the same "CollisionRequest_RequestSystem.g.cs" hint and the build failed with a duplicate
// hint name. The generator now disambiguates the file name with a hash of the full type name, so this
// file compiling at all is the compile-time regression guard. The runtime side is covered by
// CollisionTests.
[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.CollisionOne.CollisionRequest))]
[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.CollisionTwo.CollisionRequest))]

namespace ED.DOTS.EntitiesRequests.Tests.CollisionOne
{
    /// <summary>Request type of the first colliding namespace; shares its short name on purpose.</summary>
    public struct CollisionRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }
}

namespace ED.DOTS.EntitiesRequests.Tests.CollisionTwo
{
    /// <summary>Request type of the second colliding namespace; shares its short name on purpose.</summary>
    public struct CollisionRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }
}
