// <copyright file="DictionaryRegularExpressionAttribute.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.DevTunnels.Contracts.Validation;

/// <summary>
/// Similar to <see cref="RegularExpressionAttribute"/> but validates every key in a
/// string dictionary.
/// </summary>
public class DictionaryRegularExpressionAttribute : RegularExpressionAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryRegularExpressionAttribute"/> class,
    /// with a regular expression pattern.
    /// </summary>
    public DictionaryRegularExpressionAttribute(string pattern) : base(pattern)
    {
    }

    /// <summary>
    /// Checks whether all keys in the dictionary match the regular expression pattern.
    /// </summary>
    /// <remarks>
    /// Null dictionary keys are not considered valid. A null dictionary is valid unless there
    /// is also a <see cref="RequiredAttribute"/> applied.
    /// </remarks>
    public override bool IsValid(object? value)
    {
        var dictionary = value as IDictionary<string, string>;
        if (dictionary == null)
        {
            if (value != null)
            {
                throw new InvalidOperationException(
                    "Value to be validated must be a dictionary of strings.");
            }

            // RequiredAttribute should be used to assert a value is not null.
            return true;
        }

        foreach (var key in dictionary.Keys)
        {
            if (key == null || !base.IsValid(key))
            {
                return false;
            }
        }

        return true;
    }
}
