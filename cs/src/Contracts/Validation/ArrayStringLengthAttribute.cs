// <copyright file="ArrayStringLengthAttribute.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.DevTunnels.Contracts.Validation;

/// <summary>
/// Similar to <see cref="StringLengthAttribute"/> but validates every item in an array.
/// </summary>
/// <remarks>
/// Also works with any other <see cref="IEnumerable{T}"/> of strings.
/// </remarks>
public class ArrayStringLengthAttribute : StringLengthAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayStringLengthAttribute"/> class,
    /// with a maximum length.
    /// </summary>
    public ArrayStringLengthAttribute(int maximumLength) : base(maximumLength)
    {
    }

    /// <summary>
    /// Checks whether all items in an array value are a valid length.
    /// </summary>
    /// <remarks>
    /// Null array items are not considered valid. A null array is valid unless there is also a
    /// <see cref="RequiredAttribute"/> applied.
    /// </remarks>
    public override bool IsValid(object? value)
    {
        var values = value as IEnumerable<string>;
        if (values == null)
        {
            // RequiredAttribute should be used to assert a value is not null.
            return true;
        }

        foreach (var s in values)
        {
            if (s == null || !base.IsValid(s))
            {
                return false;
            }
        }

        return true;
    }
}
