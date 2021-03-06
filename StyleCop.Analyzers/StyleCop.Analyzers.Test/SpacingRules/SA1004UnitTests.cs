﻿namespace StyleCop.Analyzers.Test.SpacingRules
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Diagnostics;
    using StyleCop.Analyzers.SpacingRules;
    using TestHelper;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="SA1004DocumentationLinesMustBeginWithSingleSpace"/>.
    /// </summary>
    public class SA1004UnitTests : CodeFixVerifier
    {
        [Fact]
        public async Task TestFixedExampleAsync()
        {
            string testCode = @"
public class TypeName
{
    /// <summary>
    /// The summary text.
    /// </summary>
    /// <param name=""x"">The document root.</param>
    /// <param name=""y"">The XML header token.</param>
    private void Method1(int x, int y)
    {
    }
}
";

            await this.VerifyCSharpDiagnosticAsync(testCode, EmptyDiagnosticResults, CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestRuleExampleAsync()
        {
            string testCode = @"
public class TypeName
{
    ///<summary>
    ///The summary text.
    ///</summary>
    ///   <param name=""x"">The document root.</param>
    ///    <param name=""y"">The XML header token.</param>
    private void Method1(int x, int y)
    {
    }
}
";

            string fixedCode = @"
public class TypeName
{
    /// <summary>
    /// The summary text.
    /// </summary>
    ///   <param name=""x"">The document root.</param>
    ///    <param name=""y"">The XML header token.</param>
    private void Method1(int x, int y)
    {
    }
}
";

            // Currently the extra indentation for <param> elements is not checked.
            DiagnosticResult[] expected =
            {
                this.CSharpDiagnostic().WithLocation(4, 8),
                this.CSharpDiagnostic().WithLocation(5, 8),
                this.CSharpDiagnostic().WithLocation(6, 8),
            };

            await this.VerifyCSharpDiagnosticAsync(testCode, expected, CancellationToken.None).ConfigureAwait(false);
            await this.VerifyCSharpDiagnosticAsync(fixedCode, EmptyDiagnosticResults, CancellationToken.None).ConfigureAwait(false);
            await this.VerifyCSharpFixAsync(testCode, fixedCode).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestEmptyCommentLinesAsync()
        {
            string testCode = @"
public class TypeName
{
    /// <summary>
    ///
    /// </summary>
    private void Method1()
    {
    }

    /// <summary>
    /// </summary>
    ///
    private void Method2()
    {
    }

    ///
    /// <summary>
    /// </summary>
    private void Method3()
    {
    }
}
";

            await this.VerifyCSharpDiagnosticAsync(testCode, EmptyDiagnosticResults, CancellationToken.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override IEnumerable<DiagnosticAnalyzer> GetCSharpDiagnosticAnalyzers()
        {
            yield return new SA1004DocumentationLinesMustBeginWithSingleSpace();
        }

        /// <inheritdoc/>
        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new SA1004CodeFixProvider();
        }
    }
}
