namespace RFiDGear.Infrastructure.Checksums
{
    /// <summary>
    /// Supported CRC-8 polynomials. Cast the enum value to a byte when sending the
    /// polynomial identifier over the wire to a reader or card.
    /// </summary>
    /// <example>
    /// <code>
    /// byte polynomialId = (byte)Crc8Polynomial.CRC8_DALLAS_MAXIM;
    /// </code>
    /// </example>
    public enum Crc8Polynomial : byte
    {
        CRC8 = 0xd5,
        CRC8_CCITT = 0x07,
        CRC8_DALLAS_MAXIM = 0x31,
        CRC8_SAE_J1850 = 0x1D,
        CRC_8_WCDMA = 0x9b,
    }
}
