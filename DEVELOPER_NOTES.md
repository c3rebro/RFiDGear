# Developer Notes

## One-attempt rule
Operations that change card state (authentication, file creation, key rotation, etc.) must only be attempted once per request. The provider layer should rely on the caller to orchestrate retries rather than automatically repeating failed calls. Automatic retries can leave a reader or card in an undefined state and mask the original failure reason. When a command fails, return the appropriate error code and let the orchestrator decide whether to re-run the task.

## Normalized provider error codes
The provider layer now returns a small, normalized set of error codes to make downstream handling consistent. These codes are surfaced by `ReaderDevice` and its implementations and should be propagated without collapsing distinct conditions into a generic catch-all:

- `AuthFailure`: authentication or key negotiation failed.
- `PermissionDenied`: access was explicitly refused by policy or configuration.
- `ProtocolConstraint`: the requested operation violated protocol constraints (e.g., resource already exists, limits exceeded).
- `TransportError`: communication problems such as unavailable readers or timeouts.
- `Unknown`: unexpected state or unclassified errors that do not map to the categories above.

When you add or update provider logic, choose the most specific code that matches the condition so that calling layers can react accurately (e.g., prompting for different credentials versus retrying the connection).
