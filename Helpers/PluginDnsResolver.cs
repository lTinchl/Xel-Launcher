using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Hi3Helper.Plugin.Core;

namespace XelLauncher.Helpers
{
    /// <summary>
    /// Supplies the plugin HTTP stack with rotating DNS results for Hypergryph
    /// CDN hosts. Some CloudFront edges can accept TCP and then reset TLS; the
    /// socket layer cannot try the remaining DNS addresses after that point.
    /// Rotating the first address lets the plugin's existing request retry move
    /// to another edge while retaining all addresses for ordinary TCP fallback.
    /// </summary>
    internal static unsafe class PluginDnsResolver
    {
        private static readonly ConcurrentDictionary<string, RotationCounter>
            RotationCounters = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte>
            LoggedHosts = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte>
            LoggedFailures = new(StringComparer.OrdinalIgnoreCase);

        // The managed delegate must remain rooted for the lifetime of the
        // unmanaged callback pointer registered with Plugin.Core.
        private static readonly SharedDnsResolverCallback ResolverCallback =
            ResolveDns;
        private static int _configured;

        public static void Configure()
        {
            if (Interlocked.Exchange(ref _configured, 1) != 0) return;

            try
            {
                var setter = typeof(SharedStatic).GetMethod(
                    "SetDnsResolverCallback",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(nint) },
                    modifiers: null);
                if (setter == null)
                    throw new MissingMethodException(
                        typeof(SharedStatic).FullName,
                        "SetDnsResolverCallback");

                var callbackPointer = Marshal.GetFunctionPointerForDelegate(
                    ResolverCallback);
                setter.Invoke(null, new object[] { callbackPointer });
                LogHelper.Log(
                    "Plugin DNS resolver configured: Hypergryph CDN address rotation enabled");
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _configured, 0);
                LogHelper.LogError(ex, "Configure plugin DNS resolver");
            }
        }

        internal static IPAddress[] OrderAddresses(
            string hostname,
            IPAddress[] addresses)
        {
            if (addresses == null || addresses.Length == 0)
                return Array.Empty<IPAddress>();

            var distinct = addresses
                .Where(address => address != null &&
                                  address.AddressFamily is
                                  AddressFamily.InterNetwork or
                                  AddressFamily.InterNetworkV6)
                .Distinct()
                .ToArray();
            if (distinct.Length < 2 || !IsHypergryphCdnHost(hostname))
                return distinct;

            var counter = RotationCounters.GetOrAdd(
                hostname ?? "", _ => new RotationCounter());
            // Start at the second DNS answer. Subsequent connections advance
            // through every address and wrap around deterministically.
            var start = (int)((uint)Interlocked.Increment(ref counter.Value) %
                              (uint)distinct.Length);
            if (start == 0) return distinct;

            var ordered = new IPAddress[distinct.Length];
            for (var index = 0; index < distinct.Length; index++)
                ordered[index] = distinct[(start + index) % distinct.Length];
            return ordered;
        }

        private static bool IsHypergryphCdnHost(string hostname) =>
            !string.IsNullOrWhiteSpace(hostname) &&
            (hostname.Equals("hg-cdn.com", StringComparison.OrdinalIgnoreCase) ||
             hostname.EndsWith(".hg-cdn.com", StringComparison.OrdinalIgnoreCase));

        private static unsafe void ResolveDns(
            char* hostname,
            char* writeBuffer,
            int writeBufferLength,
            int* writtenAddressCount)
        {
            if (writtenAddressCount == null) return;
            *writtenAddressCount = 0;
            if (hostname == null || writeBuffer == null || writeBufferLength <= 1)
                return;

            var host = new string(hostname);
            try
            {
                var addresses = OrderAddresses(
                    host, Dns.GetHostAddresses(host));
                var offset = 0;
                var written = 0;
                foreach (var address in addresses)
                {
                    var value = address.ToString();
                    if (offset + value.Length + 1 > writeBufferLength) break;

                    value.AsSpan().CopyTo(
                        new Span<char>(writeBuffer + offset, value.Length));
                    writeBuffer[offset + value.Length] = '\0';
                    offset += value.Length + 1;
                    written++;
                }

                *writtenAddressCount = written;
                if (written > 0 && IsHypergryphCdnHost(host) &&
                    LoggedHosts.TryAdd(host, 0))
                {
                    LogHelper.Log(
                        $"Plugin DNS rotation active: Host={host} | " +
                        $"Addresses={string.Join(',', addresses.Select(
                            address => address.ToString()))}");
                }
            }
            catch (Exception ex)
            {
                if (LoggedFailures.TryAdd(host, 0))
                    LogHelper.LogError(ex, $"Plugin DNS resolver: {host}");
            }
        }

        private sealed class RotationCounter
        {
            public int Value;
        }
    }
}
