using BlinkyAccess.Client;
using mRemoteNG.App;
using mRemoteNG.Messages;
using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace mRemoteNG.Config.BlinkyAccess
{
    /// <summary>
    /// The Blinky Access portal address, the personal access token and the startup
    /// switch, read from and written to the user settings.
    ///
    /// The token is only ever stored DPAPI protected and scoped to the current Windows
    /// user. It is deliberately kept out of confCons.xml, out of every export and out
    /// of every log line, so a copied connections file carries no portal access.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class BlinkyAccessSettings
    {
        /// <summary>How much of the token the options page may show.</summary>
        private const int PreviewLength = 4;

        public static string PortalAddress
        {
            get => Properties.OptionsBlinkyAccessPage.Default.PortalAddress ?? "";
            set => Properties.OptionsBlinkyAccessPage.Default.PortalAddress = (value ?? "").Trim();
        }

        public static bool SyncOnStartup
        {
            get => Properties.OptionsBlinkyAccessPage.Default.SyncOnStartup;
            set => Properties.OptionsBlinkyAccessPage.Default.SyncOnStartup = value;
        }

        public static bool HasToken => !string.IsNullOrEmpty(Properties.OptionsBlinkyAccessPage.Default.ProtectedAccessToken);

        /// <summary>True once an address and a token are both present.</summary>
        public static bool IsConfigured => PortalAddress.Length > 0 && HasToken;

        /// <summary>
        /// Unprotects the stored token. Returns an empty string when none is stored or
        /// when the protected blob was written by another Windows user, in which case
        /// it is dropped so the user is asked for a new token instead of hitting an
        /// unexplained failure on every sync.
        /// </summary>
        public static string GetToken()
        {
            string stored = Properties.OptionsBlinkyAccessPage.Default.ProtectedAccessToken ?? "";

            if (stored.Length == 0)
                return "";

            try
            {
                byte[] plainBytes = ProtectedData.Unprotect(Convert.FromBase64String(stored), null, DataProtectionScope.CurrentUser);
                string token = Encoding.UTF8.GetString(plainBytes);
                Array.Clear(plainBytes, 0, plainBytes.Length);
                return token;
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                Runtime.MessageCollector?.AddMessage(MessageClass.WarningMsg,
                    "The stored Blinky Access token could not be read on this account and has been discarded.", true);
                ClearToken();
                return "";
            }
        }

        /// <summary>Protects and stores a token, or clears the stored one when given nothing.</summary>
        public static void SetToken(string token)
        {
            string trimmed = (token ?? "").Trim();

            if (trimmed.Length == 0)
            {
                ClearToken();
                return;
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(trimmed);

            try
            {
                byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                Properties.OptionsBlinkyAccessPage.Default.ProtectedAccessToken = Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                Array.Clear(plainBytes, 0, plainBytes.Length);
            }
        }

        public static void ClearToken()
        {
            Properties.OptionsBlinkyAccessPage.Default.ProtectedAccessToken = "";
        }

        /// <summary>
        /// The beginning of the stored token, which is all the options page shows. An
        /// empty string when no token is stored.
        /// </summary>
        public static string GetTokenPreview()
        {
            return BuildPreview(GetToken());
        }

        /// <summary>
        /// The visible part of a token: its <c>bla_pat_</c> prefix and a few characters
        /// of the secret, so the user can recognise which token is stored without the
        /// whole of it being on screen.
        /// </summary>
        public static string BuildPreview(string token)
        {
            if (string.IsNullOrEmpty(token))
                return "";

            int visible = Math.Min(token.Length, BlinkyAccessOptions.TokenPrefix.Length + PreviewLength);
            return token.Substring(0, visible) + "...";
        }

        /// <summary>
        /// Builds the options for one call. The caller owns the returned token for as
        /// long as the client lives.
        /// </summary>
        public static BlinkyAccessOptions BuildClientOptions()
        {
            return new BlinkyAccessOptions
            {
                PortalUrl = PortalAddress,
                Token = GetToken()
            };
        }
    }
}
