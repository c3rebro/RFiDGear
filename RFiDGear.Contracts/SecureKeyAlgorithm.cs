namespace RFiDGear.Contracts
{
    /// <summary>
    /// The algorithm a card key is used with. It decides how many bytes the key has, and a provider has to answer
    /// with exactly that many (the host checks it before the first card call and fails the task otherwise).
    /// </summary>
    public enum SecureKeyAlgorithm
    {
        /// <summary>
        /// DESFire single DES. 16 bytes, written the way the application hands a DES key to the card: two equal halves
        /// of 8 bytes.
        /// </summary>
        Des = 0,

        /// <summary>DESFire two-key triple DES (2K3DES). 16 bytes.</summary>
        TripleDes2K = 1,

        /// <summary>DESFire three-key triple DES (3K3DES). 24 bytes.</summary>
        TripleDes3K = 2,

        /// <summary>DESFire AES-128. 16 bytes.</summary>
        Aes128 = 3,

        /// <summary>MIFARE Classic key A or key B. 6 bytes.</summary>
        Classic6Byte = 4,
    }
}
