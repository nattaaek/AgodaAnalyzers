﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace Agoda.Analyzers.Test.Helpers;

/// <summary>
/// Superclass of all unit tests for <see cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer"/>s.
/// </summary>
[TestFixture]
public abstract partial class DiagnosticVerifier
{
    private const int DEFAULT_INDENTATION_SIZE = 4;
    private const int DEFAULT_TAB_SIZE = 4;
    private const bool DEFAULT_USE_TABS = false;

    public DiagnosticVerifier()
    {
        IndentationSize = DEFAULT_INDENTATION_SIZE;
        TabSize = DEFAULT_TAB_SIZE;
        UseTabs = DEFAULT_USE_TABS;
    }

    /// <summary>
    /// Gets or sets the value of the <see cref="FormattingOptions.IndentationSize"/> to apply to the test
    /// workspace.
    /// </summary>
    /// <value>
    /// The value of the <see cref="FormattingOptions.IndentationSize"/> to apply to the test workspace.
    /// </value>
    public int IndentationSize { get; protected set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="FormattingOptions.UseTabs"/> option is applied to the
    /// test workspace.
    /// </summary>
    /// <value>
    /// The value of the <see cref="FormattingOptions.UseTabs"/> to apply to the test workspace.
    /// </value>
    public bool UseTabs { get; protected set; }

    /// <summary>
    /// Gets or sets the value of the <see cref="FormattingOptions.TabSize"/> to apply to the test workspace.
    /// </summary>
    /// <value>
    /// The value of the <see cref="FormattingOptions.TabSize"/> to apply to the test workspace.
    /// </value>
    public int TabSize { get; protected set; }

    protected static DiagnosticLocation[] EmptyDiagnosticResults { get; } = { };

    /// <summary>
    /// Verifies that the analyzer will properly handle an empty source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TestEmptySourceAsync()
    {
        await VerifyDiagnosticsAsync(new CodeDescriptor(), EmptyDiagnosticResults).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the C# analyzer being tested
    /// </summary>
    /// <returns>
    /// New instances of all the C# analyzers being tested.
    /// </returns>
    protected abstract DiagnosticAnalyzer DiagnosticAnalyzer { get; }

    /// <summary>
    /// Gets the C# analyzer being tested
    /// </summary>
    /// <returns>
    /// New instances of all the C# analyzers being tested.
    /// </returns>
    protected abstract string DiagnosticId { get; }
        
    protected async Task VerifyDiagnosticsAsync(string code, DiagnosticLocation expectedLocations)
    {
        await VerifyDiagnosticsAsync(new CodeDescriptor(code), new [] { expectedLocations});
    }

    protected async Task VerifyDiagnosticsAsync(string code, DiagnosticLocation[] expectedLocations)
    {
        await VerifyDiagnosticsAsync(new CodeDescriptor(code), expectedLocations);
    }

    protected async Task VerifyDiagnosticsAsync(CodeDescriptor descriptor, DiagnosticLocation expectedLocations)
    {
        await VerifyDiagnosticsAsync(descriptor, new [] { expectedLocations});
    }
        
    protected async Task VerifyDiagnosticsAsync(CodeDescriptor descriptor, DiagnosticLocation[] expectedLocations)
    {
        var baseResult = CSharpDiagnostic(DiagnosticId);
        var expected = expectedLocations.Select(l => baseResult.WithLocation(l.Line, l.Col)).ToArray();


    var doc = CreateProject(new[] {descriptor.Code})
            .AddMetadataReferences(descriptor.References.Select(assembly => MetadataReference.CreateFromFile(assembly.Location)))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Type).GetTypeInfo().Assembly.Location.Replace("System.Private.CoreLib", "mscorlib")))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Type).GetTypeInfo().Assembly.Location.Replace("System.Private.CoreLib", "netstandard")))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Type).GetTypeInfo().Assembly.Location.Replace("System.Private.CoreLib", "System.Runtime")))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Type).GetTypeInfo().Assembly.Location.Replace("System.Private.CoreLib", "System.Threading.Tasks")))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Type).GetTypeInfo().Assembly.Location.Replace("System.Private.CoreLib", "System.ObjectModel")))
            .Documents
            .First();

        var analyzersArray = ImmutableArray.Create(DiagnosticAnalyzer);

        var diag = await GetSortedDiagnosticsFromDocumentsAsync(analyzersArray, new[] {doc}, CancellationToken.None)
            .ConfigureAwait(false);

        VerifyDiagnosticsAsync(diag, analyzersArray, expected);
    }

    /// <summary>
    /// Called to test a C# <see cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer"/> when applied on the single input source as a string.
    /// <note type="note">
    /// <para>Input a <see cref="DiagnosticResult"/> for each <see cref="Diagnostic"/> expected.</para>
    /// </note>
    /// </summary>
    /// <param name="source">A class in the form of a string to run the analyzer on.</param>
    /// <param name="expected">A collection of <see cref="DiagnosticResult"/>s describing the
    /// <see cref="Diagnostic"/>s that should be reported by the analyzer for the specified source.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
    /// <param name="filename">The filename or null if the default filename should be used</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private Task VerifyDiagnosticsAsync(string source, DiagnosticResult[] expected, CancellationToken cancellationToken, string filename = null)
    {
        return VerifyDiagnosticsAsync(new[] {source}, LanguageNames.CSharp, GetCSharpDiagnosticAnalyzers(), expected, cancellationToken, filename != null ? new[] {filename} : null);
    }

    /// <summary>
    /// Called to test a C# <see cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer"/> when applied on the input strings as sources.
    /// <note type="note">
    /// <para>Input a <see cref="DiagnosticResult"/> for each <see cref="Diagnostic"/> expected.</para>
    /// </note>
    /// </summary>
    /// <param name="sources">A collection of strings to create source documents from to run the analyzers
    /// on.</param>
    /// <param name="expected">A collection of <see cref="DiagnosticResult"/>s describing the
    /// <see cref="Diagnostic"/>s that should be reported by the analyzer for the specified sources.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
    /// <param name="filenames">The filenames or null if the default filename should be used</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private Task VerifyDiagnosticsAsync(string[] sources, DiagnosticResult[] expected, CancellationToken cancellationToken, string[] filenames = null)
    {
        return VerifyDiagnosticsAsync(sources, LanguageNames.CSharp, GetCSharpDiagnosticAnalyzers().ToImmutableArray(), expected, cancellationToken, filenames);
    }

    /// <summary>
    /// Checks each of the actual <see cref="Diagnostic"/>s found and compares them with the corresponding
    /// <see cref="DiagnosticResult"/> in the array of expected results. <see cref="Diagnostic"/>s are considered
    /// equal only if the <see cref="DiagnosticResult.Spans"/>, <see cref="DiagnosticResult.Id"/>,
    /// <see cref="DiagnosticResult.Severity"/>, and <see cref="DiagnosticResult.Message"/> of the
    /// <see cref="DiagnosticResult"/> match the actual <see cref="Diagnostic"/>.
    /// </summary>
    /// <param name="actualResults">The <see cref="Diagnostic"/>s found by the compiler after running the analyzer
    /// on the source code.</param>
    /// <param name="analyzers">The analyzers that have been run on the sources.</param>
    /// <param name="expectedResults">A collection of <see cref="DiagnosticResult"/>s describing the expected
    /// diagnostics for the sources.</param>
    private static void VerifyDiagnosticsAsync(IEnumerable<Diagnostic> actualResults, ImmutableArray<DiagnosticAnalyzer> analyzers, DiagnosticResult[] expectedResults)
    {
        var expectedCount = expectedResults.Length;
        var actualCount = actualResults.Count();

        if (expectedCount != actualCount)
        {
            var diagnosticsOutput = actualResults.Any() ? FormatDiagnostics(analyzers, actualResults.ToArray()) : "    NONE.";

            Assert.True(
                false,
                string.Format("Mismatch between number of diagnostics returned, expected \"{0}\" actual \"{1}\"\r\n\r\nDiagnostics:\r\n{2}\r\n", expectedCount, actualCount, diagnosticsOutput));
        }

        for (var i = 0; i < expectedResults.Length; i++)
        {
            var actual = actualResults.ElementAt(i);
            var expected = expectedResults[i];

            if (!expected.HasLocation)
            {
                if (actual.Location != Location.None)
                {
                    var message =
                        string.Format(
                            "Expected:\r\nA project diagnostic with No location\r\nActual:\r\n{0}",
                            FormatDiagnostics(analyzers, actual));
                    Assert.True(false, message);
                }
            }
            else
            {
                VerifyDiagnosticLocation(analyzers, actual, actual.Location, expected.Spans.First());
                var additionalLocations = actual.AdditionalLocations.ToArray();

                if (additionalLocations.Length != expected.Spans.Length - 1)
                {
                    Assert.True(
                        false,
                        string.Format(
                            "Expected {0} additional locations but got {1} for Diagnostic:\r\n    {2}\r\n",
                            expected.Spans.Length - 1,
                            additionalLocations.Length,
                            FormatDiagnostics(analyzers, actual)));
                }

                for (var j = 0; j < additionalLocations.Length; ++j)
                {
                    VerifyDiagnosticLocation(analyzers, actual, additionalLocations[j], expected.Spans[j + 1]);
                }
            }

            if (actual.Id != expected.Id)
            {
                var message =
                    string.Format(
                        "Expected diagnostic id to be \"{0}\" was \"{1}\"\r\n\r\nDiagnostic:\r\n    {2}\r\n",
                        expected.Id,
                        actual.Id,
                        FormatDiagnostics(analyzers, actual));
                Assert.True(false, message);
            }

            if (actual.Severity != expected.Severity)
            {
                var message =
                    string.Format(
                        "Expected diagnostic severity to be \"{0}\" was \"{1}\"\r\n\r\nDiagnostic:\r\n    {2}\r\n",
                        expected.Severity,
                        actual.Severity,
                        FormatDiagnostics(analyzers, actual));
                Assert.True(false, message);
            }

            if (actual.GetMessage() != expected.Message)
            {
                var message =
                    string.Format(
                        "Expected diagnostic message to be \"{0}\" was \"{1}\"\r\n\r\nDiagnostic:\r\n    {2}\r\n",
                        expected.Message,
                        actual.GetMessage(),
                        FormatDiagnostics(analyzers, actual));
                Assert.True(false, message);
            }
        }
    }

    /// <summary>
    /// Helper method to <see cref="VerifyDiagnosticResults"/> that checks the location of a
    /// <see cref="Diagnostic"/> and compares it with the location described by a
    /// <see cref="FileLinePositionSpan"/>.
    /// </summary>
    /// <param name="analyzers">The analyzer that have been run on the sources.</param>
    /// <param name="diagnostic">The diagnostic that was found in the code.</param>
    /// <param name="actual">The location of the diagnostic found in the code.</param>
    /// <param name="expected">The <see cref="FileLinePositionSpan"/> describing the expected location of the
    /// diagnostic.</param>
    private static void VerifyDiagnosticLocation(ImmutableArray<DiagnosticAnalyzer> analyzers, Diagnostic diagnostic, Location actual, FileLinePositionSpan expected)
    {
        var actualSpan = actual.GetLineSpan();

        var message =
            string.Format(
                "Expected diagnostic to be in file \"{0}\" was actually in file \"{1}\"\r\n\r\nDiagnostic:\r\n    {2}\r\n",
                expected.Path,
                actualSpan.Path,
                FormatDiagnostics(analyzers, diagnostic));
        Assert.True(
            actualSpan.Path == expected.Path || actualSpan.Path != null && actualSpan.Path.Contains("Test0.") && expected.Path.Contains("Test."),
            message);

        var actualStartLinePosition = actualSpan.StartLinePosition;
        var actualEndLinePosition = actualSpan.EndLinePosition;

        VerifyLinePosition(analyzers, diagnostic, actualSpan.StartLinePosition, expected.StartLinePosition, "start");
        if (expected.StartLinePosition < expected.EndLinePosition)
        {
            VerifyLinePosition(analyzers, diagnostic, actualSpan.EndLinePosition, expected.EndLinePosition, "end");
        }
    }

    private static void VerifyLinePosition(ImmutableArray<DiagnosticAnalyzer> analyzers, Diagnostic diagnostic, LinePosition actualLinePosition, LinePosition expectedLinePosition, string positionText)
    {
        // Only check the line position if it matters
        if (expectedLinePosition.Line > 0)
        {
            Assert.True(
                actualLinePosition.Line + 1 == expectedLinePosition.Line,
                string.Format(
                    "Expected diagnostic to {0} on line \"{1}\" was actually on line \"{2}\"\r\n\r\nDiagnostic:\r\n    {3}\r\n",
                    positionText,
                    expectedLinePosition.Line,
                    actualLinePosition.Line + 1,
                    FormatDiagnostics(analyzers, diagnostic)));
        }

        // Only check the column position if it matters
        if (expectedLinePosition.Character > 0)
        {
            Assert.True(
                actualLinePosition.Character + 1 == expectedLinePosition.Character,
                string.Format(
                    "Expected diagnostic to {0} at column \"{1}\" was actually at column \"{2}\"\r\n\r\nDiagnostic:\r\n    {3}\r\n",
                    positionText,
                    expectedLinePosition.Character,
                    actualLinePosition.Character + 1,
                    FormatDiagnostics(analyzers, diagnostic)));
        }
    }

    /// <summary>
    /// Helper method to format a <see cref="Diagnostic"/> into an easily readable string.
    /// </summary>
    /// <param name="analyzers">The analyzers that this verifier tests.</param>
    /// <param name="diagnostics">A collection of <see cref="Diagnostic"/>s to be formatted.</param>
    /// <returns>The <paramref name="diagnostics"/> formatted as a string.</returns>
    private static string FormatDiagnostics(ImmutableArray<DiagnosticAnalyzer> analyzers, params Diagnostic[] diagnostics)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < diagnostics.Length; ++i)
        {
            var diagnosticsId = diagnostics[i].Id;

            builder.Append("// ").AppendLine(diagnostics[i].ToString());

            var applicableAnalyzer = analyzers.FirstOrDefault(a => a.SupportedDiagnostics.Any(dd => dd.Id == diagnosticsId));
            if (applicableAnalyzer != null)
            {
                var analyzerType = applicableAnalyzer.GetType();

                var location = diagnostics[i].Location;
                if (location == Location.None)
                {
                    builder.AppendFormat("GetGlobalResult({0}.{1})", analyzerType.Name, diagnosticsId);
                }
                else
                {
                    Assert.True(
                        location.IsInSource,
                        string.Format("Test base does not currently handle diagnostics in metadata locations. Diagnostic in metadata:\r\n{0}", diagnostics[i]));

                    var resultMethodName = diagnostics[i].Location.SourceTree.FilePath.EndsWith(".cs") ? "GetCSharpResultAt" : "GetBasicResultAt";
                    var linePosition = diagnostics[i].Location.GetLineSpan().StartLinePosition;

                    builder.AppendFormat(
                        "{0}({1}, {2}, {3}.{4})",
                        resultMethodName,
                        linePosition.Line + 1,
                        linePosition.Character + 1,
                        analyzerType.Name,
                        diagnosticsId);
                }

                if (i != diagnostics.Length - 1)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static bool IsSubjectToExclusion(DiagnosticResult result)
    {
        if (result.Id.StartsWith("CS", StringComparison.Ordinal))
        {
            return false;
        }

        if (result.Id.StartsWith("AD", StringComparison.Ordinal))
        {
            return false;
        }

        if (result.Spans.Length == 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// General method that gets a collection of actual <see cref="Diagnostic"/>s found in the source after the
    /// analyzer is run, then verifies each of them.
    /// </summary>
    /// <param name="sources">An array of strings to create source documents from to run the analyzers on.</param>
    /// <param name="language">The language of the classes represented by the source strings.</param>
    /// <param name="analyzers">The analyzers to be run on the source code.</param>
    /// <param name="expected">A collection of <see cref="DiagnosticResult"/>s that should appear after the analyzer
    /// is run on the sources.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
    /// <param name="filenames">The filenames or null if the default filename should be used</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task VerifyDiagnosticsAsync(string[] sources, string language, ImmutableArray<DiagnosticAnalyzer> analyzers, DiagnosticResult[] expected, CancellationToken cancellationToken, string[] filenames)
    {
        VerifyDiagnosticsAsync(await GetSortedDiagnosticsAsync(sources, language, analyzers, cancellationToken, filenames).ConfigureAwait(false), analyzers, expected);

        // If filenames is null we want to test for exclusions too
        if (filenames == null)
        {
            // Also check if the analyzer honors exclusions
            if (expected.Any(IsSubjectToExclusion))
            {
                // Diagnostics reported by the compiler and analyzer diagnostics which don't have a location will
                // still be reported. We also insert a new line at the beginning so we have to move all diagnostic
                // locations which have a specific position down by one line.
                var expectedResults = expected
                    .Where(x => !IsSubjectToExclusion(x))
                    .Select(x => x.WithLineOffset(1))
                    .ToArray();

                VerifyDiagnosticsAsync(await GetSortedDiagnosticsAsync(sources.Select(x => " // <auto-generated>\r\n" + x).ToArray(), language, analyzers, cancellationToken, null).ConfigureAwait(false), analyzers, expectedResults);
            }
        }
    }
}