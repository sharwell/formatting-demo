﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Options;
using Microsoft.VisualStudio.CodingConventions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.CodeAnalysis.Tools.CodeFormatter.Options
{
    internal class EditorConfigOptionsApplier
    {
        private IReadOnlyList<(IOption, OptionStorageLocation, MethodInfo)> _formattingOptionsWithStorage;

        public EditorConfigOptionsApplier()
        {
            var commonOptionsType = typeof(Formatting.FormattingOptions);
            var csharpOptionsType = typeof(CSharp.Formatting.CSharpFormattingOptions);
            _formattingOptionsWithStorage = GetOptionsWithStorageFromTypes(new[] { commonOptionsType, csharpOptionsType });
        }

        public OptionSet ApplyConventions(OptionSet optionSet, ICodingConventionsSnapshot codingConventions, string languageName)
        {
            foreach (var optionWithStorage in _formattingOptionsWithStorage)
            {
                if (TryGetConventionValue(optionWithStorage, codingConventions, out var value))
                {
                    var option = optionWithStorage.Item1;
                    var optionKey = new OptionKey(option, option.IsPerLanguage ? languageName : null);
                    optionSet = optionSet.WithChangedOption(optionKey, value);
                }
            }

            return optionSet;
        }

        private OptionSet ApplyConventionsForOptions(OptionSet optionSet, IEnumerable<(IOption, OptionStorageLocation, MethodInfo)> optionsWithStorage, ICodingConventionsSnapshot codingConventions, string languageName)
        {
            foreach (var optionWithStorage in optionsWithStorage)
            {
                if (TryGetConventionValue(optionWithStorage, codingConventions, out var value))
                {
                    var option = optionWithStorage.Item1;
                    var optionKey = new OptionKey(option, option.IsPerLanguage ? languageName : null);
                    optionSet = optionSet.WithChangedOption(optionKey, value);
                }
            }

            return optionSet;
        }

        internal IReadOnlyList<(IOption, OptionStorageLocation, MethodInfo)> GetOptionsWithStorageFromTypes(params Type[] formattingOptionTypes)
        {
            var optionType = typeof(IOption);
            return formattingOptionTypes
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty))
                .Where(p => optionType.IsAssignableFrom(p.PropertyType))
                .Select(p => (IOption)p.GetValue(null))
                .Select(GetOptionWithStorage)
                .Where(ows => ows.Item2 != null)
                .ToList();
        }

        internal (IOption, OptionStorageLocation, MethodInfo) GetOptionWithStorage(IOption option)
        {
            var editorConfigStorage = !option.StorageLocations.IsDefaultOrEmpty
                ? option.StorageLocations.FirstOrDefault(IsEditorConfigStorage)
                : null;
            var tryGetOptionMethod = editorConfigStorage?.GetType().GetMethod("TryGetOption");
            return (option, editorConfigStorage, tryGetOptionMethod);
        }

        internal static bool IsEditorConfigStorage(OptionStorageLocation storageLocation)
        {
            return storageLocation.GetType().FullName.StartsWith("Microsoft.CodeAnalysis.Options.EditorConfigStorageLocation");
        }

        internal static bool TryGetConventionValue((IOption, OptionStorageLocation, MethodInfo) optionWithStorage, ICodingConventionsSnapshot codingConventions, out object value)
        {
            var (option, editorConfigStorage, tryGetOptionMethod) = optionWithStorage;

            value = null;
            var args = new object[] { option, codingConventions.AllRawConventions, option.Type, value };

            var containedOption = (bool)tryGetOptionMethod.Invoke(editorConfigStorage, args);
            value = args[3];

            return containedOption;
        }
    }
}
