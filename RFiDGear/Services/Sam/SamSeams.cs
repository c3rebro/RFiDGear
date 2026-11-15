#nullable enable
using System;
using System.Collections.Generic;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// One command APDU to a SAM and its answer, below everything that knows what a SAM command means. The only
    /// implementation that touches LibLogicalAccess is the one inside <see cref="LlaSamReaderSource"/>; the tests use a script.
    /// </summary>
    internal interface ISamTransport
    {
        /// <summary>
        /// Sends one command APDU and returns what the card answered: the data field and the two bytes of the status word.
        /// An error status word is an answer, not an exception.
        /// </summary>
        /// <param name="apdu">The complete command APDU.</param>
        /// <returns>The complete answer, status word last.</returns>
        byte[] Exchange(byte[] apdu);
    }

    /// <summary>A connection to the card in a PC/SC reader. Disposing it lets go of the card and the reader.</summary>
    internal interface ISamCardConnection : IDisposable
    {
        /// <summary>The answer to reset of the card.</summary>
        byte[] Atr { get; }

        /// <summary>The way to send APDUs to this card.</summary>
        ISamTransport Transport { get; }
    }

    /// <summary>The PC/SC readers of this machine, and a way to connect to the card in one of them.</summary>
    internal interface ISamReaderSource
    {
        /// <summary>The names of the PC/SC readers. Throws when the PC/SC service cannot be reached.</summary>
        /// <returns>The reader names.</returns>
        IReadOnlyList<string> GetReaderNames();

        /// <summary>Connects to the card in a reader, sending nothing to it.</summary>
        /// <param name="readerName">The reader, as <see cref="GetReaderNames"/> named it.</param>
        /// <returns>The connection, or <c>null</c> when the reader has no card (or no such reader exists).</returns>
        ISamCardConnection? Connect(string readerName);
    }
}
