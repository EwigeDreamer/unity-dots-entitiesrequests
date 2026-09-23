using System;
using Microsoft.CodeAnalysis;

namespace ED.DOTS.EntitiesRequests.SourceGeneratorNew
{
    /// <summary>
    /// Value-equatable description of a registered request type. Identity of the model is
    /// <see cref="FullName"/>; <see cref="Name"/> and <see cref="Namespace"/> carry emission data only.
    /// Incremental generators require value equality so that unchanged models are not re-emitted
    /// on every compilation.
    /// </summary>
    internal readonly struct RequestModel : IEquatable<RequestModel>
    {
        /// <summary>Short type name, for example <c>SampleRequest</c>.</summary>
        internal readonly string Name;

        /// <summary>Namespace of the request type; empty for the global namespace.</summary>
        internal readonly string Namespace;

        /// <summary>Namespace-qualified type name usable from generated code. Also the identity of the model.</summary>
        internal readonly string FullName;

        private RequestModel(string name, string @namespace, string fullName)
        {
            Name = name;
            Namespace = @namespace;
            FullName = fullName;
        }

        /// <summary>Creates a model from a request type symbol.</summary>
        /// <param name="symbol">The registered request type.</param>
        /// <returns>A value-equatable model.</returns>
        internal static RequestModel Create(INamedTypeSymbol symbol)
        {
            var name = symbol.Name;
            var @namespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString();
            var fullName = string.IsNullOrWhiteSpace(@namespace) ? name : $"{@namespace}.{name}";
            return new RequestModel(name, @namespace, fullName);
        }

        /// <inheritdoc/>
        public bool Equals(RequestModel other)
        {
            return FullName == other.FullName;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RequestModel other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return FullName.GetHashCode();
        }
    }
}
