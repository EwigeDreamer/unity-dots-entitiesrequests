using System;

namespace ED.DOTS.EntitiesRequests
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class RegisterRequestAttribute : Attribute
    {
        public RegisterRequestAttribute(Type type) { }
    }
}