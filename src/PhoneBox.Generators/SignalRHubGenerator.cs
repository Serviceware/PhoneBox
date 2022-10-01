﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
        private static readonly Assembly ThisAssembly = typeof(SignalRHubGenerator).Assembly;
        private static readonly string AttributeTypeName = typeof(SignalRHubGenerationAttribute).FullName;
        private static readonly string DefaultAnnotationsStr = ComputeDefaultAnnotationsStr();
        private static readonly string GeneratedCodeAnnotationStr = ComputeGeneratedCodeAnnotationStr();
        private const string EmbeddedSourcePrefix = $"{nameof(PhoneBox)}.{nameof(Generators)}.EmbeddedSources";
        private const string EnumVarNamesExtension = "x-enum-varnames";
        private const string GeneratedCodeHeader = @"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------";

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
            context.RegisterPostInitializationOutput(RegisterPostInitializationOutput);
        }

        private static void CollectSources(SourceProductionContext context, OpenApiDocumentContainer container, string? rootNamespace, string? assemblyName, SignalRHubGenerationOutputs outputFilter, AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
        {
            AnalyzerConfigOptions options = analyzerConfigOptionsProvider.GetOptions(container.SourceFile);
            string? configuredNamespace = GetMetadataProperty(options, "build_metadata.none.Namespace");
            string? contractNamespace = GetMetadataProperty(options, "build_metadata.none.ContractNamespace");

            ReportOpenApiErrors(context, container.Path, container.Diagnostic);

            string @namespace = configuredNamespace ?? rootNamespace ?? assemblyName ?? "PhoneBox.Generated";
            ICollection<OpenApiHubModel> models = CollectModels(context, container.Path, container.Document.Components.Schemas).ToArray();
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
                    AddInterface(context, interfaceName, @namespace, contractNamespace, hubGroup);

                if (outputFilter.HasFlag(SignalRHubGenerationOutputs.Implementation))
                    AddImplementation(context, className, interfaceName, @namespace, contractNamespace);
            }

            if (outputFilter.HasFlag(SignalRHubGenerationOutputs.Implementation))
                AddExtensions(context, @namespace, hubGroups.Select(x => x.Key));
        }

        private static void RegisterPostInitializationOutput(IncrementalGeneratorPostInitializationContext context)
        {
            foreach (string resourceName in ThisAssembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(EmbeddedSourcePrefix, StringComparison.Ordinal)) 
                    continue;

                string fileName = resourceName.Substring(EmbeddedSourcePrefix.Length + 1);
                int extensionIndex = fileName.LastIndexOf('.');
                if (extensionIndex < 0) 
                    extensionIndex = fileName.Length;

                fileName = fileName.Insert(extensionIndex, ".generated");
                
                string content;
                using (Stream stream = ThisAssembly.GetManifestResourceStream(resourceName)!)
                {
                    using (TextReader reader = new StreamReader(stream))
                    {
                        content = reader.ReadToEnd();
                    }
                }

                context.AddSource(fileName, $@"{GeneratedCodeHeader}

{NormalizeEmbeddedSource(content)}");
            }
        }

        private static void AddImplementation(SourceProductionContext context, string className, string interfaceName, string @namespace, string? contractNamespace)
        {
            string fileName = $"{className}.generated.cs";
            string content = $@"{GeneratedCodeHeader}

namespace {@namespace}
{{
    public partial class {className} : global::Microsoft.AspNetCore.SignalR.Hub<global::{contractNamespace ?? @namespace}.{interfaceName}>
    {{
    }}
}}";
            context.AddSource(fileName, content);
        }

        private static void AddInterface(SourceProductionContext context, string interfaceName, string @namespace, string? contractNamespace, IEnumerable<OpenApiHubMethod> methods)
        {
            string fileName = $"{interfaceName}.generated.cs";
            string methodsStr = String.Join(Environment.NewLine, methods.Select(x => GenerateInterfaceMethod(x, contractNamespace ?? @namespace)));
            string content = $@"{GeneratedCodeHeader}

namespace {@namespace}
{{
{GeneratedCodeAnnotationStr}
    public interface {interfaceName}
    {{
{methodsStr}
    }}
}}";
            context.AddSource(fileName, content);
        }

        private static void AddModel(SourceProductionContext context, string @namespace, OpenApiHubModel model)
        {
            string content = GenerateModel(@namespace, model);
            context.AddSource($"{model.Name}.generated.cs", content);
        }

        private static string GenerateModel(string @namespace, OpenApiHubModel model)
        {
            switch (model)
            {
                case OpenApiHubClass @class: return GenerateClass(@namespace, @class);
                case OpenApiHubEnum @enum: return GenerateEnum(@namespace, @enum);
                default: throw new ArgumentOutOfRangeException(nameof(model), model, null);
            }
        }

        private static string GenerateClass(string @namespace, OpenApiHubClass @class)
        {
            string propertiesStr = String.Join(Environment.NewLine, @class.Properties.Select(x => $"        public {x.TypeName.GetGlobalTypeName(@namespace)} {x.PropertyName} {{ get; }}"));
            string ctorParametersStr = String.Join(", ", @class.Properties.Select(x => $"{x.TypeName.GetGlobalTypeName(@namespace)} {ToCamelCase(x.PropertyName)}"));
            string ctorAssignmentsStr = String.Join(Environment.NewLine, @class.Properties.Select(x => $"            this.{x.PropertyName} = {ToCamelCase(x.PropertyName)};"));
            string content = $@"{GeneratedCodeHeader}

namespace {@namespace}
{{
{DefaultAnnotationsStr}
    public sealed class {@class.Name}
    {{
{propertiesStr}

        public {@class.Name}({ctorParametersStr})
        {{
{ctorAssignmentsStr}
        }}
    }}
}}";
            return content;
        }

        private static string GenerateEnum(string @namespace, OpenApiHubEnum @enum)
        {
            string membersStr = String.Join($",{Environment.NewLine}", @enum.Members.Select(x => $"        {x.Name} = {x.Value}"));
            string content = $@"{GeneratedCodeHeader}

namespace {@namespace}
{{
{GeneratedCodeAnnotationStr}
    public enum {@enum.Name}
    {{
{membersStr}
    }}
}}";
            return content;
        }

        private static void AddExtensions(SourceProductionContext context, string @namespace, IEnumerable<OpenApiHub> hubs)
        {
            string methodsStr = String.Join($"{Environment.NewLine}{Environment.NewLine}", hubs.Select(x => @$"        public static global::Microsoft.AspNetCore.Builder.HubEndpointConventionBuilder MapHub<THub>(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints) where THub : global::{@namespace}.{x.Name}
        {{
            return endpoints.MapHub<THub>(""{x.Path}"");
        }}"));
            string content = $@"{GeneratedCodeHeader}

namespace Microsoft.AspNetCore.Builder
{{
{DefaultAnnotationsStr}
    internal static class HubEndpointRouteBuilderExtensions
    {{
{methodsStr}
    }}
}}";
            context.AddSource("HubEndpointRouteBuilderExtensions.generated.cs", content);
        }

        private static string GenerateInterfaceMethod(OpenApiHubMethod method, string @namespace)
        {
            string parametersStr = String.Join(", ", method.Parameters.Select(x => $"{x.TypeName.GetGlobalTypeName(@namespace)} {x.ParameterName}"));
            string content = $"        global::System.Threading.Tasks.Task {method.MethodName}({parametersStr});";
            return content;
        }

        private static IEnumerable<OpenApiHubModel> CollectModels(SourceProductionContext context, string path, IDictionary<string, OpenApiSchema> schemas)
        {
            foreach (KeyValuePair<string, OpenApiSchema> schemaPair in schemas)
            {
                string schemaName = schemaPair.Key;
                OpenApiSchema modelSchema = schemaPair.Value;

                bool isEnum = modelSchema.Enum.Any();

                OpenApiHubModel model;
                if (isEnum)
                {
                    OpenApiHubEnum @enum = new OpenApiHubEnum(schemaName);
                    model = @enum;

                    IDictionary<int, string> enumNameMap = new Dictionary<int, string>();
                    if (modelSchema.Extensions.TryGetValue(EnumVarNamesExtension, out IOpenApiExtension enumVarNamesValue) && enumVarNamesValue is OpenApiArray enumVarNamesArray)
                    {
                        enumNameMap.AddRange(enumVarNamesArray.Select((x, i) => new KeyValuePair<int, string>(i, ParseEnumVarName(x))));
                    }

                    for (int i = 0; i < modelSchema.Enum.Count; i++)
                    {
                        IOpenApiAny enumMember = modelSchema.Enum[i];
                        OpenApiHubEnumMember? member = ParseEnumMember(context, path, enumMember, i, enumNameMap);
                        if (member != null)
                            @enum.Members.Add(member.Value);
                    }
                }
                else
                {
                    OpenApiHubClass @class = new OpenApiHubClass(schemaName);
                    model = @class;

                    foreach (KeyValuePair<string, OpenApiSchema> propertyPair in modelSchema.Properties)
                    {
                        string propertyName = propertyPair.Key;
                        OpenApiSchema propertySchema = propertyPair.Value;
                        @class.Properties.Add(new OpenApiHubClassProperty(propertyName, GetCSharpTypeName(propertySchema)));
                    }
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

        private static OpenApiTypeName GetCSharpTypeName(OpenApiSchema schema)
        {
            string? typeName = GetOpenApiTypeName(schema, out bool isPrimitive);
            switch (typeName)
            {
                // TODO: Finish implementation
                case null: return new OpenApiTypeName("void", isPrimitive: true);
                case "boolean": return new OpenApiTypeName("bool", isPrimitive: true);
                default: return new OpenApiTypeName(typeName, isPrimitive);
            }
        }
        private static string? GetOpenApiTypeName(OpenApiSchema schema, out bool isPrimitive)
        {
            if (schema.Reference != null)
            {
                isPrimitive = false;
                return schema.Reference.Id;
            }

            if (schema.Type != null)
            {
                isPrimitive = true;
                return schema.Type;
            }

            isPrimitive = false;
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
            OpenApiDocument document = reader.Read(text.ToString(), out OpenApiDiagnostic diagnostic);
            return new OpenApiDocumentContainer(item.Path, item, document, diagnostic);
        }

        private static void ReportOpenApiErrors(SourceProductionContext context, string path, OpenApiDiagnostic diagnostic)
        {
            foreach (OpenApiError error in diagnostic.Errors)
                ReportOpenApiError(DiagnosticSeverity.Error, error, path, context);

            foreach (OpenApiError warning in diagnostic.Warnings)
                ReportOpenApiError(DiagnosticSeverity.Warning, warning, path, context);
        }

        private static void ReportOpenApiError(DiagnosticSeverity severity, OpenApiError error, string path, SourceProductionContext context)
        {
            ReportDiagnostic(severity, ErrorCode.OpenApi, error.Pointer, error.Message, context, path);
        }

        private static void ReportDiagnostic(DiagnosticSeverity severity, string id, string title, string message, SourceProductionContext context, string path)
        {
            DiagnosticDescriptor descriptor = new DiagnosticDescriptor
            (
                id: id
              , title: title
              , messageFormat: message
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

        private static OpenApiHubEnumMember? ParseEnumMember(SourceProductionContext context, string path, IOpenApiAny enumMember, int index, IDictionary<int, string> enumNameMap)
        {
            switch (enumMember)
            {
                case OpenApiInteger @int:
                    int intValue = @int.Value;
                    if (!enumNameMap.TryGetValue(index, out string name))
                    {
                        ReportDiagnostic(DiagnosticSeverity.Error, ErrorCode.OpenApi, "OpenAPI document parsing error", $"Missing enum name for value '{intValue}'. Specify it using the {EnumVarNamesExtension} property.", context, path);
                        return null;
                    }
                    return new OpenApiHubEnumMember(name, intValue);

                case OpenApiString @string:
                    return new OpenApiHubEnumMember(@string.Value, index);

                default: throw new ArgumentOutOfRangeException(nameof(enumMember), enumMember, null);
            }
        }

        private static string ParseEnumVarName(IOpenApiAny value)
        {
            switch (value)
            {
                case OpenApiString @string: return @string.Value;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        private static string NormalizeHubName(string hubName)
        {
            string normalizedHubName = hubName;
            const string hubSuffix = "Hub";
            if (normalizedHubName.EndsWith(hubSuffix, StringComparison.Ordinal))
                normalizedHubName = normalizedHubName.Substring(0, normalizedHubName.Length - hubSuffix.Length);

            return normalizedHubName;
        }

        private static string ComputeDefaultAnnotationsStr() => String.Join(Environment.NewLine, Annotation.All.Select(ComputeAnnotationStr));
        
        private static string ComputeGeneratedCodeAnnotationStr() => ComputeAnnotationStr(Annotation.GeneratedCode);

        private static string ComputeAnnotationStr(Annotation annotation) => $"    [{annotation.Name}{annotation.Arguments}]";

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

        private static string NormalizeEmbeddedSource(string content)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(content);
            EmbeddedSourceNormalizationVisitor visitor = new EmbeddedSourceNormalizationVisitor();
            SyntaxNode normalizedNode = visitor.Visit(syntaxTree.GetRoot())!;
            string normalizedContent = normalizedNode.ToFullString();
            return normalizedContent;
        }

        private sealed class EmbeddedSourceNormalizationVisitor : CSharpSyntaxRewriter
        {
            public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) => node.WithAttributeLists(CreateAttributeLists(node, Annotation.All));

            public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node) => node.WithAttributeLists(CreateAttributeLists(node, EnumerableExtensions.Create(Annotation.GeneratedCode)));

            private static SyntaxList<AttributeListSyntax> CreateAttributeLists(MemberDeclarationSyntax node, IEnumerable<Annotation> annotations)
            {
                IEnumerable<AttributeListSyntax> attributeLists = annotations.Select(x => SyntaxFactory.AttributeList(new SeparatedSyntaxList<AttributeSyntax>().Add(CreateAttribute(x.Name, x.Arguments)))
                                                                                                       .WithLeadingTrivia(SyntaxFactory.Whitespace(new string(' ', 4)))
                                                                                                       .WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)))
                                                                             .Concat(node.AttributeLists);
                return new SyntaxList<AttributeListSyntax>(attributeLists);
            }

            private static AttributeSyntax CreateAttribute(string name, string? arguments)
            {
                AttributeArgumentListSyntax? argumentList = null;
                if (arguments != null)
                    argumentList = SyntaxFactory.ParseAttributeArgumentList(arguments);

                AttributeSyntax attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(name), argumentList);
                return attribute;
            }
        }

        private static class ErrorCode
        {
            private const string Prefix = "HUBGEN";
            
            public const string OpenApi = $"{Prefix}001";
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
            public OpenApiTypeName TypeName { get; }

            public OpenApiHubMethodParameter(string parameterName, OpenApiTypeName typeName)
            {
                this.ParameterName = parameterName;
                this.TypeName = typeName;
            }
        }

        private abstract class OpenApiHubModel
        {
            public string Name { get; }

            protected OpenApiHubModel(string name)
            {
                this.Name = name;
            }
        }

        private sealed class OpenApiHubClass : OpenApiHubModel
        {
            public ICollection<OpenApiHubClassProperty> Properties { get; }

            public OpenApiHubClass(string name) : base(name)
            {
                this.Properties = new Collection<OpenApiHubClassProperty>();
            }
        }

        private readonly struct OpenApiHubClassProperty
        {
            public string PropertyName { get; }
            public OpenApiTypeName TypeName { get; }

            public OpenApiHubClassProperty(string propertyName, OpenApiTypeName typeName)
            {
                this.PropertyName = propertyName;
                this.TypeName = typeName;
            }
        }

        private sealed class OpenApiHubEnum : OpenApiHubModel
        {
            public ICollection<OpenApiHubEnumMember> Members { get; }

            public OpenApiHubEnum(string name) : base(name)
            {
                this.Members = new Collection<OpenApiHubEnumMember>();
            }
        }

        private readonly struct OpenApiHubEnumMember
        {
            public string Name { get; }
            public int Value { get; }

            public OpenApiHubEnumMember(string name, int value)
            {
                this.Name = name;
                this.Value = value;
            }
        }

        private readonly struct OpenApiTypeName
        {
            public string Name { get; }
            public bool IsPrimitive { get; }

            public OpenApiTypeName(string name, bool isPrimitive)
            {
                this.Name = name;
                this.IsPrimitive = isPrimitive;
            }

            public string GetGlobalTypeName(string @namespace)
            {
                StringBuilder sb = new StringBuilder(this.Name);
                if (!this.IsPrimitive)
                    sb.Insert(0, $"global::{@namespace}.");

                string globalTypeName = sb.ToString();
                return globalTypeName;
            }
        }
    }
}