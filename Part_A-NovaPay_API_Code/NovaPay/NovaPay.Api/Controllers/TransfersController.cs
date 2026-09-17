using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaPay.Api.Dtos;
using NovaPay.Api.Services;

namespace NovaPay.Api.Controllers;

[ApiController]
[Authorize]
[Route("transfers")]
public class TransfersController(IWalletService walletService) : ControllerBase
{
    private const string IdempotencyHeaderName = "Idempotency-Key";

    /// <summary>
    /// Moves funds between two wallets. Requires an "Idempotency-Key" header so retries (e.g. over
    /// a flaky NIP/USSD round trip) never double-move money; the same key with the same payload
    /// replays the original result instead of re-executing the transfer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransferResponse>> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        [FromHeader(Name = IdempotencyHeaderName)] string? idempotencyKeyHeader,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKeyHeader))
        {
            return Problem(
                title: "Missing Idempotency-Key",
                detail: $"The '{IdempotencyHeaderName}' header is required for POST /transfers.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var idempotencyKey = idempotencyKeyHeader.Trim();
        if (idempotencyKey.Length > 200)
        {
            return Problem(
                title: "Invalid Idempotency-Key",
                detail: $"The '{IdempotencyHeaderName}' header must not exceed 200 characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var transfer = await walletService.TransferAsync(
            request.FromWalletId, request.ToWalletId, request.AmountKobo, idempotencyKey, ct);

        var response = TransferResponse.From(transfer);
        return Created($"/transfers/{transfer.Id}", response);
    }
}
