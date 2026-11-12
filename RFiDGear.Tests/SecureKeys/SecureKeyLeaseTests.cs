using RFiDGear.Contracts;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>A lease is the only place a resolved key lives: it must copy, zero, and never show itself.</summary>
    public class SecureKeyLeaseTests
    {
        private static readonly byte[] DistinctiveKey = { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC };

        [Fact]
        public void The_lease_holds_a_copy_of_the_key()
        {
            var source = (byte[])DistinctiveKey.Clone();

            using var lease = new SecureKeyLease(source);
            Array.Clear(source);

            Assert.Equal(DistinctiveKey.Length, lease.Length);
            Assert.True(lease.Key.SequenceEqual(DistinctiveKey));
        }

        [Fact]
        public void A_key_without_bytes_is_refused()
        {
            Assert.Throws<ArgumentException>(() => new SecureKeyLease(ReadOnlySpan<byte>.Empty));
        }

        [Fact]
        public void The_hex_string_is_upper_case_without_separators()
        {
            using var lease = new SecureKeyLease(DistinctiveKey);

            Assert.Equal("123456789ABC", lease.ToHexString());
        }

        [Fact]
        public void Disposing_zeroes_the_bytes()
        {
            var lease = new SecureKeyLease(DistinctiveKey);
            var span = lease.Key;

            lease.Dispose();

            // The span still points at the lease's buffer: this is the proof that it was overwritten, not just hidden.
            Assert.All(span.ToArray(), b => Assert.Equal(0, b));
        }

        [Fact]
        public void Disposing_twice_is_harmless()
        {
            var lease = new SecureKeyLease(DistinctiveKey);

            lease.Dispose();
            lease.Dispose();

            Assert.Throws<ObjectDisposedException>(() => lease.ToHexString());
        }

        [Fact]
        public void A_disposed_lease_hands_out_nothing()
        {
            var lease = new SecureKeyLease(DistinctiveKey);
            lease.Dispose();

            Assert.Throws<ObjectDisposedException>(() => lease.Key.ToArray());
            Assert.Throws<ObjectDisposedException>(() => lease.ToHexString());
            Assert.Equal(DistinctiveKey.Length, lease.Length);
        }

        [Fact]
        public void Disposing_from_many_threads_at_once_throws_nothing_and_leaves_zeros()
        {
            var lease = new SecureKeyLease(DistinctiveKey);
            var view = lease.Key;
            var failures = new ConcurrentBag<Exception>();
            using var start = new Barrier(8);

            var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() =>
            {
                try
                {
                    start.SignalAndWait();
                    lease.Dispose();
                }
                catch (Exception e)
                {
                    failures.Add(e);
                }
            })).ToList();

            threads.ForEach(thread => thread.Start());
            threads.ForEach(thread => thread.Join());

            Assert.Empty(failures);
            Assert.All(view.ToArray(), b => Assert.Equal(0, b));
            Assert.Throws<ObjectDisposedException>(() => lease.ToHexString());
        }

        [Fact]
        public void Nothing_that_prints_the_lease_shows_the_key()
        {
            using var lease = new SecureKeyLease(DistinctiveKey);

            var printed = new[]
            {
                lease.ToString(),
                $"{lease}",
                string.Format("{0}", lease),
                typeof(SecureKeyLease).GetCustomAttribute<DebuggerDisplayAttribute>()?.Value ?? string.Empty,
            };

            Assert.All(printed, text =>
            {
                Assert.DoesNotContain("123456", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("9ABC", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(Convert.ToBase64String(DistinctiveKey), text, StringComparison.Ordinal);
                Assert.Contains("redacted", text, StringComparison.Ordinal);
            });
        }

        [Fact]
        public void The_debugger_does_not_list_the_buffer()
        {
            var buffers = typeof(SecureKeyLease)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(byte[]))
                .ToList();

            Assert.NotEmpty(buffers);
            Assert.All(buffers, field =>
                Assert.Equal(
                    DebuggerBrowsableState.Never,
                    field.GetCustomAttribute<DebuggerBrowsableAttribute>()?.State));
        }

        [Fact]
        public void The_lease_offers_no_way_to_compare_or_save_itself()
        {
            var type = typeof(SecureKeyLease);
            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            Assert.Null(type.GetCustomAttribute<SerializableAttribute>());
            Assert.DoesNotContain(type.GetMethods(declared), method => method.Name is "Equals" or "GetHashCode" or "Finalize");
            Assert.Empty(type.GetInterfaces().Where(i => i.Name.StartsWith("IEquatable", StringComparison.Ordinal) || i.Name.StartsWith("ISerializable", StringComparison.Ordinal)));
        }
    }
}
