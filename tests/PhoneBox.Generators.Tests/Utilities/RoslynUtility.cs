﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PhoneBox.Generators.Tests
{
    internal static class RoslynUtility
    {
        public static CSharpCompilation AddReference<T>(this CSharpCompilation compilation) => AddReference(compilation, typeof(T));
        public static CSharpCompilation AddReference(this CSharpCompilation compilation, Type type) => AddReference(compilation, type.Assembly);
        public static CSharpCompilation AddReference(this CSharpCompilation compilation, Assembly assembly) => compilation.AddReferences(MetadataReference.CreateFromFile(assembly.Location));

        public static void VerifyCompilation(Compilation compilation) => VerifyCompilation(compilation.GetDiagnostics());
        public static void VerifyCompilation(GeneratorRunResult result)
        {
            if (result.Exception != null)
                throw result.Exception;

            VerifyCompilation(result.Diagnostics);
        }
        public static void VerifyCompilation(ImmutableArray<Diagnostic> diagnostics)
        {
            ICollection<Diagnostic> errors = diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
            if (!errors.Any())
                return;

            string errorsText = String.Join(Environment.NewLine, errors.Select(x => x));
            throw new CodeCompilationException(errorsText);
        }
    }

    internal sealed class CodeCompilationException : Exception
    {
        public CodeCompilationException(string errorMessages) : base($@"One or more errors occured while validating the generated code:
{errorMessages.TrimEnd(Environment.NewLine.ToCharArray())}")
        {
        }
    }
}