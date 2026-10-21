using Microsoft.Win32;
using mRemoteNG.App;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

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
        private const string PuttyRegistryKey = @"Software\SimonTatham\PuTTY";

        // identifiers already handed to Pageant in this process
        private static readonly HashSet<string> LoadedIdentifiers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Makes sure Pageant holds <paramref name="certificateIdentifier"/>, using the
        /// pageant.exe that sits next to the configured PuTTY. Failures are reported and
        /// swallowed: the connection itself may still work without the agent.
        /// </summary>
        public static void EnsureCertificateLoaded(string certificateIdentifier)
        {
            if (string.IsNullOrEmpty(certificateIdentifier))
                return;

            lock (LoadedIdentifiers)
            {
                if (!LoadedIdentifiers.Add(certificateIdentifier))
                    return;
            }

            string pageantPath = GetPageantPath();
            if (pageantPath == null)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.PageantNotFound);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = pageantPath,
                    Arguments = certificateIdentifier,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

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
        /// pageant.exe from the same installation as the configured PuTTY, so a custom
        /// path pointing at PuTTY CAC also gives us PuTTY CAC's Pageant.
        /// </summary>
        private static string GetPageantPath()
        {
            string puttyPath = PuttyBase.PuttyPath;
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
