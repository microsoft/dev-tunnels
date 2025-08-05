// <copyright file="ArrayStringLengthAttribute.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.DevTunnels.Contracts.Validation;

/// <summary>
/// Validates the number of items in a string dictionary.
/// </summary>
public class DictionaryCountAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryCountAttribute"/> class,
    /// with a maximum count.
    /// </summary>
    public DictionaryCountAttribute(int maximumCount)
    {
        if (maximumCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount), "Maximum count must be non-negative.");
        }

        MaximumCount = maximumCount;
    }

    /// <summary>
    /// Gets the maximum number of items allowed in the dictionary.
    /// </summary>
    public int MaximumCount { get; }

    /// <summary>
    /// Checks whether the number of items in the dictionary is valid.
    /// </summary>
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

        return dictionary.Count <= MaximumCount;
    }
}
