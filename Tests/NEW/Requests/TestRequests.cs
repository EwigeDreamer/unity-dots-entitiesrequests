using ED.DOTS.EntitiesRequests.Tmp;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tmp.Tests.TestRequest_1))]
[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tmp.Tests.TestRequest_2))]
[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tmp.Tests.TaggedTestRequest))]

namespace ED.DOTS.EntitiesRequests.Tmp.Tests
{
    /// <summary>
    /// General-purpose request type shared by the ECS fixtures. Defined once so that fixtures do not
    /// need a type of their own.
    /// </summary>
    public struct TestRequest_1
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Second independent request type. Exists so that a world can host several request types at once,
    /// which is what the multi-type fixture verifies.
    /// </summary>
    public struct TestRequest_2
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>Request type carrying the identity of the writer that produced it.</summary>
    public struct TaggedTestRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;

        /// <summary>Writer that produced the request.</summary>
        public int WriterId;
    }
}
