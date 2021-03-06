﻿namespace TestHelper
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeActions;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Formatting;
    using StyleCop.Analyzers.Test.Helpers;
    using Xunit;

    /// <summary>
    /// Superclass of all unit tests made for diagnostics with code fixes.
    /// Contains methods used to verify correctness of code fixes.
    /// </summary>
    public abstract partial class CodeFixVerifier : DiagnosticVerifier
    {
        /// <summary>
        /// Returns the code fix being tested (C#) - to be implemented in non-abstract class.
        /// </summary>
        /// <returns>The <see cref="CodeFixProvider"/> to be used for C# code.</returns>
        protected virtual CodeFixProvider GetCSharpCodeFixProvider()
        {
            return null;
        }

        /// <summary>
        /// Called to test a C# code fix when applied on the input source as a string.
        /// </summary>
        /// <param name="oldSource">A class in the form of a string before the code fix was applied to it.</param>
        /// <param name="newSource">A class in the form of a string after the code fix was applied to it.</param>
        /// <param name="codeFixIndex">Index determining which code fix to apply if there are multiple.</param>
        /// <param name="allowNewCompilerDiagnostics">A value indicating whether or not the test will fail if the code fix introduces other warnings after being applied.</param>
        /// <param name="maxNumberOfIterations">Defines an upper limit for the number of iterations the fixer will be called.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task VerifyCSharpFixAsync(string oldSource, string newSource, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, int maxNumberOfIterations = int.MaxValue, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSource, newSource, codeFixIndex, allowNewCompilerDiagnostics, maxNumberOfIterations, GetSingleAnalyzerDocumentAsync, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Called to test a C# fix all provider when applied on the input source as a string.
        /// </summary>
        /// <param name="oldSource">A class in the form of a string before the code fix was applied to it.</param>
        /// <param name="newSource">A class in the form of a string after the code fix was applied to it.</param>
        /// <param name="codeFixIndex">Index determining which code fix to apply if there are multiple.</param>
        /// <param name="allowNewCompilerDiagnostics">A value indicating whether or not the test will fail if the code fix introduces other warnings after being applied.</param>
        /// <param name="maxNumberOfIterations">Defines an upper limit for the number of iterations the fixer will be called.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task VerifyCSharpFixAllFixAsync(string oldSource, string newSource, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, int maxNumberOfIterations = int.MaxValue, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.VerifyFixInternalAsync(LanguageNames.CSharp, this.GetCSharpDiagnosticAnalyzers().ToImmutableArray(), this.GetCSharpCodeFixProvider(), oldSource, newSource, codeFixIndex, allowNewCompilerDiagnostics, maxNumberOfIterations, GetFixAllAnalyzerDocumentAsync, cancellationToken).ConfigureAwait(false);
        }

        private async Task VerifyFixInternalAsync(string language, ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, string oldSource, string newSource, int? codeFixIndex,
            bool allowNewCompilerDiagnostics, int maxNumberOfIterations, Func<ImmutableArray<DiagnosticAnalyzer>, CodeFixProvider, int?, CancellationToken, Document, int, Task<Document>> getFixedDocument, CancellationToken cancellationToken)
        {
            var document = this.CreateDocument(oldSource, language);
            var compilerDiagnostics = await GetCompilerDiagnosticsAsync(document, cancellationToken).ConfigureAwait(false);

            document = await getFixedDocument(analyzers, codeFixProvider, codeFixIndex, cancellationToken, document, maxNumberOfIterations).ConfigureAwait(false);

            var newCompilerDiagnostics = GetNewDiagnostics(compilerDiagnostics, await GetCompilerDiagnosticsAsync(document, cancellationToken).ConfigureAwait(false));

            // check if applying the code fix introduced any new compiler diagnostics
            if (!allowNewCompilerDiagnostics && newCompilerDiagnostics.Any())
            {
                // Format and get the compiler diagnostics again so that the locations make sense in the output
                document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);
                newCompilerDiagnostics = GetNewDiagnostics(compilerDiagnostics, await GetCompilerDiagnosticsAsync(document, cancellationToken).ConfigureAwait(false));

                string message =
                    string.Format("Fix introduced new compiler diagnostics:\r\n{0}\r\n\r\nNew document:\r\n{1}\r\n",
                        string.Join("\r\n", newCompilerDiagnostics.Select(d => d.ToString())),
                        (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)).ToFullString());
                Assert.True(false, message);
            }

            // after applying all of the code fixes, compare the resulting string to the inputted one
            var actual = await GetStringFromDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            Assert.Equal(newSource, actual);
        }

        private static async Task<Document> GetSingleAnalyzerDocumentAsync(ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, int? codeFixIndex, CancellationToken cancellationToken, Document document, int maxNumberOfIterations)
        {
            var previousDiagnostics = ImmutableArray.Create<Diagnostic>();

            bool done;
            do
            {
                var analyzerDiagnostics = await GetSortedDiagnosticsFromDocumentsAsync(analyzers, new[] { document }, cancellationToken).ConfigureAwait(false);
                if (analyzerDiagnostics.Length == 0)
                {
                    break;
                }

                if (!AreDiagnosticsDifferent(analyzerDiagnostics, previousDiagnostics))
                {
                    break;
                }

                if (--maxNumberOfIterations < 0)
                {
                    Assert.True(false, "The upper limit for the number of code fix iterations was exceeded");
                }

                previousDiagnostics = analyzerDiagnostics;

                done = true;
                for (var i = 0; i < analyzerDiagnostics.Length; i++)
                {
                    var actions = new List<CodeAction>();
                    var context = new CodeFixContext(document, analyzerDiagnostics[i], (a, d) => actions.Add(a), cancellationToken);
                    await codeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

                    if (actions.Count > 0)
                    {
                        var fixedDocument = await ApplyFixAsync(document, actions.ElementAt(codeFixIndex.GetValueOrDefault(0)), cancellationToken).ConfigureAwait(false);
                        if (fixedDocument != document)
                        {
                            done = false;
                            var newText = await fixedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);

                            // workaround for issue #936 - force re-parsing to get the same sort of syntax tree as the original document.
                            document = document.WithText(newText);
                            break;
                        }
                    }
                }
            }
            while (!done);

            return document;
        }

        private static async Task<Document> GetFixAllAnalyzerDocumentAsync(ImmutableArray<DiagnosticAnalyzer> analyzers, CodeFixProvider codeFixProvider, int? codeFixIndex, CancellationToken cancellationToken, Document document, int maxNumberOfIterations)
        {
            var previousDiagnostics = ImmutableArray.Create<Diagnostic>();

            var fixAllProvider = codeFixProvider.GetFixAllProvider();

            if (fixAllProvider == null)
            {
                return null;
            }

            bool done;
            do
            {
                var analyzerDiagnostics = await GetSortedDiagnosticsFromDocumentsAsync(analyzers, new[] { document }, cancellationToken).ConfigureAwait(false);
                if (analyzerDiagnostics.Length == 0)
                {
                    break;
                }

                if (!AreDiagnosticsDifferent(analyzerDiagnostics, previousDiagnostics))
                {
                    break;
                }

                if (--maxNumberOfIterations < 0)
                {
                    Assert.True(false, "The upper limit for the number of fix all iterations was exceeded");
                }

                previousDiagnostics = analyzerDiagnostics;

                done = true;

                FixAllContext.DiagnosticProvider fixAllDiagnosticProvider = TestDiagnosticProvider.Create(analyzerDiagnostics);

                FixAllContext fixAllContext = new FixAllContext(document, codeFixProvider, FixAllScope.Document, null, codeFixProvider.FixableDiagnosticIds, fixAllDiagnosticProvider, cancellationToken);

                CodeAction action = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);

                if (action == null)
                {
                    return document;
                }

                var fixedDocument = await ApplyFixAsync(document, action, cancellationToken).ConfigureAwait(false);

                if (fixedDocument != document)
                {
                    done = false;
                    var newText = await fixedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);

                    // workaround for issue #936 - force re-parsing to get the same sort of syntax tree as the original document.
                    document = document.WithText(newText);
                }
            }
            while (!done);

            return document;
        }

        private static bool AreDiagnosticsDifferent(ImmutableArray<Diagnostic> analyzerDiagnostics, ImmutableArray<Diagnostic> previousDiagnostics)
        {
            if (analyzerDiagnostics.Length != previousDiagnostics.Length)
            {
                return true;
            }

            for (var i = 0; i < analyzerDiagnostics.Length; i++)
            {
                if (analyzerDiagnostics[i].Id != previousDiagnostics[i].Id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
