﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CodeStyle
{
    internal static class DiagnosticHelper
    {
        /// <summary>
        /// Creates a <see cref="Diagnostic"/> instance.
        /// </summary>
        /// <param name="descriptor">A <see cref="DiagnosticDescriptor"/> describing the diagnostic.</param>
        /// <param name="location">An optional primary location of the diagnostic. If null, <see cref="Location"/> will return <see cref="Location.None"/>.</param>
        /// <param name="effectiveSeverity">Effective severity of the diagnostic.</param>
        /// <param name="additionalLocations">
        /// An optional set of additional locations related to the diagnostic.
        /// Typically, these are locations of other items referenced in the message.
        /// If null, <see cref="Diagnostic.AdditionalLocations"/> will return an empty list.
        /// </param>
        /// <param name="properties">
        /// An optional set of name-value pairs by means of which the analyzer that creates the diagnostic
        /// can convey more detailed information to the fixer. If null, <see cref="Diagnostic.Properties"/> will return
        /// <see cref="ImmutableDictionary{TKey, TValue}.Empty"/>.
        /// </param>
        /// <param name="messageArgs">Arguments to the message of the diagnostic.</param>
        /// <returns>The <see cref="Diagnostic"/> instance.</returns>
        public static Diagnostic Create(
            DiagnosticDescriptor descriptor,
            Location location,
            ReportDiagnostic effectiveSeverity,
            IEnumerable<Location> additionalLocations,
            ImmutableDictionary<string, string> properties,
            params object[] messageArgs)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            LocalizableString message;
            if (messageArgs == null || messageArgs.Length == 0)
            {
                message = descriptor.MessageFormat;
            }
            else
            {
                message = new LocalizableStringWithArguments(descriptor.MessageFormat, messageArgs);
            }

            var warningLevel = effectiveSeverity.ToDiagnosticSeverity() ?? descriptor.DefaultSeverity;
            return Diagnostic.Create(
                descriptor.Id,
                descriptor.Category,
                message,
                effectiveSeverity.ToDiagnosticSeverity() ?? descriptor.DefaultSeverity,
                descriptor.DefaultSeverity,
                descriptor.IsEnabledByDefault,
                warningLevel: effectiveSeverity.WithDefaultSeverity(descriptor.DefaultSeverity) == ReportDiagnostic.Error ? 0 : 1,
                effectiveSeverity == ReportDiagnostic.Suppress,
                descriptor.Title,
                descriptor.Description,
                descriptor.HelpLinkUri,
                location,
                additionalLocations,
                descriptor.CustomTags,
                properties);
        }

        /// <summary>
        /// Returns the equivalent <see cref="DiagnosticSeverity"/> for a <see cref="ReportDiagnostic"/> value.
        /// </summary>
        /// <param name="reportDiagnostic">The <see cref="ReportDiagnostic"/> value.</param>
        /// <returns>
        /// The equivalent <see cref="DiagnosticSeverity"/> for a <see cref="ReportDiagnostic"/> value; otherwise,
        /// <see langword="null"/> if <see cref="DiagnosticSeverity"/> does not contain a direct equivalent for
        /// <paramref name="reportDiagnostic"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// If <paramref name="reportDiagnostic"/> is not one of the expected values.
        /// </exception>
        private static DiagnosticSeverity? ToDiagnosticSeverity(this ReportDiagnostic reportDiagnostic)
        {
            switch (reportDiagnostic)
            {
            case ReportDiagnostic.Error:
                return DiagnosticSeverity.Error;

            case ReportDiagnostic.Warn:
                return DiagnosticSeverity.Warning;

            case ReportDiagnostic.Info:
                return DiagnosticSeverity.Info;

            case ReportDiagnostic.Hidden:
                return DiagnosticSeverity.Hidden;

            case ReportDiagnostic.Suppress:
            case ReportDiagnostic.Default:
                return null;

            default:
                var o = reportDiagnostic;
                var output = $"Unexpected value '{o}' of type '{o.GetType().FullName}'";
                throw new InvalidOperationException(output);
            }
        }

        /// <summary>
        /// Applies a default severity to a <see cref="ReportDiagnostic"/> value.
        /// </summary>
        /// <param name="reportDiagnostic">The <see cref="ReportDiagnostic"/> value.</param>
        /// <param name="defaultSeverity">The default severity.</param>
        /// <returns>
        /// <para>If <paramref name="reportDiagnostic"/> is <see cref="ReportDiagnostic.Default"/>, returns
        /// <paramref name="defaultSeverity"/>.</para>
        /// <para>-or-</para>
        /// <para>Otherwise, returns <paramref name="reportDiagnostic"/> if it has a non-default value.</para>
        /// </returns>
        private static ReportDiagnostic WithDefaultSeverity(this ReportDiagnostic reportDiagnostic, DiagnosticSeverity defaultSeverity)
        {
            if (reportDiagnostic != ReportDiagnostic.Default)
            {
                return reportDiagnostic;
            }

            return defaultSeverity.ToReportDiagnostic();
        }

        /// <summary>
        /// Returns the equivalent <see cref="ReportDiagnostic"/> for a <see cref="DiagnosticSeverity"/> value.
        /// </summary>
        /// <param name="diagnosticSeverity">The <see cref="DiagnosticSeverity"/> value.</param>
        /// <returns>
        /// The equivalent <see cref="ReportDiagnostic"/> for the <see cref="DiagnosticSeverity"/> value.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// If <paramref name="diagnosticSeverity"/> is not one of the expected values.
        /// </exception>
        private static ReportDiagnostic ToReportDiagnostic(this DiagnosticSeverity diagnosticSeverity)
        {
            switch (diagnosticSeverity)
            {
            case DiagnosticSeverity.Hidden:
                return ReportDiagnostic.Hidden;

            case DiagnosticSeverity.Info:
                return ReportDiagnostic.Info;

            case DiagnosticSeverity.Warning:
                return ReportDiagnostic.Warn;

            case DiagnosticSeverity.Error:
                return ReportDiagnostic.Error;

            default:
                var o = diagnosticSeverity;
                var output = $"Unexpected value '{o}' of type '{o.GetType().FullName}'";
                throw new InvalidOperationException(output);
            }
        }

        public sealed class LocalizableStringWithArguments : LocalizableString
        {
            private readonly LocalizableString _messageFormat;
            private readonly string[] _formatArguments;

            public LocalizableStringWithArguments(LocalizableString messageFormat, params object[] formatArguments)
            {
                if (messageFormat == null)
                {
                    throw new ArgumentNullException(nameof(messageFormat));
                }

                if (formatArguments == null)
                {
                    throw new ArgumentNullException(nameof(formatArguments));
                }

                _messageFormat = messageFormat;
                _formatArguments = new string[formatArguments.Length];
                for (var i = 0; i < formatArguments.Length; i++)
                {
                    _formatArguments[i] = $"{formatArguments[i]}";
                }
            }

            protected override string GetText(IFormatProvider formatProvider)
            {
                var messageFormat = _messageFormat.ToString(formatProvider);
                return messageFormat != null ?
                    (_formatArguments.Length > 0 ? string.Format(formatProvider, messageFormat, _formatArguments) : messageFormat) :
                    string.Empty;
            }

            protected override bool AreEqual(object other)
            {
                var otherResourceString = other as LocalizableStringWithArguments;
                return otherResourceString != null &&
                    _messageFormat.Equals(otherResourceString._messageFormat) &&
                    SequenceEqual(_formatArguments, otherResourceString._formatArguments, (a, b) => a == b);
            }

            protected override int GetHash()
            {
                // TODO: include values in hash
                return _messageFormat.GetHashCode();
            }

            // TODO: unify definitions
            public static bool SequenceEqual<T>(IEnumerable<T> first, IEnumerable<T> second, Func<T, T, bool> comparer)
            {
                Debug.Assert(comparer != null);

                if (first == second)
                {
                    return true;
                }

                if (first == null || second == null)
                {
                    return false;
                }

                using (var enumerator = first.GetEnumerator())
                using (var enumerator2 = second.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (!enumerator2.MoveNext() || !comparer(enumerator.Current, enumerator2.Current))
                        {
                            return false;
                        }
                    }

                    if (enumerator2.MoveNext())
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
