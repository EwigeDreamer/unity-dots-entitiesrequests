using System;

namespace ED.DOTS.EntitiesRequests
{
    /// <summary>
    /// Attribute used to mark an assembly as containing a specific request type.
    /// The source generator will create a request system for each registered type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class RegisterRequestAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RegisterRequestAttribute"/> class.
        /// </summary>
        /// <param name="requestType">The type of request to register.</param>
        public RegisterRequestAttribute(Type requestType) { }
    }
}