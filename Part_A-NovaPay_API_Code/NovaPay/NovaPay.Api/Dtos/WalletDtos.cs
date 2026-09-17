using System.ComponentModel.DataAnnotations;

namespace NovaPay.Api.Dtos;

public record CreateWalletRequest(
    [Required, StringLength(200, MinimumLength = 1)] string OwnerName
);

public record CreditWalletRequest(
    [Range(0, long.MaxValue, ErrorMessage = "AmountKobo must not be negative.")]
    long AmountKobo
);

public record WalletResponse(
    Guid Id,
    string OwnerName,
    long BalanceKobo,
    DateTime CreatedAtUtc
)
{
    public static WalletResponse From(Domain.Wallet w) => new(w.Id, w.OwnerName, w.BalanceKobo, w.CreatedAtUtc);
}
