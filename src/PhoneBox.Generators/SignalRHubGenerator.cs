using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace PhoneBox.Generators
{
    [Generator]
    public sealed class SignalRHubGenerator : IIncrementalGenerator
    {
        private static readonly string AttributeTypeName = typeof(SignalRHubGenerationAttribute).FullName;

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<string?> rootNamespace = context.AnalyzerConfigOptionsProvider.Select((x, _) => GetRootNamespace(x));
            IncrementalValueProvider<string?> assemblyName = context.CompilationProvider.Select((x, _) => x.AssemblyName);
            IncrementalValuesProvider<SignalRHubGenerationOutputs?> outputFilter = context.SyntaxProvider.CreateSyntaxProvider(IsAssemblyAttribute, CollectOutputFilter);
            IncrementalValuesProvider<OpenApiDocumentContainer> documents = context.AdditionalTextsProvider.Where(static file => file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                                                                                   .Select(LoadYamlFile)
                                                                                   .Where(x => x.HasValue)
                                                                                   .Select((x, _) => x!.Value);

            var all = documents.Combine(context.AnalyzerConfigOptionsProvider)
                               .Combine(outputFilter.Collect())
                               .Combine(rootNamespace)
                               .Combine(assemblyName)
                               .Select(static (x, _) => new
                               {
                                   Container = x.Left.Left.Left.Left,
                                   RootNamespace = x.Left.Right,
                                   AssemblyName = x.Right,
                                   OutputFilter = x.Left.Left.Right.FirstOrDefault(x => x != null) ?? SignalRHubGenerationOutputs.All,
                                   AnalyzerConfigOptionsProvider = x.Left.Left.Left.Right
                               });

            context.RegisterSourceOutput(all, (x, y) => CollectSources(x, y.Container, y.RootNamespace, y.AssemblyName, y.OutputFilter, y.AnalyzerConfigOptionsProvider));
        }

        private static void CollectSources(SourceProductionContext context, OpenApiDocumentContainer container, string? rootNamespace, string? assemblyName, SignalRHubGenerationOutputs outputFilter, AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
        {
            AnalyzerConfigOptions options = analyzerConfigOptionsProvider.GetOptions(container.SourceFile);
            string? configuredNamespace = GetMetadataProperty(options, "build_metadata.none.Namespace");
            string? contractNamespace = GetMetadataProperty(options, "build_metadata.none.ContractNamespace");

            ReportOpenApiErrors(context, container.Path, container.Diagnostic);

            string? @namespace = configuredNamespace ?? rootNamespace ?? assemblyName;
            ICollection<OpenApiHubModel> models = CollectModels(container.Document.Components.Schemas).ToArray();
            if (models.Any() && outputFilter.HasFlag(SignalRHubGenerationOutputs.Model))
            {
                foreach (OpenApiHubModel model in models)
                {
                    AddModel(context, @namespace, model);
                }
            }

            var hubGroups = CollectHubMethods(container).GroupBy(x => x.Hub).ToArray();
            foreach (IGrouping<OpenApiHub, OpenApiHubMethod> hubGroup in hubGroups)
            {
                string hubName = NormalizeHubName(hubGroup.Key.Name);
                string interfaceName = $"I{hubName}Hub";
                string className = $"{hubName}Hub";

                if (outputFilter.HasFlag(SignalRHubGenerationOutputs.Interface))
                    AddInterface(context, interfaceName, @namespace, hubGroup);

                if (outputFilter.HasFlag(SignalRHubGenerationOutputs.Implementation))
                    AddImplementation(context, className, interfaceName, @namespace, contractNamespace, hubGroup);
            }

            if (outputFilter.HasFlag(SignalRHubGenerationOutputs.Implementation))
                AddExtensions(context, @namespace, hubGroups.Select(x => x.Key));
        }

        private static void AddImplementation(SourceProductionContext context, string className, string interfaceName, string? @namespace, string? contractNamespace, IEnumerable<OpenApiHubMethod> methods)
        {
            ICollection<string> usings = new SortedSet<string>();
            usings.Add("Microsoft.AspNetCore.SignalR");

            if (contractNamespace != null)
                usings.Add(contractNamespace);

            string usingsStr = String.Join(Environment.NewLine, usings.Select(x => $"using {x};"));
            string fileName = $"{className}.generated.cs";
            string content = $@"{usingsStr}

namespace {@namespace}
{{
    public partial class {className} : Hub<{interfaceName}>
    {{
    }}
}}";
            context.AddSource(fileName, content);
        }

        private static void AddInterface(SourceProductionContext context, string interfaceName, string? @namespace, IEnumerable<OpenApiHubMethod> methods)
        {
            string fileName = $"{interfaceName}.generated.cs";
            string methodsStr = String.Join(Environment.NewLine, methods.Select(GenerateInterfaceMethod));
            string content = $@"using System.Threading.Tasks;

namespace {@namespace}
{{
    public interface {interfaceName}
    {{
{methodsStr}
    }}
}}";
            context.AddSource(fileName, content);
        }

        private static void AddModel(SourceProductionContext context, string? @namespace, OpenApiHubModel model)
        {
            string propertiesStr = String.Join(Environment.NewLine, model.Properties.Select(x => $"        public {x.TypeName} {x.PropertyName} {{ get; }}"));
            string ctorParametersStr = String.Join(", ", model.Properties.Select(x => $"{x.TypeName} {ToCamelCase(x.PropertyName)}"));
            string ctorAssignmentsStr = String.Join(Environment.NewLine, model.Properties.Select(x => $"            this.{x.PropertyName} = {ToCamelCase(x.PropertyName)};"));
            string content = $@"namespace {@namespace}
{{
    public sealed class {model.Name}
    {{
{propertiesStr}

        public {model.Name}({ctorParametersStr})
        {{
{ctorAssignmentsStr}
        }}
    }}
}}";
            context.AddSource($"{model.Name}.generated.cs", content);
        }

        private static void AddExtensions(SourceProductionContext context, string? @namespace, IEnumerable<OpenApiHub> hubs)
        {
            ICollection<string> usings = new SortedSet<string>();
            usings.Add("Microsoft.AspNetCore.Routing");

            if (@namespace != null)
                usings.Add(@namespace);

            string usingsStr = String.Join(Environment.NewLine, usings.Select(x => $"using {x};"));
            string methodsStr = String.Join($"{Environment.NewLine}{Environment.NewLine}", hubs.Select(x => @$"        public static HubEndpointConventionBuilder MapHub<THub>(this IEndpointRouteBuilder endpoints) where THub : {x.Name}
        {{
            return endpoints.MapHub<THub>(""{x.Path}"");
        }}"));
            string content = $@"{usingsStr}

namespace Microsoft.AspNetCore.Builder
{{
    internal static class HubEndpointRouteBuilderExtensions
    {{
{methodsStr}
    }}
}}";
            context.AddSource("HubEndpointRouteBuilderExtensions.generated.cs", content);
        }

        private static string GenerateInterfaceMethod(OpenApiHubMethod method)
        {
            string parametersStr = String.Join(", ", method.Parameters.Select(x => $"{x.TypeName} {x.ParameterName}"));
            string content = $"        Task {method.MethodName}({parametersStr});";
            return content;
        }

        private static IEnumerable<OpenApiHubModel> CollectModels(IDictionary<string, OpenApiSchema> schemas)
        {
            foreach (KeyValuePair<string, OpenApiSchema> schemaPair in schemas)
            {
                string schemaName = schemaPair.Key;
                OpenApiSchema modelSchema = schemaPair.Value;

                OpenApiHubModel model = new OpenApiHubModel(schemaName);

                foreach (KeyValuePair<string, OpenApiSchema> propertyPair in modelSchema.Properties)
                {
                    string propertyName = propertyPair.Key;
                    OpenApiSchema propertySchema = propertyPair.Value;
                    model.Properties.Add(new OpenApiHubModelProperty(propertyName, GetCSharpTypeName(propertySchema)));
                }

                yield return model;
            }
        }

        private static IEnumerable<OpenApiHubMethod> CollectHubMethods(OpenApiDocumentContainer container)
        {
            foreach (KeyValuePair<string, OpenApiPathItem> pathPair in container.Document.Paths)
            {
                string path = pathPair.Key;
                OpenApiPathItem group = pathPair.Value;

                foreach (KeyValuePair<OperationType, OpenApiOperation> operationPair in group.Operations)
                {
                    OperationType method = operationPair.Key;
                    OpenApiOperation operation = operationPair.Value;
                    bool isWebSocket = IsWebSocket(operation);
                    if (!isWebSocket)
                        continue;

                    string hubName = GetHubName(operation);
                    string methodName = operation.OperationId;
                    string hubPath = GetHubPath(path);
                    OpenApiHub hub = new OpenApiHub(hubName, hubPath);
                    OpenApiHubMethod hubMethod = new OpenApiHubMethod(hub, methodName);

                    OpenApiSchema? bodySchema = operation.RequestBody?.Content.FirstOrDefault().Value?.Schema;
                    if (bodySchema != null)
                        hubMethod.Parameters.Add(new OpenApiHubMethodParameter("content", GetCSharpTypeName(bodySchema)));

                    foreach (OpenApiParameter parameter in operation.Parameters)
                    {
                        hubMethod.Parameters.Add(new OpenApiHubMethodParameter(parameter.Name, GetCSharpTypeName(parameter.Schema)));
                    }

                    yield return hubMethod;
                }
            }
        }

        private static string GetCSharpTypeName(OpenApiSchema schema)
        {
            string? typeName = GetOpenApiTypeName(schema);
            switch (typeName)
            {
                // TODO: Finish implementation
                case null:
                    return "void";

                case "boolean":
                    return "bool";

                default:
                    return typeName;
            }
        }
        private static string? GetOpenApiTypeName(OpenApiSchema schema)
        {
            if (schema.Reference != null)
                return schema.Reference.Id;

            if (schema.Type != null)
                return schema.Type;

            return null;
        }

        private static bool IsWebSocket(IOpenApiExtensible extensible)
        {
            if (!extensible.Extensions.TryGetValue("x-websocket", out IOpenApiExtension extension))
                return false;

            return extension is OpenApiBoolean { Value: true };
        }

        private static string GetHubName(OpenApiOperation operation) => operation.Tags.Any() ? operation.Tags[0].Name : "Default";

        private static string GetHubPath(string path)
        {
            int pathEndIndex = path.IndexOf('/', 1);
            if (pathEndIndex > 0)
            {
                string rootPath = path.Substring(0, pathEndIndex);
                return rootPath;
            }
            return path;
        }

        private static OpenApiDocumentContainer? LoadYamlFile(AdditionalText item, CancellationToken cancellationToken)
        {
            SourceText? text = item.GetText(cancellationToken);
            if (text == null)
                return null;

            OpenApiStringReader reader = new OpenApiStringReader();

            // TODO: What happens if the yaml file does not represent an OpenAPI document. Exception?
            OpenApiDocument document = reader.Read(text!.ToString(), out OpenApiDiagnostic diagnostic);
            return new OpenApiDocumentContainer(item.Path, item, document, diagnostic);
        }

        private static void ReportOpenApiErrors(SourceProductionContext context, string path, OpenApiDiagnostic diagnostic)
        {
            foreach (OpenApiError error in diagnostic.Errors)
                ReportOpenApiError(context, path, error, DiagnosticSeverity.Error);

            foreach (OpenApiError warning in diagnostic.Warnings)
                ReportOpenApiError(context, path, warning, DiagnosticSeverity.Warning);
        }

        private static void ReportOpenApiError(SourceProductionContext context, string path, OpenApiError error, DiagnosticSeverity severity)
        {
            DiagnosticDescriptor descriptor = new DiagnosticDescriptor
            (
                id: $"{nameof(SignalRHubGenerator)}001"
              , title: error.Pointer
              , messageFormat: error.Message
              , category: nameof(SignalRHubGenerator)
              , defaultSeverity: severity
              , isEnabledByDefault: true
            );
            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.Create(path, new TextSpan(), new LinePositionSpan())));
        }

        private static bool IsAssemblyAttribute(SyntaxNode node, CancellationToken cancellationToken)
        {
            return node is AttributeListSyntax { Target: { } } attributeList && attributeList.Target.Identifier.IsKind(SyntaxKind.AssemblyKeyword);
        }

        private static SignalRHubGenerationOutputs? CollectOutputFilter(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            AttributeListSyntax attributeList = (AttributeListSyntax)context.Node;
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol attributeSymbol)
                    return null;

                string displayString = attributeSymbol.ContainingType.ToDisplayString();
                if (displayString != AttributeTypeName)
                    return null;

                if (attribute.ArgumentList?.Arguments == null)
                    return null;

                SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;
                if (!arguments.Any())
                    return null;

                Optional<object?> outputFilterValue = context.SemanticModel.GetConstantValue(arguments[0].Expression);
                if (!outputFilterValue.HasValue)
                    return null;

                SignalRHubGenerationOutputs? outputFilter = (SignalRHubGenerationOutputs?)(int?)outputFilterValue.Value;
                return outputFilter;
            }
            return null;
        }

        private static string? GetRootNamespace(AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
        {
            return analyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.rootnamespace", out string? rootNamespace) ? rootNamespace : null;
        }

        private static string? GetMetadataProperty(AnalyzerConfigOptions options, string key)
        {
            _ = options.TryGetValue(key, out string? value);

            if (value == "")
                value = null;

            return value;
        }

        private static string NormalizeHubName(string hubName)
        {
            string normalizedHubName = hubName;
            const string hubSuffix = "Hub";
            if (normalizedHubName.EndsWith(hubSuffix, StringComparison.Ordinal))
                normalizedHubName = normalizedHubName.Substring(0, normalizedHubName.Length - hubSuffix.Length);

            return normalizedHubName;
        }

        private static string ToCamelCase(string s)
        {
            if (String.IsNullOrEmpty(s) || !Char.IsUpper(s[0]))
            {
                return s;
            }

            char[] chars = s.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !Char.IsUpper(chars[i]))
                {
                    break;
                }

                bool hasNext = i + 1 < chars.Length;
                if (i > 0 && hasNext && !Char.IsUpper(chars[i + 1]))
                {
                    // if the next character is a space, which is not considered uppercase 
                    // (otherwise we wouldn't be here...)
                    // we want to ensure that the following:
                    // 'FOO bar' is rewritten as 'foo bar', and not as 'foO bar'
                    // The code was written in such a way that the first word in uppercase
                    // ends when if finds an uppercase letter followed by a lowercase letter.
                    // now a ' ' (space, (char)32) is considered not upper
                    // but in that case we still want our current character to become lowercase
                    if (Char.IsSeparator(chars[i + 1]))
                    {
                        chars[i] = Char.ToLowerInvariant(chars[i]);
                    }

                    break;
                }

                chars[i] = Char.ToLowerInvariant(chars[i]);
            }

            return new String(chars);
        }

        private readonly struct OpenApiDocumentContainer
        {
            public string Path { get; }
            public AdditionalText SourceFile { get; }
            public OpenApiDocument Document { get; }
            public OpenApiDiagnostic Diagnostic { get; }

            public OpenApiDocumentContainer(string path, AdditionalText sourceFile, OpenApiDocument document, OpenApiDiagnostic diagnostic)
            {
                this.Path = path;
                this.SourceFile = sourceFile;
                this.Document = document;
                this.Diagnostic = diagnostic;
            }
        }

        private readonly struct OpenApiHub
        {
            public string Name { get; }
            public string Path { get; }

            public OpenApiHub(string name, string path)
            {
                this.Name = name;
                this.Path = path;
            }
        }

        private readonly struct OpenApiHubMethod
        {
            public OpenApiHub Hub { get; }
            public string MethodName { get; }
            public ICollection<OpenApiHubMethodParameter> Parameters { get; }

            public OpenApiHubMethod(OpenApiHub hub, string methodName)
            {
                this.Hub = hub;
                this.MethodName = methodName;
                this.Parameters = new Collection<OpenApiHubMethodParameter>();
            }
        }

        private readonly struct OpenApiHubMethodParameter
        {
            public string ParameterName { get; }
            public string TypeName { get; }

            public OpenApiHubMethodParameter(string parameterName, string typeName)
            {
                this.ParameterName = parameterName;
                this.TypeName = typeName;
            }
        }

        private readonly struct OpenApiHubModel
        {
            public string Name { get; }
            public ICollection<OpenApiHubModelProperty> Properties { get; }

            public OpenApiHubModel(string name)
            {
                this.Name = name;
                this.Properties = new Collection<OpenApiHubModelProperty>();
            }
        }

        private readonly struct OpenApiHubModelProperty
        {
            public string PropertyName { get; }
            public string TypeName { get; }

            public OpenApiHubModelProperty(string propertyName, string typeName)
            {
                this.PropertyName = propertyName;
                this.TypeName = typeName;
            }
        }
    }
}