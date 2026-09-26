using System;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Marks an assembly as containing a request type of the new requests core.
    /// The source generator creates a request system for every registered type
    /// in the very assembly that declares the attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class RegisterRequestAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RegisterRequestAttribute"/> class.
        /// </summary>
        /// <param name="requestType">The request type to register.</param>
        public RegisterRequestAttribute(Type requestType) { }
    }
}
