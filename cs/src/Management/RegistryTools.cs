// <copyright file="RegistryTools.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.DevTunnels.Contracts;
using Microsoft.Win32;

namespace Microsoft.DevTunnels.Management
{
    internal class RegistryTools
    {
        /// <summary>
        /// Get registry key value from the HKLM root Registry.
        /// </summary>
        /// <param name="regKeyPath"></param>
        /// <param name="defaultOnError"></param>
        /// <returns></returns>
        public object GetRegistryValueFromLocalMachineRoot(string regKeyPath, object? defaultOnError = null)
        {
            object regValue = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                regValue = this.GetRegistryValue(rootKey, regKeyPath, defaultOnError);
            }
            return regValue;
        }

        /// <summary>
        /// Get registry key settings int value.
        /// </summary>
        /// <param name="rootKey">Root key entry</param>
        /// <param name="regKeyPath">Path to Key</param>
        /// <param name="defaultOnError"></param>
        /// <returns></returns>
        private object GetRegistryValue(RegistryKey rootKey, string regKeyPath, object? defaultOnError = null)
        {
            StringBuilder headerBuilder = new StringBuilder();

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (rootKey != null)
                    {
                        using (RegistryKey? subKey = rootKey.OpenSubKey(regKeyPath, RegistryKeyPermissionCheck.ReadSubTree, System.Security.AccessControl.RegistryRights.QueryValues))
                        {
                            if (subKey != null)
                            {
                                foreach (string valueName in subKey.GetValueNames())
                                {
                                    object? value = subKey.GetValue(valueName);
                                    if (value != null)
                                    {
                                        string headerValue = value.ToString()!;

                                        headerBuilder.AppendFormat("{0}={1}; ", Uri.EscapeDataString(valueName), Uri.EscapeDataString(headerValue!));
                                    }
                                }
                            }
                        }

                        if (headerBuilder.Length > 0)
                        {
                            headerBuilder.Length -= 2;
                        }
                    }
                }
            }
            catch
            {
            }

            return headerBuilder.ToString();
        }
    }
}