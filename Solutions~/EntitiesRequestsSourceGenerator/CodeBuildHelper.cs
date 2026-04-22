using Microsoft.CodeAnalysis;

namespace ED.DOTS.EntitiesRequests.SourceGenerator
{
    internal class CodeBuildHelper
    {
        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol _registerRequestAttributeSymbol;

        public CodeBuildHelper(Compilation compilation)
        {
            _compilation = compilation;
            _registerRequestAttributeSymbol = compilation.GetTypeByMetadataName("ED.DOTS.EntitiesRequests.RegisterRequestAttribute");
        }

        public bool IsRegistrationAttribute(AttributeData attribute)
        {
            if (attribute?.AttributeClass == null)
                return false;

            return SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _registerRequestAttributeSymbol);
        }
    }
}