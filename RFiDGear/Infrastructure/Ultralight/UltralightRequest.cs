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
                data == null ? null : (byte[])data.Clone());   // the call can wait for the port, and the caller may reuse its array meanwhile
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
                    return map.ContainsReadableRange(StartPage, PageCount) ? ERROR.NoError : ERROR.ProtocolConstraint;

                case UltralightOperation.Write:
                    return UltralightWritePolicy.Check(map, StartPage, PageCount, Access.AllowProtectedPages).IsAllowed ? ERROR.NoError : ERROR.ProtocolConstraint;

                default:
                    return ERROR.NoError;
            }
        }

        /// <summary>Clears the key, the password, the PACK and the copy of the bytes to write (a page of a password is among them).</summary>
        public void Dispose()
        {
            if (Data != null)
            {
                CryptographicOperations.ZeroMemory(Data);
            }

            if (Key != null)
            {
                CryptographicOperations.ZeroMemory(Key);
            }

            if (Pack != null)
            {
                CryptographicOperations.ZeroMemory(Pack);
            }
        }

        /// <summary>Whether a way of authenticating is one the chip has: the key of an Ultralight C, the password of an EV1 or an NTAG, or none.</summary>
        /// <param name="auth">The way of authenticating.</param>
        /// <param name="variant">The chip.</param>
        /// <returns><c>true</c> when the chip has it.</returns>
        internal static bool AuthenticationFits(UltralightAuth auth, UltralightVariant variant)
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

            // A number that is none of the expectations (from a project file, say) is not Auto: nothing is asked of the port for it.
            if (!Enum.IsDefined(typeof(UltralightExpectation), Access.Expected))
            {
                return;
            }

            if (Operation == UltralightOperation.Authenticate && Access.Auth == UltralightAuth.None)
            {
                return;
            }

            if (Access.Auth != UltralightAuth.None && !TryReadSecrets())
            {
                return;
            }

            if ((Operation == UltralightOperation.Read && !ReadIsInOrder()) || (Operation == UltralightOperation.Write && !WriteIsInOrder()))
            {
                return;
            }

            Error = ERROR.NoError;
        }

        // What is true for every chip; the layout of the chip that is found decides the rest. A chip that the task names has its
        // layout known now, and a READ that reaches the key of an Ultralight C goes on at page 0, so those pages are not read.
        private bool ReadIsInOrder()
        {
            if (PageCount < 1 || StartPage < 0 || PageCount > UltralightMemoryMap.MaxPageCount - StartPage)
            {
                return false;
            }

            return !Access.Expected.TryGetVariant(out var variant) || UltralightMemoryMap.For(variant).ContainsReadableRange(StartPage, PageCount);
        }

        private bool WriteIsInOrder() =>
            PageCount >= 1 && UltralightWritePolicy.Check(Access.Expected, StartPage, PageCount, Access.AllowProtectedPages).IsAllowed;

        // Authenticating and the key need a chip that is named: the key of an Ultralight C is not the password of an NTAG, and the
        // reader cannot know which to ask for before the card answers.
        private bool TryReadSecrets()
        {
            if (!Access.Expected.TryGetVariant(out var variant) || !AuthenticationFits(Access.Auth, variant))
            {
                return false;
            }

            var keyLength = Access.Auth == UltralightAuth.UltralightC ? 16 : 4;

            if (!TryParseHex(Access.KeyHex, keyLength, out var key))
            {
                return false;
            }

            // The request owns the key from here on: when the PACK is wrong the request ends, and Dispose clears the key.
            Key = key;

            if (Access.Auth == UltralightAuth.UltralightC)
            {
                return true;
            }

            if (!TryParseHex(Access.PackHex, 2, out var pack))
            {
                return false;
            }

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
