using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace XelLauncher.Helpers
{
    public static class SecretProtector
    {
        private const string Prefix = "dpapi:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("XelLauncher.SkylandTokens.v1");

        public static string Protect(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var data = CreateBlob(Encoding.UTF8.GetBytes(text));
            var entropy = CreateBlob(Entropy);
            DATA_BLOB output = default;

            try
            {
                if (!CryptProtectData(ref data, "XelLauncher", ref entropy, IntPtr.Zero, IntPtr.Zero, 0, out output))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                return Prefix + Convert.ToBase64String(ReadBlob(output));
            }
            finally
            {
                FreeBlob(data);
                FreeBlob(entropy);
                if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
            }
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrWhiteSpace(protectedText)) return "";
            if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported protected data format.");

            var encrypted = Convert.FromBase64String(protectedText.Substring(Prefix.Length));
            var data = CreateBlob(encrypted);
            var entropy = CreateBlob(Entropy);
            DATA_BLOB output = default;

            try
            {
                if (!CryptUnprotectData(ref data, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, 0, out output))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                return Encoding.UTF8.GetString(ReadBlob(output));
            }
            finally
            {
                FreeBlob(data);
                FreeBlob(entropy);
                if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
            }
        }

        private static DATA_BLOB CreateBlob(byte[] data)
        {
            var blob = new DATA_BLOB { cbData = data.Length };
            blob.pbData = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, blob.pbData, data.Length);
            return blob;
        }

        private static byte[] ReadBlob(DATA_BLOB blob)
        {
            var data = new byte[blob.cbData];
            Marshal.Copy(blob.pbData, data, 0, blob.cbData);
            return data;
        }

        private static void FreeBlob(DATA_BLOB blob)
        {
            if (blob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(blob.pbData);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(
            ref DATA_BLOB pDataIn,
            string szDataDescr,
            ref DATA_BLOB pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            out DATA_BLOB pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(
            ref DATA_BLOB pDataIn,
            IntPtr ppszDataDescr,
            ref DATA_BLOB pOptionalEntropy,
            IntPtr pvReserved,
            IntPtr pPromptStruct,
            int dwFlags,
            out DATA_BLOB pDataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);
    }
}
