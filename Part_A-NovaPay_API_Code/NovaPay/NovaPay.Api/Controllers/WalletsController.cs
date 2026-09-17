using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaPay.Api.Dtos;
using NovaPay.Api.Services;

namespace NovaPay.Api.Controllers;

[ApiController]
[Authorize]
[Route("wallets")]
public class WalletsController(IWalletService walletService) : ControllerBase
{
    /// <summary>Creates a new wallet with a zero starting balance.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<WalletResponse>> CreateWallet(
        [FromBody] CreateWalletRequest request, CancellationToken ct)
    {
        var wallet = await walletService.CreateWalletAsync(request.OwnerName, ct);
        var response = WalletResponse.From(wallet);
        return CreatedAtAction(nameof(GetWallet), new { id = wallet.Id }, response);
    }

    /// <summary>Returns a wallet's current balance, in kobo.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WalletResponse>> GetWallet(Guid id, CancellationToken ct)
    {
        var wallet = await walletService.GetWalletAsync(id, ct);
        return Ok(WalletResponse.From(wallet));
    }

    /// <summary>Deposits funds into a wallet. Amount is in kobo.</summary>
    [HttpPost("{id:guid}/credit")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WalletResponse>> CreditWallet(
        Guid id, [FromBody] CreditWalletRequest request, CancellationToken ct)
    {
        var wallet = await walletService.CreditWalletAsync(id, request.AmountKobo, ct);
        return Ok(WalletResponse.From(wallet));
    }
}
