using System;
using System.Security.Cryptography;

namespace RFiDGear.Infrastructure.Ultralight
{
    /// <summary>The page operations of a reader.</summary>
    internal enum UltralightOperation
    {
        Read,
        Write,
        Authenticate
    }

    /// <summary>
    /// One call of a reader, checked before the port or the card is touched, with the key and the password as bytes. Whatever is
    /// wrong with the call itself is found here (<see cref="Error"/>), and what depends on the chip that is found is checked
    /// with <see cref="CheckAgainst"/> once the reader knows it. The bytes of the key are cleared by <see cref="Dispose"/>.
    /// </summary>
    internal sealed class UltralightRequest : IDisposable
    {
        private UltralightRequest(UltralightOperation operation, UltralightAccess access, int startPage, int pageCount, byte[] data)
        {
            Operation = operation;
            Access = access;
            StartPage = startPage;
            PageCount = pageCount;
            Data = data;
        }

        /// <summary>Gets the operation.</summary>
        public UltralightOperation Operation { get; }

        /// <summary>Gets the access.</summary>
        public UltralightAccess Access { get; }

        /// <summary>Gets the first page of a read or a write; 0 for an authentication.</summary>
        public int StartPage { get; }

        /// <summary>Gets the number of pages of a read or a write; 0 for an authentication.</summary>
        public int PageCount { get; private set; }

        /// <summary>Gets the bytes to write.</summary>
        public byte[] Data { get; }

        /// <summary>Gets the key (Ultralight C: 16 bytes) or the password (4 bytes), or <c>null</c> without authentication.</summary>
        public byte[] Key { get; private set; }

        /// <summary>Gets the PACK (2 bytes) for a password, or <c>null</c>.</summary>
        public byte[] Pack { get; private set; }

        /// <summary>Gets <see cref="ERROR.NoError"/> when the call is in order, else why it cannot be made. Nothing was done to a card.</summary>
        public ERROR Error { get; private set; }

        /// <summary>A request to read pages.</summary>
        /// <param name="access">The access.</param>
        /// <param name="startPage">The first page.</param>
        /// <param name="pageCount">The number of pages.</param>
        /// <returns>The request, with its <see cref="Error"/>.</returns>
        public static UltralightRequest ForRead(UltralightAccess access, int startPage, int pageCount)
        {
            var request = new UltralightRequest(UltralightOperation.Read, access, startPage, pageCount, null);
            request.Validate();

            return request;
        }

        /// <summary>A request to write pages.</summary>
        /// <param name="access">The access.</param>
        /// <param name="startPage">The first page.</param>
        /// <param name="data">The bytes to write, a whole number of 4-byte pages.</param>
        /// <returns>The request, with its <see cref="Error"/>.</returns>
        public static UltralightRequest ForWrite(UltralightAccess access, int startPage, byte[] data)
        {
            var wellFormed = data != null && data.Length > 0 && data.Length % UltralightMemoryMap.PageSize == 0;
            var request = new UltralightRequest(
                UltralightOperation.Write,
                access,
                startPage,
                wellFormed ? data.Length / UltralightMemoryMap.PageSize : 0,
                data);
            request.Validate();

            return request;
        }

        /// <summary>A request to authenticate.</summary>
        /// <param name="access">The access, which names how to authenticate.</param>
        /// <returns>The request, with its <see cref="Error"/>.</returns>
        public static UltralightRequest ForAuthenticate(UltralightAccess access)
        {
            var request = new UltralightRequest(UltralightOperation.Authenticate, access, 0, 0, null);
            request.Validate();

            return request;
        }

        /// <summary>
        /// What depends on the chip that was found: that the task expects it, that it has the pages, that a write is allowed, and
        /// that the way of authenticating is one the chip has.
        /// </summary>
        /// <param name="map">The layout of the chip.</param>
        /// <returns><see cref="ERROR.NoError"/> or <see cref="ERROR.ProtocolConstraint"/>.</returns>
        public ERROR CheckAgainst(UltralightMemoryMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!Access.Expected.Accepts(map.Variant) || !AuthenticationFits(Access.Auth, map.Variant))
            {
                return ERROR.ProtocolConstraint;
            }

            switch (Operation)
            {
                case UltralightOperation.Read:
                    return map.ContainsRange(StartPage, PageCount) ? ERROR.NoError : ERROR.ProtocolConstraint;

                case UltralightOperation.Write:
                    return UltralightWritePolicy.Check(map, StartPage, PageCount, Access.AllowProtectedPages).IsAllowed ? ERROR.NoError : ERROR.ProtocolConstraint;

                default:
                    return ERROR.NoError;
            }
        }

        /// <summary>Clears the key, the password and the PACK.</summary>
        public void Dispose()
        {
            if (Key != null)
            {
                CryptographicOperations.ZeroMemory(Key);
            }

            if (Pack != null)
            {
                CryptographicOperations.ZeroMemory(Pack);
            }
        }

        private static bool AuthenticationFits(UltralightAuth auth, UltralightVariant variant)
        {
            switch (auth)
            {
                case UltralightAuth.None:
                    return true;

                case UltralightAuth.UltralightC:
                    return variant == UltralightVariant.C;

                case UltralightAuth.Password:
                    return variant != UltralightVariant.Plain && variant != UltralightVariant.C;

                default:
                    return false;
            }
        }

        private void Validate()
        {
            Error = ERROR.ProtocolConstraint;

            if (Operation == UltralightOperation.Authenticate && Access.Auth == UltralightAuth.None)
            {
                return;
            }

            if (Access.Auth != UltralightAuth.None && !TryReadSecrets())
            {
                return;
            }

            switch (Operation)
            {
                case UltralightOperation.Read:
                    // What is true for every chip; the layout of the chip that is found decides the rest.
                    if (PageCount < 1 || StartPage < 0 || PageCount > UltralightMemoryMap.MaxPageCount - StartPage)
                    {
                        return;
                    }

                    if (Access.Expected.TryGetVariant(out var variant) && !UltralightMemoryMap.For(variant).ContainsRange(StartPage, PageCount))
                    {
                        return;
                    }

                    break;

                case UltralightOperation.Write:
                    if (PageCount < 1 || !UltralightWritePolicy.Check(Access.Expected, StartPage, PageCount, Access.AllowProtectedPages).IsAllowed)
                    {
                        return;
                    }

                    break;
            }

            Error = ERROR.NoError;
        }

        // Authenticating and the key need a chip that is named: the key of an Ultralight C is not the password of an NTAG, and the
        // reader cannot know which to ask for before the card answers.
        private bool TryReadSecrets()
        {
            if (!Access.Expected.TryGetVariant(out var variant) || !AuthenticationFits(Access.Auth, variant))
            {
                return false;
            }

            if (Access.Auth == UltralightAuth.UltralightC)
            {
                return TryParseHex(Access.KeyHex, 16, out var key) && Assign(key, null);
            }

            return TryParseHex(Access.KeyHex, 4, out var password) && TryParseHex(Access.PackHex, 2, out var pack) && Assign(password, pack);
        }

        private bool Assign(byte[] key, byte[] pack)
        {
            Key = key;
            Pack = pack;

            return true;
        }

        private static bool TryParseHex(string text, int length, out byte[] bytes)
        {
            bytes = null;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var digits = new System.Text.StringBuilder(text.Length);

            foreach (var c in text)
            {
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    continue;
                }

                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }

                digits.Append(c);
            }

            if (digits.Length != 2 * length)
            {
                return false;
            }

            bytes = Convert.FromHexString(digits.ToString());

            return true;
        }
    }
}
