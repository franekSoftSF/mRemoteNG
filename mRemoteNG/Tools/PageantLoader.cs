using Microsoft.Win32;
using mRemoteNG.App;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;

namespace mRemoteNG.Tools
{
    /// <summary>
    /// Loads a smart card certificate into Pageant, which is what makes agent
    /// forwarding usable: the agent has to hold the certificate before the session is
    /// opened, because forwarding is negotiated when the session starts. Loading it
    /// afterwards does not apply to a session that is already up.
    ///
    /// Handing the identifier to an already running Pageant adds the certificate to
    /// that agent rather than starting a second one, so this is safe to repeat.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class PageantLoader
    {
        private const int AgentReadyTimeoutMs = 5000;

        private const string PuttyRegistryKey = @"Software\SimonTatham\PuTTY";

        // identifiers already handed to Pageant in this process
        private static readonly HashSet<string> LoadedIdentifiers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Makes sure Pageant holds <paramref name="certificateIdentifier"/>, using the
        /// pageant.exe that sits next to the PuTTY this connection runs. Failures are
        /// reported and swallowed: the connection itself may still work without the agent.
        /// </summary>
        /// <param name="puttyExecutable">
        /// The client this connection will actually run. A connection with a certificate
        /// is switched away from the bundled PuTTYNG to a PuTTY CAC found elsewhere, and
        /// its Pageant is the one that can hold a certificate — the configured path
        /// still points at PuTTYNG, which has no Pageant beside it at all.
        /// </param>
        public static void EnsureCertificateLoaded(string certificateIdentifier, string puttyExecutable)
        {
            if (string.IsNullOrEmpty(certificateIdentifier))
                return;

            lock (LoadedIdentifiers)
            {
                if (!LoadedIdentifiers.Add(certificateIdentifier))
                    return;
            }

            string pageantPath = GetPageantPath(puttyExecutable);
            if (pageantPath == null)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.PageantNotFound);
                return;
            }

            WarnIfForeignPageantIsRunning(pageantPath);

            try
            {
                Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = pageantPath,
                    Arguments = certificateIdentifier,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                // Agent forwarding is negotiated as the session opens, so the key has to
                // be in the agent before PuTTY starts. Handing the key to a running
                // Pageant makes this process exit; if it does not exit it has become the
                // agent itself, and then its window has to be up before PuTTY looks for
                // it.
                if (process != null && !process.WaitForExit(AgentReadyTimeoutMs))
                    WaitForPageantWindow(AgentReadyTimeoutMs);

                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                    $"{Language.PageantCertificateLoaded}: {certificateIdentifier}", true);
            }
            catch (Exception ex)
            {
                lock (LoadedIdentifiers)
                    LoadedIdentifiers.Remove(certificateIdentifier);

                Runtime.MessageCollector.AddExceptionMessage(Language.PageantLoadFailed, ex);
            }
        }

        /// <summary>
        /// A Pageant from a different installation cannot hold a certificate: stock
        /// Pageant has no CAPI support, so handing it "CAPI:&lt;thumbprint&gt;" does
        /// nothing and the forwarded agent stays empty. That failure is silent, which
        /// is why it is called out here.
        /// </summary>
        private static void WarnIfForeignPageantIsRunning(string expectedPath)
        {
            foreach (Process process in Process.GetProcessesByName("pageant"))
            {
                try
                {
                    string runningPath = process.MainModule?.FileName ?? "";

                    if (!string.IsNullOrEmpty(runningPath) &&
                        !string.Equals(runningPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                            $"{Language.PageantForeignInstance}: {runningPath} (expected {expectedPath})");
                    }
                }
                catch (Exception)
                {
                    // a process of another user, or one that exited meanwhile
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        /// <summary>
        /// Waits until Pageant's IPC window exists, which is how PuTTY finds the agent.
        /// </summary>
        private static void WaitForPageantWindow(int timeoutMs)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (NativeMethods.FindWindow("Pageant", "Pageant") != IntPtr.Zero)
                    return;

                Thread.Sleep(100);
            }

            Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.PageantNotReady);
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        }

        /// <summary>
        /// Pageant's "Autoload Certs" setting makes it enumerate the whole certificate
        /// store on every start. The agent then offers those keys one at a time, and
        /// OpenSSH stops after MaxAuthTries (6 by default), so the card's key may never
        /// be reached. The symptom is an unhelpful "Server refused our key".
        /// </summary>
        public static void WarnIfAutoloadCertsEnabled()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(PuttyRegistryKey);
                object value = key?.GetValue("AutoloadCerts");

                if (value is int enabled && enabled != 0)
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.PageantAutoloadCertsEnabled);
            }
            catch (Exception)
            {
                // the setting is a diagnostic aid, never a reason to fail a connection
            }
        }

        /// <summary>
        /// pageant.exe from the same installation as the PuTTY being run, so the client
        /// picked for a certificate also gives us that installation's Pageant. Only when
        /// no client was named does the configured path stand in.
        /// </summary>
        internal static string GetPageantPath(string puttyExecutable)
        {
            string puttyPath = string.IsNullOrEmpty(puttyExecutable) ? PuttyBase.PuttyPath : puttyExecutable;
            if (string.IsNullOrEmpty(puttyPath))
                return null;

            string directory = Path.GetDirectoryName(puttyPath);
            if (string.IsNullOrEmpty(directory))
                return null;

            string pageantPath = Path.Combine(directory, "pageant.exe");
            return File.Exists(pageantPath) ? pageantPath : null;
        }
    }
}
