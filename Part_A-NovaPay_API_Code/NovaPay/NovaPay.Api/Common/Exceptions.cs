namespace NovaPay.Api.Common;

/// <summary>Base type for domain errors that map to a specific HTTP status code via ExceptionHandlingMiddleware.</summary>
public abstract class NovaPayException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
    public abstract string Title { get; }
}

public class WalletNotFoundException(Guid walletId)
    : NovaPayException($"Wallet '{walletId}' was not found.")
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string Title => "Wallet not found";
}

public class InsufficientFundsException(Guid walletId)
    : NovaPayException($"Wallet '{walletId}' has insufficient funds for this transfer.")
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string Title => "Insufficient funds";
}

public class DailyLimitExceededException(Guid walletId, long limitKobo)
    : NovaPayException($"Wallet '{walletId}' would exceed its daily outbound transfer limit of {limitKobo} kobo.")
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string Title => "Daily transfer limit exceeded";
}

public class IdempotencyKeyConflictException(string idempotencyKey)
    : NovaPayException($"Idempotency-Key '{idempotencyKey}' was already used with a different request payload.")
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string Title => "Idempotency key conflict";
}
