using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Dibix.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PhoneBox.Generators.Tests
{
    [TestClass]
    public sealed class GeneratorTest : TestBase
    {
        [TestMethod]
        public void OpenApiGenerator_Contracts() => CompileContracts(assertOutputs: true);

        [TestMethod]
        public void OpenApiGenerator_Implementation()
        {
            Compilation contractCompilation = CompileContracts(assertOutputs: false);

            string contractAssemblyFilePath = Path.Combine(TestDirectory, $"{typeof(GeneratorTest).Namespace}.Contracts.generated.dll");
            EmitResult emitResult = contractCompilation.Emit(contractAssemblyFilePath);
            RoslynUtility.VerifyCompilation(emitResult.Diagnostics);
            Assert.IsTrue(emitResult.Success, "emitResult.Success");

            _ = RunGenerator
            (
                filter: "PhoneBox.Generators.SignalRHubGenerationOutputs.Implementation"
              , metadataNamespace: null
              , metadataHubNamespace: $"{typeof(GeneratorTest).Namespace}.SignalR"
              , metadataContractNamespace: "PhoneBox.Abstractions"
              , assertOutputs: true
              , expectedFiles: new[]
                {
                    "OpenApiGenerationAttribute.g.cs"
                  , "SignalRHubGenerationOutputs.g.cs"
                  , "WebHookCallConnectedRequest.g.cs"
                  , "WebHookCallDisconnectedRequest.g.cs"
                  , "ITelephonyHook.g.cs"
                  , "TelephonyHook.g.cs"
                  , "TelephonyHub.g.cs"
                  , "HubEndpointRouteBuilderExtensions.g.cs"
                  , "EndpointExtensions.g.cs"
                }
              , compilation => compilation.AddReference<Hub>()
                                          .AddReference<HubEndpointConventionBuilder>()
                                          .AddReference<IEndpointRouteBuilder>()
                                          .AddReference<HttpContext>()
                                          .AddReference<IServiceProvider>()
                                          .AddReference<IAuthorizeData>()
                                          .AddReference(typeof(ServiceCollectionServiceExtensions))
                                          .AddReference(typeof(AuthorizationEndpointConventionBuilderExtensions))
                                          .AddReferences(MetadataReference.CreateFromFile(contractAssemblyFilePath))
                                          .AddSyntaxTrees(GetEmbeddedImplementationSource("TelephonyHook.cs")));
        }

        private Compilation CompileContracts(bool assertOutputs) => RunGenerator
        (
            filter: "PhoneBox.Generators.SignalRHubGenerationOutputs.Model | PhoneBox.Generators.SignalRHubGenerationOutputs.Interface"
          , metadataNamespace: "PhoneBox.Abstractions"
          , metadataHubNamespace: null
          , metadataContractNamespace: null
          , assertOutputs: assertOutputs
          , expectedFiles: new[]
            {
                "OpenApiGenerationAttribute.g.cs"
              , "SignalRHubGenerationOutputs.g.cs"
              , "CallConnectedEvent.g.cs"
              , "CallDisconnectedEvent.g.cs"
            //, "CallState.g.cs"
            //, "CallHangUpReason.g.cs"
              , "ITelephonyHub.g.cs"
            }
        );

        private Compilation RunGenerator(string filter, string? metadataNamespace, string? metadataHubNamespace, string? metadataContractNamespace, bool assertOutputs, IReadOnlyList<string> expectedFiles, Func<CSharpCompilation, CSharpCompilation>? configureCompilation = null)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText($"[assembly: PhoneBox.Generators.OpenApiGenerationAttribute({filter})]");
            Assembly netStandardAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == "netstandard");
            Assembly systemRuntimeAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == "System.Runtime");
            CSharpCompilation inputCompilation = CSharpCompilation.Create(null)
                                                                  .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                                                                  .AddReference<object>()
                                                                  .AddReferences(MetadataReference.CreateFromFile(netStandardAssembly.Location))
                                                                  .AddReferences(MetadataReference.CreateFromFile(systemRuntimeAssembly.Location))
                                                                  .AddSyntaxTrees(syntaxTree);

            if (configureCompilation != null) 
                inputCompilation = configureCompilation(inputCompilation);

            // At this state the compilation isn't valid because the generator will add the post initialization outputs that are used within this compilation
            //RoslynUtility.VerifyCompilation(inputCompilation);

            Mock<AdditionalText> additionalText = new Mock<AdditionalText>(MockBehavior.Strict);
            additionalText.SetupGet(x => x.Path).Returns(".yml");
            additionalText.Setup(x => x.GetText(It.IsAny<CancellationToken>())).Returns(SourceText.From(GetEmbeddedResourceContent("OpenApiSchema.yml")));

            Mock<AnalyzerConfigOptions> globalAnalyzerConfigOptions = new Mock<AnalyzerConfigOptions>(MockBehavior.Strict);
            Mock<AnalyzerConfigOptions> fileAnalyzerConfigOptions = new Mock<AnalyzerConfigOptions>(MockBehavior.Strict);
            Mock<AnalyzerConfigOptionsProvider> analyzerConfigOptionsProvider = new Mock<AnalyzerConfigOptionsProvider>(MockBehavior.Strict);
            globalAnalyzerConfigOptions.Setup(x => x.TryGetValue("build_property.rootnamespace", out It.Ref<string?>.IsAny))
                                       .Returns((string _, out string? value) =>
                                       {
                                           value = typeof(GeneratorTest).Namespace;
                                           return true;
                                       });
            fileAnalyzerConfigOptions.Setup(x => x.TryGetValue("build_metadata.none.Namespace", out It.Ref<string?>.IsAny))
                                     .Returns((string _, out string? value) =>
                                     {
                                         value = metadataNamespace;
                                         return metadataNamespace != null;
                                     });
            fileAnalyzerConfigOptions.Setup(x => x.TryGetValue("build_metadata.none.HubNamespace", out It.Ref<string?>.IsAny))
                                     .Returns((string _, out string? value) =>
                                     {
                                         value = metadataHubNamespace;
                                         return metadataHubNamespace != null;
                                     });
            fileAnalyzerConfigOptions.Setup(x => x.TryGetValue("build_metadata.none.ContractNamespace", out It.Ref<string?>.IsAny))
                                     .Returns((string _, out string? value) =>
                                     {
                                         value = metadataContractNamespace;
                                         return metadataContractNamespace != null;
                                     });
            analyzerConfigOptionsProvider.SetupGet(x => x.GlobalOptions).Returns(globalAnalyzerConfigOptions.Object);
            analyzerConfigOptionsProvider.Setup(x => x.GetOptions(additionalText.Object)).Returns(fileAnalyzerConfigOptions.Object);

            IIncrementalGenerator generator = new OpenApiGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create
            (
                generators: EnumerableExtensions.Create(generator.AsSourceGenerator())
              , additionalTexts: EnumerableExtensions.Create(additionalText.Object)
              , optionsProvider: analyzerConfigOptionsProvider.Object
            );

            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);
            
            GeneratorDriverRunResult runResult = driver.GetRunResult();
            RoslynUtility.VerifyCompilation(runResult.Diagnostics);
            Assert.AreEqual(1, runResult.Results.Length);
            RoslynUtility.VerifyCompilation(runResult.Results[0]);

            if (assertOutputs)
            {
                for (int i = 0; i < runResult.GeneratedTrees.Length; i++)
                {
                    SyntaxTree outputSyntaxTree = runResult.GeneratedTrees[i];
                    FileInfo outputFile = new FileInfo(outputSyntaxTree.FilePath);
                    string actualCode = outputSyntaxTree.ToString();
                    AddResultFile(outputFile.Name, actualCode);
                    Assert.AreEqual(expectedFiles[i], outputFile.Name);
                    string expectedCode = GetExpectedSource(outputFile.Name);
                    AssertEqual(expectedCode, actualCode, outputName: Path.GetFileNameWithoutExtension(outputFile.Name), extension: outputFile.Extension.TrimStart('.'));
                }
            }

            RoslynUtility.VerifyCompilation(outputCompilation);
            RoslynUtility.VerifyCompilation(diagnostics);

            Assert.AreEqual(expectedFiles.Count, runResult.GeneratedTrees.Length);
            Assert.AreEqual(expectedFiles.Count, runResult.Results[0].GeneratedSources.Length);

            return outputCompilation;
        }

        private SyntaxTree GetEmbeddedImplementationSource(string fileName) => CSharpSyntaxTree.ParseText(GetEmbeddedResourceContent(fileName), path: fileName);

        private string GetExpectedSource(string fileName)
        {
            const string generatorVersionPlaceholder = "%GENERATORVERSION%";
            string NormalizeContent(System.Text.RegularExpressions.Match match)
            {
                if (match.Groups["GeneratorVersion"].Value != generatorVersionPlaceholder)
                    throw new InvalidOperationException($"Expected resource content contains hardcoded version: {fileName}");

                string result = $"{match.Groups["Begin"].Value}{ThisAssembly.AssemblyFileVersion}{match.Groups["End"].Value}";
                return result;
            }

            string content = GetEmbeddedResourceContent(fileName);
            string normalizedContent = Regex.Replace(content, @"(?<Begin>.*GeneratedCode\(""[^""]+"", "")(?<GeneratorVersion>[^""]+)(?<End>""\).*)", NormalizeContent);

            return normalizedContent;
        }
    }
}