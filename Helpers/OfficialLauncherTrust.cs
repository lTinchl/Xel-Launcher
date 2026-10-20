using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace XelLauncher.Helpers;

internal static class OfficialLauncherTrust
{
    internal static bool IsTrusted(string path, bool global)
    {
        if (!File.Exists(path) || !Path.GetFileName(path).Equals("Games.exe", StringComparison.OrdinalIgnoreCase))
            return false;
        return VerifyPublisher(path, publisher => string.Equals(publisher,
            global ? "GRYPH FRONTIER PTE. LTD." : "Shanghai Hypergryph Network Technology Co., Ltd.",
            StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsTrustedGameFile(string path) =>
        File.Exists(path) && VerifyPublisher(path, publisher =>
            publisher.Contains("Hypergryph", StringComparison.OrdinalIgnoreCase) ||
            publisher.Contains("GRYPHLINE", StringComparison.OrdinalIgnoreCase) ||
            publisher.Contains("GRYPH FRONTIER", StringComparison.OrdinalIgnoreCase));

    private static bool VerifyPublisher(string path, Func<string, bool> acceptsPublisher)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        IntPtr filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        var data = new TrustData
        {
            Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1,
            File = filePointer, StateAction = 1,
            // Check the chain, including revocation, and honor timestamped signatures.
            ProviderFlags = 0x80
        };
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try
        {
            Marshal.StructureToPtr(file, filePointer, false);
            if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) return false;
#pragma warning disable SYSLIB0057
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            string publisher = certificate.GetNameInfo(X509NameType.SimpleName, false);
            return acceptsPublisher(publisher);
        }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
        finally
        {
            data.StateAction = 2;
            WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            Marshal.DestroyStructure<TrustFile>(filePointer);
            Marshal.FreeHGlobal(filePointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public IntPtr File, KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public IntPtr PolicyCallback, SipClient;
        public uint UiChoice, RevocationChecks, UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData, Url;
        public uint ProviderFlags, UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
