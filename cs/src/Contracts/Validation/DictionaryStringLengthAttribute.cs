// <copyright file="ArrayStringLengthAttribute.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.DevTunnels.Contracts.Validation;

/// <summary>
/// Validates the length of string values in a dictionary.
/// </summary>
public class DictionaryStringLengthAttribute : StringLengthAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryStringLengthAttribute"/> class,
    /// with a maximum length.
    /// </summary>
    public DictionaryStringLengthAttribute(int maximumLength) : base(maximumLength)
    {
    }

    /// <summary>
    /// Checks whether all items in the dictionary are a valid length.
    /// </summary>
    /// <remarks>
    /// Null dictionary items are not considered valid. A null dictionary is valid unless
    /// there is also a <see cref="RequiredAttribute"/> applied.
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

        foreach (var item in dictionary.Values)
        {
            if (item == null || !base.IsValid(item))
            {
                return false;
            }
        }

        return true;
    }
}
