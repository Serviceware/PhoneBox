using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
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
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<string?> rootNamespace = context.AnalyzerConfigOptionsProvider.Select((x, _) => GetRootNamespace(x));
            IncrementalValueProvider<string?> assemblyName = context.CompilationProvider.Select((x, _) => x.AssemblyName);
            IncrementalValuesProvider<OpenApiDocumentContainer> documents = context.AdditionalTextsProvider.Where(static file => file.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                                                                                   .Select(LoadYamlFile)
                                                                                   .Where(x => x.HasValue)
                                                                                   .Select((x, _) => x!.Value);

            var all = documents.Combine(context.AnalyzerConfigOptionsProvider)
                               .Combine(rootNamespace)
                               .Combine(assemblyName)
                               .Select(static (x, _) => new
                               {
                                   Container = x.Left.Left.Left,
                                   RootNamespace = x.Left.Right,
                                   AssemblyName = x.Right,
                                   AnalyzerConfigOptionsProvider = x.Left.Left.Right
                               });

            context.RegisterSourceOutput(all, (x, y) => CollectSources(x, y.Container, y.RootNamespace, y.AssemblyName, y.AnalyzerConfigOptionsProvider));
        }

        private static void CollectSources(SourceProductionContext context, OpenApiDocumentContainer container, string? rootNamespace, string? assemblyName, AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
        {
            AnalyzerConfigOptions options = analyzerConfigOptionsProvider.GetOptions(container.SourceFile);
            _ = options.TryGetValue("build_metadata.none.Namespace", out string? configuredNamespace);

            if (configuredNamespace == "")
                configuredNamespace = null;

            ReportOpenApiErrors(context, container.Path, container.Diagnostic);

            string? @namespace = configuredNamespace ?? rootNamespace ?? assemblyName;
            ICollection<OpenApiHubContract> contracts = CollectContracts(container.Document.Components.Schemas).ToArray();
            if (contracts.Any())
            {
                context.AddSource("_Model.generated.cs", GenerateModels(@namespace, contracts));
            }

            foreach (IGrouping<string, OpenApiHubMethod> hub in CollectHubMethods(container).GroupBy(x => x.HubName))
            {
                string hubName = hub.Key;
                string fileName = $"{hubName}.generated.cs";
                context.AddSource(fileName, GenerateInterface(hubName, @namespace, hub));
            }
        }

        private static string GenerateInterface(string hubName, string? @namespace, IEnumerable<OpenApiHubMethod> methods)
        {
            string normalizedHubName = hubName;
            int hubSuffixIndex = normalizedHubName.LastIndexOf("Hub", StringComparison.Ordinal);
            if (hubSuffixIndex > 0) 
                normalizedHubName = normalizedHubName.Substring(0, hubSuffixIndex);

            string methodsStr = String.Join(Environment.NewLine, methods.Select(GenerateMethod));
            string content = $@"using System.Threading.Tasks;

namespace {@namespace}
{{
    public interface I{normalizedHubName}Hub
    {{
{methodsStr}
    }}
}}";
            return content;
        }

        private static string GenerateModels(string? @namespace, IEnumerable<OpenApiHubContract> contracts)
        {
            string contractsStr = String.Join($"{Environment.NewLine}{Environment.NewLine}", contracts.Select(GenerateModel));
            string content = $@"namespace {@namespace}
{{
{contractsStr}
}}";
            return content;
        }

        private static string GenerateModel(OpenApiHubContract contract)
        {
            string propertiesStr = String.Join(Environment.NewLine, contract.Properties.Select(x => $"        public {x.TypeName} {x.PropertyName} {{ get; }}"));
            string ctorParametersStr = String.Join(", ", contract.Properties.Select(x => $"{x.TypeName} {ToCamelCase(x.PropertyName)}"));
            string ctorAssignmentsStr = String.Join(Environment.NewLine, contract.Properties.Select(x => $"            this.{x.PropertyName} = {ToCamelCase(x.PropertyName)};"));
            string content = $@"    public sealed class {contract.Name}
    {{
{propertiesStr}

        public {contract.Name}({ctorParametersStr})
        {{
{ctorAssignmentsStr}
        }}
    }}";
            return content;
        }

        private static string GenerateMethod(OpenApiHubMethod x)
        {
            string parametersStr = String.Join(", ", x.Parameters.Select(x => $"{x.TypeName} {x.ParameterName}"));
            string content = $"        Task {x.MethodName}({parametersStr});";
            return content;
        }

        private static IEnumerable<OpenApiHubContract> CollectContracts(IDictionary<string, OpenApiSchema> schemas)
        {
            foreach (KeyValuePair<string, OpenApiSchema> schemaPair in schemas)
            {
                string schemaName = schemaPair.Key;
                OpenApiSchema contractSchema = schemaPair.Value;

                OpenApiHubContract contract = new OpenApiHubContract(schemaName);

                foreach (KeyValuePair<string, OpenApiSchema> propertyPair in contractSchema.Properties)
                {
                    string propertyName = propertyPair.Key;
                    OpenApiSchema propertySchema = propertyPair.Value;
                    contract.Properties.Add(new OpenApiHubContractProperty(propertyName, GetCSharpTypeName(propertySchema)));
                }

                yield return contract;
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
                    OpenApiHubMethod hubMethod = new OpenApiHubMethod(hubName, methodName);

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

        private static string? GetRootNamespace(AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
        {
            return analyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.rootnamespace", out string? rootNamespace) ? rootNamespace : null;
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

        private readonly struct OpenApiHubMethod
        {
            public string HubName { get; }
            public string MethodName { get; }
            public ICollection<OpenApiHubMethodParameter> Parameters { get; }

            public OpenApiHubMethod(string hubName, string methodName)
            {
                this.HubName = hubName;
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

        private readonly struct OpenApiHubContract
        {
            public string Name { get; }
            public ICollection<OpenApiHubContractProperty> Properties { get; }

            public OpenApiHubContract(string name)
            {
                this.Name = name;
                this.Properties = new Collection<OpenApiHubContractProperty>();
            }
        }

        private readonly struct OpenApiHubContractProperty
        {
            public string PropertyName { get; }
            public string TypeName { get; }

            public OpenApiHubContractProperty(string propertyName, string typeName)
            {
                this.PropertyName = propertyName;
                this.TypeName = typeName;
            }
        }
    }
}