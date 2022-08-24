﻿using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dibix.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PhoneBox.Generators.Tests
{
    [TestClass]
    public sealed class GeneratorTest : TestBase
    {
        [TestMethod]
        public void SignalRHubGenerator()
        {
            CSharpCompilation inputCompilation = CSharpCompilation.Create(null)
                                                                  .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                                                                  .AddReference<object>();
            RoslynUtility.VerifyCompilation(inputCompilation);

            Mock<AdditionalText> additionalText = new Mock<AdditionalText>(MockBehavior.Strict);
            additionalText.SetupGet(x => x.Path).Returns(".yml");
            additionalText.Setup(x => x.GetText(It.IsAny<CancellationToken>())).Returns(SourceText.From(Resource.OpenApiSchema));

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
                                         value = "PhoneBox.Generators.Tests";
                                         return true;
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
            Assert.AreEqual(2, runResult.GeneratedTrees.Length);
            Assert.AreEqual(1, runResult.Results.Length);
            Assert.AreEqual(2, runResult.Results[0].GeneratedSources.Length);
            RoslynUtility.VerifyCompilation(runResult.Results[0]);
            Assert.AreEqual(2, outputCompilation.SyntaxTrees.Count());

            foreach (SyntaxTree syntaxTree in outputCompilation.SyntaxTrees)
            {
                string expectedCode = base.GetEmbeddedResourceContent(System.IO.Path.GetFileName(syntaxTree.FilePath));
                string actualCode = syntaxTree.ToString();
                base.AssertEqual(expectedCode, actualCode, "cs");
            }

            RoslynUtility.VerifyCompilation(outputCompilation);
            RoslynUtility.VerifyCompilation(diagnostics);
        }
    }
}