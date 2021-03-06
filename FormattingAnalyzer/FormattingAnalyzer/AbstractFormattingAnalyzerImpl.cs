// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.CodeStyle
{
    internal abstract class AbstractFormattingAnalyzerImpl
    {
        public static readonly string ReplaceTextKey = nameof(ReplaceTextKey);

        public static readonly ImmutableDictionary<string, string> RemoveTextProperties =
            ImmutableDictionary.Create<string, string>().Add(ReplaceTextKey, "");

        private readonly DiagnosticDescriptor _descriptor;

        protected AbstractFormattingAnalyzerImpl(DiagnosticDescriptor descriptor)
        {
            _descriptor = descriptor;
        }

        internal void InitializeWorker(AnalysisContext context)
        {
            var workspace = new AdhocWorkspace();
            var codingConventionsManager = CodingConventionsManagerFactory.CreateCodingConventionsManager();

            context.RegisterSyntaxTreeAction(c => AnalyzeSyntaxTree(c, workspace, codingConventionsManager));
        }

        protected abstract OptionSet ApplyFormattingOptions(OptionSet optionSet, ICodingConventionContext codingConventionContext);

        private void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context, Workspace workspace, ICodingConventionsManager codingConventionsManager)
        {
            var options = workspace.Options;
            if (File.Exists(context.Tree.FilePath))
            {
                var codingConventionContext = codingConventionsManager.GetConventionContextAsync(context.Tree.FilePath, context.CancellationToken).GetAwaiter().GetResult();
                options = ApplyFormattingOptions(options, codingConventionContext);
            }

            var formattingChanges = Formatter.GetFormattedTextChanges(context.Tree.GetRoot(context.CancellationToken), workspace, options, context.CancellationToken);
            foreach (var formattingChange in formattingChanges)
            {
                var change = formattingChange;
                if (change.NewText.Length > 0 && !change.Span.IsEmpty)
                {
                    var oldText = context.Tree.GetText(context.CancellationToken);

                    // Handle cases where the change is a substring removal from the beginning
                    var offset = change.Span.Length - change.NewText.Length;
                    if (offset >= 0 && oldText.GetSubText(new TextSpan(change.Span.Start + offset, change.NewText.Length)).ContentEquals(SourceText.From(change.NewText)))
                    {
                        change = new TextChange(new TextSpan(change.Span.Start, offset), "");
                    }
                    else
                    {
                        // Handle cases where the change is a substring removal from the end
                        if (change.NewText.Length < change.Span.Length
                            && oldText.GetSubText(new TextSpan(change.Span.Start, change.NewText.Length)).ContentEquals(SourceText.From(change.NewText)))
                        {
                            change = new TextChange(new TextSpan(change.Span.Start + change.NewText.Length, change.Span.Length - change.NewText.Length), "");
                        }
                    }
                }

                if (change.NewText.Length == 0 && change.Span.IsEmpty)
                {
                    // No actual change
                    continue;
                }

                ImmutableDictionary<string, string> properties;
                if (change.NewText.Length == 0)
                {
                    properties = RemoveTextProperties;
                }
                else
                {
                    properties = ImmutableDictionary.Create<string, string>().Add(ReplaceTextKey, change.NewText);
                }

                var location = Location.Create(context.Tree, change.Span);
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    _descriptor,
                    location,
                    ReportDiagnostic.Default,
                    additionalLocations: null,
                    properties));
            }
        }
    }
}
