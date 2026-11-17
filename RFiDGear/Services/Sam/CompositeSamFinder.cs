#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>What the SAM setup needs of a reader kind: a look at the SAMs that it holds, and a connection to one of them.</summary>
    internal interface ISamFinder
    {
        /// <summary>Looks for SAMs. It sends no command to a card that is not a SAM.</summary>
        /// <returns>The SAMs that were found, and whether the search could not be made at all.</returns>
        SamSearchResult Find();

        /// <summary>Connects to a SAM that <see cref="Find"/> listed.</summary>
        /// <param name="readerName">The name that <see cref="Find"/> gave.</param>
        /// <returns>The connection, or <see langword="null"/> when there is no SAM there (any more).</returns>
        SamConnection? Open(string readerName);
    }

    /// <summary>
    /// The list of the SAM setup made of what several finders see (the PC/SC readers, the SAM sockets of a TWN4). It merges what they find, says
    /// that the search failed only when every finder failed, and opens a SAM with the finder that listed it.
    /// </summary>
    internal sealed class CompositeSamFinder : ISamFinder
    {
        private readonly IReadOnlyList<ISamFinder> _finders;
        private readonly Dictionary<string, ISamFinder> _owners = new Dictionary<string, ISamFinder>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();

        /// <summary>Creates the finder.</summary>
        /// <param name="finders">The finders, in the order of the list.</param>
        internal CompositeSamFinder(params ISamFinder[] finders)
        {
            _finders = finders ?? throw new ArgumentNullException(nameof(finders));
        }

        /// <inheritdoc />
        public SamSearchResult Find()
        {
            var all = new List<SamReaderInfo>();
            var owners = new Dictionary<string, ISamFinder>(StringComparer.OrdinalIgnoreCase);
            var failed = 0;

            foreach (var finder in _finders)
            {
                try
                {
                    var result = finder.Find();

                    if (result.EnumerationFailed)
                    {
                        failed++;
                    }

                    foreach (var reader in result.Readers.Where(reader => owners.TryAdd(reader.Name, finder)))
                    {
                        all.Add(reader);
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // One kind of reader failing must not hide the others. Only the type: the text of a library exception is not for a log.
                    failed++;
                    Log.ForContext<CompositeSamFinder>().Warning("SAM search: a finder failed ({ExceptionType}).", e.GetType().Name);
                }
            }

            lock (_gate)
            {
                _owners.Clear();

                foreach (var owner in owners)
                {
                    _owners[owner.Key] = owner.Value;
                }
            }

            return new SamSearchResult(all, _finders.Count > 0 && failed == _finders.Count);
        }

        /// <inheritdoc />
        public SamConnection? Open(string readerName)
        {
            if (string.IsNullOrEmpty(readerName))
            {
                return null;
            }

            ISamFinder? owner;

            lock (_gate)
            {
                _owners.TryGetValue(readerName, out owner);
            }

            if (owner != null)
            {
                return owner.Open(readerName);
            }

            // A name that the last search did not list: the finders are asked in order, and the first connection is taken.
            foreach (var finder in _finders)
            {
                var connection = finder.Open(readerName);

                if (connection != null)
                {
                    return connection;
                }
            }

            return null;
        }
    }
}
