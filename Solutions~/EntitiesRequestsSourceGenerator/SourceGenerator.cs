using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Utilities;

namespace ED.DOTS.EntitiesRequests.SourceGenerator
{
    /// <summary>
    /// Incremental source generator for the requests core (namespace <c>ED.DOTS.EntitiesRequests</c>).
    /// For every type registered with <c>[assembly: RegisterRequest(typeof(T))]</c> it emits the closed
    /// generic component and generic job registrations, a request system and a runtime-init hook that
    /// publishes the owner system index through <c>RequestBridge&lt;T&gt;</c>, all into the very assembly
    /// that declared the attribute.
    /// </summary>
    [Generator]
    public sealed class SourceGenerator : IIncrementalGenerator
    {
        /// <summary>Metadata name of the registration attribute of the core.</summary>
        private const string RegistrationAttributeMetadataName = "ED.DOTS.EntitiesRequests.RegisterRequestAttribute";

        /// <summary>Suffix of the generated per-type request system.</summary>
        private const string SystemSuffix = "_RequestSystem";

        /// <summary>Suffix of the generated runtime-init class that publishes the owner index bridge.</summary>
        private const string BridgeInitSuffix = "_RequestBridgeInit";

        /// <summary>
        /// Namespace of the core. Generated code qualifies every core type with it, so a consumer
        /// type of the same short name, declared in the request type's namespace, cannot shadow it.
        /// </summary>
        private const string CoreNamespace = "ED.DOTS.EntitiesRequests";

        private readonly FileLogger _logger = new FileLogger("EntitiesRequests");

        /// <summary>Builds the incremental pipeline: assembly attributes to request models to sources.</summary>
        /// <param name="context">Incremental generator context.</param>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var requests = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    RegistrationAttributeMetadataName,
                    IsCompilationUnit,
                    CollectRequests)
                .SelectMany(FlattenModels);

            context.RegisterSourceOutput(requests, Emit);
        }

        private static bool IsCompilationUnit(SyntaxNode node, CancellationToken cancellationToken)
        {
            return node is CompilationUnitSyntax;
        }

        private static ImmutableArray<RequestModel> FlattenModels(ImmutableArray<RequestModel> models, CancellationToken cancellationToken)
        {
            return models;
        }

        private ImmutableArray<RequestModel> CollectRequests(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var builder = ImmutableArray.CreateBuilder<RequestModel>();
            for (var i = 0; i < context.Attributes.Length; i++)
            {
                var typeSymbol = ReadRequestType(context.Attributes[i]);
                if (typeSymbol == null)
                {
                    continue;
                }

                if (typeSymbol.ContainingType != null)
                {
                    _logger.Error($"{nameof(SourceGenerator)}: nested request types are not supported: {typeSymbol.ToDisplayString()}.");
                    continue;
                }

                builder.Add(RequestModel.Create(typeSymbol));
            }

            return builder.ToImmutable();
        }

        private INamedTypeSymbol ReadRequestType(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length != 1)
            {
                _logger.Error($"{nameof(SourceGenerator)}: RegisterRequest expects exactly one argument.");
                return null;
            }

            var argument = attribute.ConstructorArguments[0];
            if (argument.Kind == TypedConstantKind.Error)
            {
                _logger.Error($"{nameof(SourceGenerator)}: RegisterRequest argument failed to bind to a type.");
                return null;
            }

            var typeSymbol = argument.Value as INamedTypeSymbol;
            if (typeSymbol == null)
            {
                _logger.Error($"{nameof(SourceGenerator)}: RegisterRequest argument is not a named type.");
                return null;
            }

            return typeSymbol;
        }

        private void Emit(SourceProductionContext context, RequestModel model)
        {
            var hintName = $"{model.Name}{SystemSuffix}.g.cs";
            var sourceCode = GenerateCode(model);
            context.AddSource(hintName, SourceText.From(sourceCode, Encoding.UTF8));
            _logger.Info($"{nameof(SourceGenerator)}: generated {hintName} for {model.FullName}.");
        }

        private static string GenerateCode(RequestModel model)
        {
            var systemName = model.Name + SystemSuffix;
            var hasNamespace = !string.IsNullOrWhiteSpace(model.Namespace);
            var indent = hasNamespace ? "    " : string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated/>");
            builder.AppendLine("using Unity.Burst;");
            builder.AppendLine("using Unity.Entities;");
            builder.AppendLine("using Unity.Jobs;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.AppendLine($"[assembly: RegisterGenericComponentType(typeof({CoreNamespace}.RequestSingleton<{model.FullName}>))]");
            builder.AppendLine($"[assembly: RegisterGenericJobType(typeof({CoreNamespace}.MergeRequestsJob<{model.FullName}>))]");
            builder.AppendLine();

            if (hasNamespace)
            {
                builder.AppendLine($"namespace {model.Namespace}");
                builder.AppendLine("{");
            }

            builder.AppendLine($"{indent}[UpdateInGroup(typeof({CoreNamespace}.RequestSystemGroup))]");
            builder.AppendLine($"{indent}[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation");
            builder.AppendLine($"{indent}                   | WorldSystemFilterFlags.ServerSimulation");
            builder.AppendLine($"{indent}                   | WorldSystemFilterFlags.LocalSimulation)]");
            builder.AppendLine($"{indent}[BurstCompile]");
            builder.AppendLine($"{indent}public partial struct {systemName} : ISystem");
            builder.AppendLine($"{indent}{{");
            builder.AppendLine($"{indent}    [BurstCompile]");
            builder.AppendLine($"{indent}    public void OnCreate(ref SystemState state)");
            builder.AppendLine($"{indent}    {{");
            builder.AppendLine($"{indent}        {CoreNamespace}.RequestOwner<{model.FullName}>.OnCreate(ref state);");
            builder.AppendLine($"{indent}    }}");
            builder.AppendLine();
            builder.AppendLine($"{indent}    [BurstCompile]");
            builder.AppendLine($"{indent}    public void OnUpdate(ref SystemState state)");
            builder.AppendLine($"{indent}    {{");
            builder.AppendLine($"{indent}        {CoreNamespace}.RequestOwner<{model.FullName}>.OnUpdate(ref state);");
            builder.AppendLine($"{indent}    }}");
            builder.AppendLine();
            builder.AppendLine($"{indent}    [BurstCompile]");
            builder.AppendLine($"{indent}    public void OnDestroy(ref SystemState state)");
            builder.AppendLine($"{indent}    {{");
            builder.AppendLine($"{indent}        {CoreNamespace}.RequestOwner<{model.FullName}>.OnDestroy(ref state);");
            builder.AppendLine($"{indent}    }}");
            builder.AppendLine($"{indent}}}");
            builder.AppendLine();
            builder.AppendLine($"{indent}internal static class {model.Name}{BridgeInitSuffix}");
            builder.AppendLine($"{indent}{{");
            builder.AppendLine($"{indent}    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]");
            builder.AppendLine($"{indent}#if UNITY_EDITOR");
            builder.AppendLine($"{indent}    [UnityEditor.InitializeOnLoadMethod]");
            builder.AppendLine($"{indent}#endif");
            builder.AppendLine($"{indent}    private static void Init()");
            builder.AppendLine($"{indent}    {{");
            builder.AppendLine($"{indent}        TypeManager.Initialize();");
            builder.AppendLine($"{indent}        {CoreNamespace}.RequestBridge<{model.FullName}>.Publish(TypeManager.GetSystemTypeIndex<{systemName}>());");
            builder.AppendLine($"{indent}    }}");
            builder.AppendLine($"{indent}}}");

            if (hasNamespace)
            {
                builder.AppendLine("}");
            }

            return builder.ToString();
        }
    }
}
