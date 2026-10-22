using mRemoteNG.Connection.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace mRemoteNG.Tools
{
    /// <summary>
    /// Finds a PuTTY that can authenticate with a certificate.
    ///
    /// PuTTY CAC carries the same file version information as stock PuTTY, so there is
    /// no reliable way to tell them apart by metadata. What can be ruled out is the
    /// bundled PuTTYNG, which has no certificate support at all. The candidates are
    /// therefore ordered by how likely they are to be the right build, starting with
    /// the installation whose Pageant is already running with certificates loaded.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class PuttyCacLocator
    {
        private static string? _cached;

        /// <summary>
        /// Path of a putty.exe that is not the bundled PuTTYNG, or null when none was
        /// found. The result is remembered for the lifetime of the process.
        /// </summary>
        public static string? Find()
        {
            if (_cached != null)
                return _cached;

            foreach (string candidate in GetCandidates())
            {
                if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate))
                    continue;

                if (PuttyTypeDetector.GetPuttyType(candidate) == PuttyTypeDetector.PuttyType.PuttyNg)
                    continue;

                _cached = candidate;
                return _cached;
            }

            return null;
        }

        private static IEnumerable<string> GetCandidates()
        {
            // a configured client that is not PuTTYNG was chosen deliberately
            string configured = PuttyBase.PuttyPath ?? "";
            if (!string.IsNullOrEmpty(configured))
                yield return configured;

            // the installation whose Pageant is running is the one holding the card
            foreach (string pageantDirectory in GetRunningPageantDirectories())
                yield return Path.Combine(pageantDirectory, "putty.exe");

            foreach (string directory in new[]
                     {
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PuTTY CAC"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PuTTY"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PuTTY CAC"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PuTTY"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\PuTTY CAC"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\PuTTY")
                     })
            {
                yield return Path.Combine(directory, "putty.exe");
            }
        }

        private static IEnumerable<string> GetRunningPageantDirectories()
        {
            List<string> directories = [];

            foreach (Process process in Process.GetProcessesByName("pageant"))
            {
                try
                {
                    string? fileName = process.MainModule?.FileName;
                    string? directory = string.IsNullOrEmpty(fileName) ? null : Path.GetDirectoryName(fileName);

                    if (!string.IsNullOrEmpty(directory))
                        directories.Add(directory);
                }
                catch (Exception)
                {
                    // a process of another user or a different integrity level
                }
                finally
                {
                    process.Dispose();
                }
            }

            return directories;
        }
    }
}
