using System.Linq;
using Microsoft.CodeAnalysis;

namespace EntitiesRequestsSourceGenerator
{
    public static class CodeBuildHelper
    {
        private static readonly string[] PossibleAttributeNames = new[]
        {
            "ED.DOTS.EntitiesRequests.RegisterRequestAttribute",
            "ED.DOTS.EntitiesRequests.RegisterRequest",
            "RegisterRequestAttribute",
            "RegisterRequest",
        };

        public static bool IsRegistrationAttribute(AttributeData attribute)
        {
            if (attribute?.AttributeClass != null)
                return PossibleAttributeNames.Any(attribute.AttributeClass.Name.Equals);
            return false;
        }
    }
}