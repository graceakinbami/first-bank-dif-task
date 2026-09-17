using System.ComponentModel.DataAnnotations;

namespace NovaPay.Api.Dtos;

public record CreateTransferRequest(
    [Required] Guid FromWalletId,
    [Required] Guid ToWalletId,
    [Range(1, long.MaxValue, ErrorMessage = "AmountKobo must be a positive number of kobo.")]
    long AmountKobo
);

public record TransferResponse(
    Guid Id,
    Guid FromWalletId,
    Guid ToWalletId,
    long AmountKobo,
    string Status,
    DateTime CreatedAtUtc
)
{
    public static TransferResponse From(Domain.Transfer t) =>
        new(t.Id, t.FromWalletId, t.ToWalletId, t.AmountKobo, "Completed", t.CreatedAtUtc);
}
