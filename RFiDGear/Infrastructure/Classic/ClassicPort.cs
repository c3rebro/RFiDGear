#nullable enable
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.Classic
{
    /// <summary>The answer of a primitive of <see cref="IClassicPort"/> that returns no data.</summary>
    /// <param name="Error">What happened: <see cref="ERROR.NoError"/>, or the reason it did not work.</param>
    public readonly record struct ClassicPortResult(ERROR Error)
    {
        /// <summary>Gets a value that says whether the primitive worked.</summary>
        public bool Succeeded => Error == ERROR.NoError;

        /// <summary>Gets the answer of a primitive that worked.</summary>
        public static ClassicPortResult Ok => new ClassicPortResult(ERROR.NoError);
    }

    /// <summary>The answer of <see cref="IClassicPort.ReadBlockAsync(int)"/>.</summary>
    /// <param name="Error">What happened: <see cref="ERROR.NoError"/>, or the reason it did not work.</param>
    /// <param name="Data">The 16 bytes of the block when it worked, otherwise <c>null</c>.</param>
    public readonly record struct ClassicBlockResult(ERROR Error, byte[]? Data)
    {
        /// <summary>Gets a value that says whether a whole block was read.</summary>
        public bool Succeeded => Error == ERROR.NoError && Data is { Length: ClassicLayout.BytesPerBlock };
    }

    /// <summary>
    /// What a reader has to be able to do for a MIFARE Classic chip, and nothing more: log in to a sector, read a block, write a block, and
    /// wake the chip again. Everything that needs judgement (which key, which block, what the access bits allow, the MAD) is done once, above
    /// this interface, in <see cref="ClassicOperations"/>; the readers only move bytes.
    /// <para>
    /// Rules that every implementation keeps, because the chips and readers showed them:
    /// </para>
    /// <list type="bullet">
    /// <item><description>One attempt. A primitive never repeats itself and never tries another key: a card that is taken away in the middle of a write must not be written twice.</description></item>
    /// <item><description>A login that returns <see cref="ERROR.NoError"/> does not prove that the chip accepted the key: a PC/SC reader answers a login with key B in a sector where key B is data with success and fails at the next access. Only the first access after a login proves it.</description></item>
    /// <item><description>After any failure the chip may be halted, and the next command fails once. The caller calls <see cref="ReselectAsync"/> before it logs in again.</description></item>
    /// <item><description>A reader that has no chip, or loses it, answers <see cref="ERROR.TransportError"/>; a chip that refuses answers <see cref="ERROR.AuthFailure"/> (the key, the access conditions) or <see cref="ERROR.PermissionDenied"/> (a reader that refuses the call).</description></item>
    /// </list>
    /// </summary>
    public interface IClassicPort
    {
        /// <summary>Gets the size of the chip on the reader, or <c>null</c> when the reader does not know it.</summary>
        ClassicCardSize? Size { get; }

        /// <summary>Logs in to the sector that holds a block with one key (one attempt).</summary>
        /// <param name="block">Any block of the sector, as the chip numbers it (see <see cref="ClassicLayout"/>).</param>
        /// <param name="keyType">Which key it is.</param>
        /// <param name="key">The 6 bytes of the key.</param>
        /// <returns>The outcome; <see cref="ERROR.NoError"/> means that the reader accepted the call, see the remarks of the interface.</returns>
        Task<ClassicPortResult> LoginAsync(int block, ClassicKeyType keyType, byte[] key);

        /// <summary>Reads one block of the sector that was logged in to.</summary>
        /// <param name="block">The block, as the chip numbers it.</param>
        /// <returns>The 16 bytes, or why not.</returns>
        Task<ClassicBlockResult> ReadBlockAsync(int block);

        /// <summary>Writes one block of the sector that was logged in to (one attempt, never repeated).</summary>
        /// <param name="block">The block, as the chip numbers it.</param>
        /// <param name="data">The 16 bytes.</param>
        /// <returns>The outcome.</returns>
        Task<ClassicPortResult> WriteBlockAsync(int block, byte[] data);

        /// <summary>Wakes the chip again after a failure, so that the next login is the first command it sees. The login is gone with it.</summary>
        /// <returns>The outcome; <see cref="ERROR.TransportError"/> when the chip is gone.</returns>
        Task<ClassicPortResult> ReselectAsync();
    }
}
