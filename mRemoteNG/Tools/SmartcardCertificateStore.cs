using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace mRemoteNG.Tools
{
    /// <summary>
    /// Reads the certificates that can be used for SSH public key authentication with
    /// PuTTY CAC out of the current user's certificate store.
    ///
    /// The interesting ones are those whose private key lives on a card, which is what
    /// the key storage provider tells us. A store typically holds many certificates
    /// whose keys are long gone, and offering those to a server wastes authentication
    /// attempts.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class SmartcardCertificateStore
    {
        public const string SmartCardKeyStorageProvider = "Microsoft Smart Card Key Storage Provider";

        public class CertificateEntry
        {
            public string Thumbprint { get; init; } = "";
            public string Subject { get; init; } = "";
            public string Issuer { get; init; } = "";
            public string UserPrincipalName { get; init; } = "";
            public DateTime NotAfter { get; init; }
            public bool OnSmartCard { get; init; }

            /// <summary>Key container the store entry points at.</summary>
            public string KeyContainer { get; init; } = "";

            /// <summary>
            /// True when that key container is present on a card that is plugged in
            /// right now. A store entry whose container is gone cannot authenticate,
            /// and Windows reports it as "the smart card cannot perform the requested
            /// operation or the operation requires a different smart card".
            /// </summary>
            public bool ContainerPresent { get; init; }

            /// <summary>The identifier PuTTY CAC expects for the Windows certificate store.</summary>
            public string CapiIdentifier => $"CAPI:{Thumbprint}";

            public bool IsExpired => DateTime.Now > NotAfter;
        }

        /// <summary>
        /// Lists candidate certificates. With <paramref name="onlySmartCard"/> only
        /// certificates whose key is on a card are returned.
        /// </summary>
        public static IReadOnlyList<CertificateEntry> List(bool onlySmartCard = true)
        {
            List<CertificateEntry> entries = [];

            using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly);
            }
            catch (Exception)
            {
                return entries;
            }

            HashSet<string> liveContainers = GetContainersOnInsertedCards();

            foreach (X509Certificate2 certificate in store.Certificates)
            {
                string provider = GetKeyProviderInfo(certificate, out string container);
                bool onCard = provider != null && provider.Contains("Smart Card", StringComparison.OrdinalIgnoreCase);

                if (onlySmartCard && !onCard)
                    continue;

                if (!onlySmartCard && provider == null)
                    continue;

                entries.Add(new CertificateEntry
                {
                    Thumbprint = certificate.Thumbprint ?? "",
                    Subject = certificate.Subject,
                    Issuer = certificate.Issuer,
                    UserPrincipalName = GetUserPrincipalName(certificate),
                    NotAfter = certificate.NotAfter,
                    OnSmartCard = onCard,
                    KeyContainer = container ?? "",
                    ContainerPresent = container != null && liveContainers.Contains(container)
                });
            }

            return [.. entries
                .OrderByDescending(e => e.ContainerPresent)
                .ThenByDescending(e => e.OnSmartCard)
                .ThenBy(e => e.Subject, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// PuTTY CAC identifies a certificate in the Windows store by its SHA-1
        /// thumbprint. Tools around PIV tokens happily print a SHA-256 fingerprint of
        /// the same certificate, and passing that silently fails to match any key, so a
        /// 64 character hex value is looked up by SHA-256 and translated.
        ///
        /// Anything else is returned unchanged.
        /// </summary>
        public static string NormalizeCapiIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier) || !identifier.StartsWith("CAPI:", StringComparison.OrdinalIgnoreCase))
                return identifier;

            string value = identifier[5..].Replace(" ", "").Replace(":", "");
            if (value.Length != 64 || !value.All(Uri.IsHexDigit))
                return identifier;

            try
            {
                using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                foreach (X509Certificate2 certificate in store.Certificates)
                {
                    if (string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), value, StringComparison.OrdinalIgnoreCase))
                        return $"CAPI:{certificate.Thumbprint}";
                }
            }
            catch (Exception)
            {
                // fall through and let PuTTY report what it makes of the value
            }

            return identifier;
        }

        /// <summary>
        /// True when the private key of this certificate belongs to a smart card
        /// provider.
        ///
        /// The provider name is read from the certificate's stored key provider info.
        /// Opening the key instead would make Windows reach for the matching card, and
        /// a store that holds certificates of cards which are not plugged in then puts
        /// up a "select a smart card device" prompt and blocks for minutes.
        /// </summary>
        public static bool IsOnSmartCard(X509Certificate2 certificate)
        {
            string provider = GetKeyProviderInfo(certificate, out _);

            return provider != null &&
                   provider.Contains("Smart Card", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Names of the keys held by cards that are plugged in right now. Enumerating
        /// the smart card key storage provider reads the cards themselves without
        /// opening a key, so no PIN is asked for and no card selection prompt appears.
        ///
        /// An empty set means no card is readable, which is treated as "cannot tell"
        /// rather than "every certificate is dead".
        /// </summary>
        public static HashSet<string> GetContainersOnInsertedCards()
        {
            HashSet<string> containers = new(StringComparer.OrdinalIgnoreCase);
            IntPtr provider = IntPtr.Zero;
            IntPtr enumState = IntPtr.Zero;

            try
            {
                if (NativeMethods.NCryptOpenStorageProvider(out provider, SmartCardKeyStorageProvider, 0) != 0)
                    return containers;

                while (true)
                {
                    int status = NativeMethods.NCryptEnumKeys(provider, null, out IntPtr keyNamePtr, ref enumState, 0);
                    if (status != 0 || keyNamePtr == IntPtr.Zero)
                        break;

                    try
                    {
                        NativeMethods.NCryptKeyName keyName = Marshal.PtrToStructure<NativeMethods.NCryptKeyName>(keyNamePtr);
                        if (!string.IsNullOrEmpty(keyName.pszName))
                            containers.Add(keyName.pszName);
                    }
                    finally
                    {
                        NativeMethods.NCryptFreeBuffer(keyNamePtr);
                    }
                }
            }
            catch (Exception)
            {
                // no reader, no card, or a provider that refuses to enumerate
            }
            finally
            {
                if (enumState != IntPtr.Zero)
                    NativeMethods.NCryptFreeBuffer(enumState);
                if (provider != IntPtr.Zero)
                    NativeMethods.NCryptFreeObject(provider);
            }

            return containers;
        }

        /// <summary>
        /// Reads CERT_KEY_PROV_INFO_PROP_ID, the provider and key container recorded for
        /// this certificate in the store. Returns null when the certificate has no key
        /// associated.
        /// </summary>
        private static string GetKeyProviderInfo(X509Certificate2 certificate, out string containerName)
        {
            containerName = null;
            const uint certKeyProvInfoPropId = 2;

            try
            {
                IntPtr handle = certificate.Handle;
                if (handle == IntPtr.Zero)
                    return null;

                uint size = 0;
                if (!NativeMethods.CertGetCertificateContextProperty(handle, certKeyProvInfoPropId, IntPtr.Zero, ref size) || size == 0)
                    return null;

                IntPtr buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (!NativeMethods.CertGetCertificateContextProperty(handle, certKeyProvInfoPropId, buffer, ref size))
                        return null;

                    NativeMethods.CRYPT_KEY_PROV_INFO info =
                        Marshal.PtrToStructure<NativeMethods.CRYPT_KEY_PROV_INFO>(buffer);

                    containerName = info.pwszContainerName;
                    return info.pwszProvName;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static class NativeMethods
        {
            [DllImport("crypt32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CertGetCertificateContextProperty(
                IntPtr pCertContext,
                uint dwPropId,
                IntPtr pvData,
                ref uint pcbData);

            [DllImport("ncrypt.dll")]
            public static extern int NCryptOpenStorageProvider(out IntPtr phProvider,
                [MarshalAs(UnmanagedType.LPWStr)] string pszProviderName, uint dwFlags);

            [DllImport("ncrypt.dll")]
            public static extern int NCryptEnumKeys(IntPtr hProvider,
                [MarshalAs(UnmanagedType.LPWStr)] string pszScope, out IntPtr ppKeyName,
                ref IntPtr ppEnumState, uint dwFlags);

            [DllImport("ncrypt.dll")]
            public static extern int NCryptFreeBuffer(IntPtr pvInput);

            [DllImport("ncrypt.dll")]
            public static extern int NCryptFreeObject(IntPtr hObject);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct NCryptKeyName
            {
                [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
                [MarshalAs(UnmanagedType.LPWStr)] public string pszAlgid;
                public uint dwLegacyKeySpec;
                public uint dwFlags;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct CRYPT_KEY_PROV_INFO
            {
                [MarshalAs(UnmanagedType.LPWStr)] public string pwszContainerName;
                [MarshalAs(UnmanagedType.LPWStr)] public string pwszProvName;
                public uint dwProvType;
                public uint dwFlags;
                public uint cProvParam;
                public IntPtr rgProvParam;
                public uint dwKeySpec;
            }
        }

        /// <summary>
        /// Pulls the user principal name out of the subject alternative name, which is
        /// what SSSD matches the certificate against. Best effort only: the formatted
        /// extension text is culture dependent, so the value is treated as a display
        /// hint, never as something to act on.
        /// </summary>
        private static string GetUserPrincipalName(X509Certificate2 certificate)
        {
            try
            {
                X509Extension extension = certificate.Extensions
                    .FirstOrDefault(e => e.Oid?.Value == "2.5.29.17");

                if (extension == null)
                    return "";

                Match match = Regex.Match(extension.Format(false), @"[\w.+-]+@[\w.-]+\.\w+");
                return match.Success ? match.Value : "";
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
