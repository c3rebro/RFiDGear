#nullable enable
using System;
using System.Collections.Concurrent;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// Remembers the text of a resource key. The resources read the settings from the disk on every lookup, and what a window or a
    /// control shows asks for its texts again whenever something about it changes; the language cannot change while they live (it
    /// needs a restart of the application), so a text is asked for once.
    /// </summary>
    internal static class CachedText
    {
        /// <summary>Wraps a way to get a text so that each key is asked for once.</summary>
        /// <param name="source">Gives the text of a resource key.</param>
        /// <returns>A function that gives the same texts and asks <paramref name="source"/> once for each key.</returns>
        internal static Func<string, string> Of(Func<string, string> source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var cache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

            return key => cache.GetOrAdd(key, source);
        }
    }
}
