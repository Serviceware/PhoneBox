using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Dibix.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PhoneBox.Generators.Tests
{
    [TestClass]
    public sealed class GeneratorTest : TestBase
    {
        [TestMethod]
        public void SignalRHubGenerator_Contracts()
        {
            (GeneratorDriverRunResult? runResult, IList<SyntaxTree>? syntaxTrees, Compilation? _) = this.CompileContracts();

            Assert.AreEqual(3, runResult.GeneratedTrees.Length);
            Assert.AreEqual(3, runResult.Results[0].GeneratedSources.Length);
            Assert.AreEqual(4, syntaxTrees.Count);
        }

        [TestMethod]
        public void SignalRHubGenerator_Implementation()
        {
            (GeneratorDriverRunResult? _, IList<SyntaxTree>? _, Compilation? contractCompilation) = this.CompileContracts();

            string contractAssemblyFilePath = Path.Combine(this.TestDirectory, $"{typeof(GeneratorTest).Namespace}.Contracts.generated.dll");
            EmitResult emitResult = contractCompilation.Emit(contractAssemblyFilePath);
            RoslynUtility.VerifyCompilation(emitResult.Diagnostics);
            Assert.IsTrue(emitResult.Success, "emitResult.Success");

            (GeneratorDriverRunResult? runResult, IList<SyntaxTree>? syntaxTrees, Compilation? _) = this.RunGenerator
            (
                filter: SignalRHubGenerationOutputs.Implementation
              , metadataNamespace: null
              , metadataContractNamespace: "PhoneBox.Abstractions"
              , expectedFiles: new[]
                {
                    "TelephonyHub.generated.cs"
                  , "HubEndpointRouteBuilderExtensions.generated.cs"
                }
              , MetadataReference.CreateFromFile(typeof(Hub).Assembly.Location)
              , MetadataReference.CreateFromFile(typeof(HubEndpointConventionBuilder).Assembly.Location)
              , MetadataReference.CreateFromFile(typeof(IEndpointRouteBuilder).Assembly.Location)
              , MetadataReference.CreateFromFile(contractAssemblyFilePath)
            );

            Assert.AreEqual(2, runResult.GeneratedTrees.Length);
            Assert.AreEqual(2, runResult.Results[0].GeneratedSources.Length);
            Assert.AreEqual(3, syntaxTrees.Count);
        }

        private (GeneratorDriverRunResult runResult, IList<SyntaxTree> syntaxTrees, Compilation outputCompilation) CompileContracts() => this.RunGenerator
        (
            filter: SignalRHubGenerationOutputs.Model | SignalRHubGenerationOutputs.Interface
          , metadataNamespace: "PhoneBox.Abstractions"
          , metadataContractNamespace: null
          , expectedFiles: new[]
            {
                "CallConnectedEvent.generated.cs"
              , "CallDisconnectedEvent.generated.cs"
              , "ITelephonyHub.generated.cs"
            }
        );

        private (GeneratorDriverRunResult runResult, IList<SyntaxTree> syntaxTrees, Compilation outputCompilation) RunGenerator(SignalRHubGenerationOutputs filter, string? metadataNamespace, string? metadataContractNamespace, string[] expectedFiles, params MetadataReference[] additionalReferences)
        {
            IEnumerable<string> filterFlags = Enum.GetValues<SignalRHubGenerationOutputs>()
                                                  .Where(x => filter.HasFlag(x))
                                                  .Select(x => $"{typeof(SignalRHubGenerationOutputs)}.{x}");
            string filterFlagsStr = String.Join(" | ", filterFlags);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText($"[assembly: {typeof(SignalRHubGenerationAttribute)}({filterFlagsStr})]");
            Assembly netStandardAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == "netstandard");
            Assembly systemRuntimeAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == "System.Runtime");
            CSharpCompilation inputCompilation = CSharpCompilation.Create(null)
                                                                  .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                                                                  .AddReference<object>()
                                                                  .AddReference<SignalRHubGenerationAttribute>()
                                                                  .AddReferences(MetadataReference.CreateFromFile(netStandardAssembly.Location))
                                                                  .AddReferences(MetadataReference.CreateFromFile(systemRuntimeAssembly.Location))
                                                                  .AddReferences(additionalReferences)
                                                                  .AddSyntaxTrees(syntaxTree);
            RoslynUtility.VerifyCompilation(inputCompilation);

            Mock<AdditionalText> additionalText = new Mock<AdditionalText>(MockBehavior.Strict);
            additionalText.SetupGet(x => x.Path).Returns(".yml");
            additionalText.Setup(x => x.GetText(It.IsAny<CancellationToken>())).Returns(SourceText.From(this.GetEmbeddedResourceContent("OpenApiSchema.yml")));

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
            fileAnalyzerConfigOptions.Setup(x => x.TryGetValue("build_metadata.none.ContractNamespace", out It.Ref<string?>.IsAny))
                                     .Returns((string _, out string? value) =>
                                     {
                                         value = metadataContractNamespace;
                                         return metadataContractNamespace != null;
                                     });
            analyzerConfigOptionsProvider.SetupGet(x => x.GlobalOptions).Returns(globalAnalyzerConfigOptions.Object);
            analyzerConfigOptionsProvider.Setup(x => x.GetOptions(additionalText.Object)).Returns(fileAnalyzerConfigOptions.Object);

            IIncrementalGenerator generator = new SignalRHubGenerator();
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
            IList<SyntaxTree> syntaxTrees = outputCompilation.SyntaxTrees.ToArray();
            Assert.AreEqual(inputCompilation.SyntaxTrees[0], syntaxTrees[0]);

            for (int i = 1; i < syntaxTrees.Count; i++)
            {
                SyntaxTree outputSyntaxTree = syntaxTrees[i];
                FileInfo outputFile = new FileInfo(outputSyntaxTree.FilePath);
                string actualCode = outputSyntaxTree.ToString();
                this.AddResultFile(outputFile.Name, actualCode);
                Assert.AreEqual(expectedFiles[i - 1], outputFile.Name);
                string expectedCode = this.GetEmbeddedResourceContent(outputFile.Name);
                this.AssertEqual(expectedCode, actualCode, outputName: Path.GetFileNameWithoutExtension(outputFile.Name), extension: outputFile.Extension.TrimStart('.'));
            }

            RoslynUtility.VerifyCompilation(outputCompilation);
            RoslynUtility.VerifyCompilation(diagnostics);

            return (runResult, syntaxTrees, outputCompilation);
        }
    }
}