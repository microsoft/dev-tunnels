// <copyright file="PolicyProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.DevTunnels.Contracts;
using Microsoft.Win32;

namespace Microsoft.DevTunnels.Management
{
    /// <summary>
    /// Provides methods to get and format policy settings from the Windows Registry.
    /// </summary>
    public class PolicyProvider
    {
        private readonly string regKeyPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="PolicyProvider"/> class.
        /// </summary>
        /// <param name="regKeyPath">The registry path where the policy settings are stored on the local machine</param>
        public PolicyProvider(string regKeyPath)
        {
            this.regKeyPath = regKeyPath ?? throw new ArgumentException(nameof(regKeyPath));
        }

        /// <summary>
        /// Get formatted registry values as a header string from the Local Machine Hive. 
        /// </summary>
        /// <param name="defaultOnError">default value to return on error</param>
        /// <returns>A string formatted for use in an Http header, or a 'null' as a default value.</returns>
        public string? GetHeaderValue(string? defaultOnError = null)
        {
            
            string regValue = string.Empty;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return defaultOnError;
            }
            try
            {
                using var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                regValue = this.CreateHeaderString(rootKey, regKeyPath);

            }
            catch
            {
                return defaultOnError;
            }
           
            return regValue.Length > 1024 ? regValue.Substring(0, 1024) : regValue;
        }

        /// <summary>
        /// Format registry key values into a semicolon-delimited string.  
        /// </summary>
        /// <param name="rootKey">Root key entry</param>
        /// <param name="regKeyPath">Path to Key</param>
        /// <returns>A semicolon-delimited string of key-value pairs.</returns>
        private string CreateHeaderString(RegistryKey rootKey, string regKeyPath)
        {
            var headerBuilder = new StringBuilder();


#pragma warning disable CA1416 // This code will only run on Windows
            using var subKey = rootKey.OpenSubKey(regKeyPath, RegistryKeyPermissionCheck.ReadSubTree, System.Security.AccessControl.RegistryRights.QueryValues);

            if (subKey != null)
            {
                foreach (string valueName in subKey.GetValueNames())
                {
                    var value = subKey.GetValue(valueName, "");
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                    {
                        string headerValue = value.ToString()!;
                        headerBuilder.AppendFormat("{0}={1}; ", Uri.EscapeDataString(valueName), Uri.EscapeDataString(headerValue!));
                    }
                }
                if (headerBuilder.Length > 0)
                {
                    headerBuilder.Length -= 2; // Remove trailing semicolon and space
                }
            }
#pragma warning restore CA1416

            return headerBuilder.ToString();
        } 

    }
}