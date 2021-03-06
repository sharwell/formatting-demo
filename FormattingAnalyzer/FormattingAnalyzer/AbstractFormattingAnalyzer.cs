// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using FormattingAnalyzer;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.CodeStyle
{
    internal abstract class AbstractFormattingAnalyzer
        : AbstractCodeStyleDiagnosticAnalyzer
    {
        internal const string FormattingDiagnosticId = "IDE0060";

        static AbstractFormattingAnalyzer()
        {
            AppDomain.CurrentDomain.AssemblyResolve += HandleAssemblyResolve;
        }

        protected AbstractFormattingAnalyzer()
            : base(
                FormattingDiagnosticId,
                new LocalizableResourceString(nameof(Resources.Formatting_analyzer_title), Resources.ResourceManager, typeof(Resources)),
                new LocalizableResourceString(nameof(Resources.Formatting_analyzer_message), Resources.ResourceManager, typeof(Resources)))
        {
        }

        protected abstract Type GetAnalyzerImplType();

        protected override void InitializeWorker(AnalysisContext context)
        {
            var analyzer = (AbstractFormattingAnalyzerImpl)Activator.CreateInstance(GetAnalyzerImplType(), Descriptor);
            analyzer.InitializeWorker(context);
        }

        private static Assembly HandleAssemblyResolve(object sender, ResolveEventArgs args)
        {
            switch (new AssemblyName(args.Name).Name)
            {
            case "Microsoft.CodeAnalysis.Workspaces":
            case "Microsoft.CodeAnalysis.CSharp.Workspaces":
            case "Microsoft.VisualStudio.CodingConventions":
                var result = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(typeof(AbstractFormattingAnalyzer).Assembly.Location), "..\\workspaces", new AssemblyName(args.Name).Name + ".dll"));
                return result;

            default:
                return null;
            }
        }
    }
}
