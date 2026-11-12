namespace RFiDGear.Contracts
{
    /// <summary>
    /// What the host does with the key it asks for. One task can ask for the same slot reference in more than one role
    /// (a key change needs the key that authenticates, the key that is being replaced and the key that replaces it).
    /// </summary>
    public enum SecureKeyUsage
    {
        /// <summary>The key authenticates to the card: the key the card has now.</summary>
        Authenticate = 0,

        /// <summary>The key a key change replaces: what the card holds in the slot now.</summary>
        ChangeKeyCurrent = 1,

        /// <summary>The key a key change writes: what the card will hold in the slot afterwards.</summary>
        ChangeKeyNew = 2,
    }
}
