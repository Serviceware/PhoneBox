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
        public void SignalRHubGenerator_Contracts() => this.CompileContracts(assertOutputs: true);

        [TestMethod]
        public void SignalRHubGenerator_Implementation()
        {
            Compilation contractCompilation = this.CompileContracts(assertOutputs: false);

            string contractAssemblyFilePath = Path.Combine(this.TestDirectory, $"{typeof(GeneratorTest).Namespace}.Contracts.generated.dll");
            EmitResult emitResult = contractCompilation.Emit(contractAssemblyFilePath);
            RoslynUtility.VerifyCompilation(emitResult.Diagnostics);
            Assert.IsTrue(emitResult.Success, "emitResult.Success");

            _ = this.RunGenerator
            (
                filter: "PhoneBox.Generators.SignalRHubGenerationOutputs.Implementation"
              , metadataNamespace: null
              , metadataContractNamespace: "PhoneBox.Abstractions"
              , assertOutputs: true
              , expectedFiles: new[]
                {
                    "SignalRHubGenerationAttribute.generated.cs"
                  , "SignalRHubGenerationOutputs.generated.cs"
                  , "TelephonyHub.generated.cs"
                  , "HubEndpointRouteBuilderExtensions.generated.cs"
                }
              , MetadataReference.CreateFromFile(typeof(Hub).Assembly.Location)
              , MetadataReference.CreateFromFile(typeof(HubEndpointConventionBuilder).Assembly.Location)
              , MetadataReference.CreateFromFile(typeof(IEndpointRouteBuilder).Assembly.Location)
              , MetadataReference.CreateFromFile(contractAssemblyFilePath)
            );
        }

        private Compilation CompileContracts(bool assertOutputs) => this.RunGenerator
        (
            filter: "PhoneBox.Generators.SignalRHubGenerationOutputs.Model | PhoneBox.Generators.SignalRHubGenerationOutputs.Interface"
          , metadataNamespace: "PhoneBox.Abstractions"
          , metadataContractNamespace: null
          , assertOutputs: assertOutputs
          , expectedFiles: new[]
            {
                "SignalRHubGenerationAttribute.generated.cs"
              , "SignalRHubGenerationOutputs.generated.cs"
              , "CallConnectedEvent.generated.cs"
              , "CallDisconnectedEvent.generated.cs"
              , "CallState.generated.cs"
              , "CallHangUpReason.generated.cs"
              , "ITelephonyHub.generated.cs"
            }
        );

        private Compilation RunGenerator(string filter, string? metadataNamespace, string? metadataContractNamespace, bool assertOutputs, IReadOnlyList<string> expectedFiles, params MetadataReference[] additionalReferences)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText($"[assembly: PhoneBox.Generators.SignalRHubGenerationAttribute({filter})]");
            Assembly netStandardAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == "netstandard");
            Assembly systemRuntimeAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == "System.Runtime");
            CSharpCompilation inputCompilation = CSharpCompilation.Create(null)
                                                                  .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                                                                  .AddReference<object>()
                                                                  .AddReferences(MetadataReference.CreateFromFile(netStandardAssembly.Location))
                                                                  .AddReferences(MetadataReference.CreateFromFile(systemRuntimeAssembly.Location))
                                                                  .AddReferences(additionalReferences)
                                                                  .AddSyntaxTrees(syntaxTree);
            
            // At this state the compilation isn't valid because the generator will add the post initialization outputs that are used within this compilation
            //RoslynUtility.VerifyCompilation(inputCompilation);

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

            if (assertOutputs)
            {
                for (int i = 1; i < syntaxTrees.Count; i++)
                {
                    SyntaxTree outputSyntaxTree = syntaxTrees[i];
                    FileInfo outputFile = new FileInfo(outputSyntaxTree.FilePath);
                    string actualCode = outputSyntaxTree.ToString();
                    this.AddResultFile(outputFile.Name, actualCode);
                    Assert.AreEqual(expectedFiles[i - 1], outputFile.Name);
                    string expectedCode = this.GetEmbeddedResourceContent(outputFile.Name).Replace("%GENERATORVERSION%", ThisAssembly.AssemblyFileVersion);
                    this.AssertEqual(expectedCode, actualCode, outputName: Path.GetFileNameWithoutExtension(outputFile.Name), extension: outputFile.Extension.TrimStart('.'));
                }
            }

            RoslynUtility.VerifyCompilation(outputCompilation);
            RoslynUtility.VerifyCompilation(diagnostics);

            Assert.AreEqual(expectedFiles.Count, runResult.GeneratedTrees.Length);
            Assert.AreEqual(expectedFiles.Count, runResult.Results[0].GeneratedSources.Length);
            Assert.AreEqual(expectedFiles.Count + 1, syntaxTrees.Count);

            return outputCompilation;
        }
    }
}