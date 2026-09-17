# NovaPay Wallet API

A .NET 8 ASP.NET Core Web API implementing the wallet/transfer core for FirstBank NovaPay's
NovaWallet module: create wallets, credit them, and move money between them safely — atomically,
idempotently, and within a daily outbound transfer limit that resets at midnight West Africa Time
(WAT, UTC+1) no matter where the service is hosted.

## Running it

```bash
cd NovaPay.Api
dotnet run
```

Swagger UI is at `https://localhost:<port>/swagger` in Development. Every endpoint requires:

```
Authorization: Bearer novapay-dev-token
```

(configurable via `NovaPay:BearerToken` in `appsettings.json`). Data lives in a local SQLite file,
`novapay.db`, created automatically on first run — no external database needed.

Run the test suite (20 integration + unit tests, exercising real HTTP round trips against the full
pipeline — auth, controllers, EF Core, SQLite):

```bash
dotnet test
```

## Endpoints

| Method | Path | Description |
|---|---|---|
| POST | `/wallets` | Create a wallet (`{ "ownerName": "..." }`), starts at zero balance |
| GET | `/wallets/{id}` | Current balance, in kobo |
| POST | `/wallets/{id}/credit` | Deposit funds (`{ "amountKobo": 100000 }`) |
| POST | `/transfers` | Move funds between two wallets; requires `Idempotency-Key` header |

All amounts are **kobo** (`long`), never floating point — 1 Naira = 100 kobo — to avoid the rounding
drift that `decimal`/`double` balances eventually accumulate over millions of transactions.

## Design decisions & why

**Money is `long` kobo everywhere.** No floats reach a balance calculation.

**Idempotency-Key is required, not optional, on `/transfers`.** In a real deployment, transfer
requests travel over NIBSS NIP rails and the USSD (`*894#`) channel, both of which are prone to
timeouts and client-side retries. Without a dedupe key, a retried request after a timeout could
double-debit a customer. The key is required (`400` if missing) and:
- A repeat call with the **same key + same payload** replays the original result — the transfer is
  looked up and returned, money is not moved again.
- The **same key with a different payload** is rejected with `409 Conflict` — this catches a buggy
  or malicious client reusing a key across unrelated transfers.
- Only *successful* transfers are persisted against a key. A failed attempt (insufficient funds,
  daily limit, unknown wallet) moves no money, so there's nothing to deduplicate — the client can
  retry the same key once conditions change (e.g., after the wallet is topped up).

**Balance can't go negative — enforced under concurrency, not just in the happy path.** SQLite has
no row-level locking, so a naive read-check-write over EF Core can race: two concurrent transfers
could both read the same starting balance, both pass the "sufficient funds" check, and both debit,
overdrawing the wallet. `Common/WalletLock.cs` serializes balance mutations per wallet id (in a
fixed, Guid-ordered sequence when a transfer touches two wallets, so opposite-direction transfers
between the same pair can never deadlock), and the whole read-check-write executes inside a database
transaction for durability. A `ConcurrentTransfers_NeverOverdrawTheWallet` test fires 20 concurrent
transfer requests against a wallet that can only afford 5, and asserts exactly 5 succeed and the
final balance is exactly zero — never negative. This lock is process-local; a horizontally-scaled,
multi-instance deployment would move this guarantee into the database itself via
`SELECT ... FOR UPDATE` row locking on Postgres/SQL Server, which is a drop-in replacement for
`WalletLock` at the same call sites.

**Daily transfer limit resets at WAT midnight regardless of server timezone.** `Common/WatClock.cs`
hardcodes the UTC+1 offset rather than relying on `TimeZoneInfo` and the host OS's timezone
database — Windows and Linux ship different tz identifiers for Lagos, and WAT itself never observes
daylight saving, so a fixed offset is both simpler and more correct than a `TimeZoneInfo` lookup.
The limit defaults to ₦500,000/day (`NovaPay:DailyTransferLimitKobo`) and is checked against the sum
of a wallet's completed outbound transfers within the current WAT calendar day.

**Bearer auth is a single hardcoded token**, per the brief. It's wired through ASP.NET Core's real
`AuthenticationHandler`/`[Authorize]` pipeline (`Auth/StaticBearerTokenAuthHandler.cs`) rather than
ad-hoc middleware, so swapping in real JWT/OAuth2 later (backed by BVN/NIN-verified customer
identity, as CBN licensing would require) is a one-file change with no controller edits.

**Errors are RFC 7807 `ProblemDetails`**, mapped from a small domain exception hierarchy
(`Common/Exceptions.cs`) by `Middleware/ExceptionHandlingMiddleware.cs`: `404` for an unknown
wallet, `422` for insufficient funds or a breached daily limit, `409` for an idempotency key reused
with a different payload, `400` for malformed input.

## Regulatory context this design leans on

- **Amounts in kobo**, per CBN's expectation of exact, auditable ledger arithmetic for a licensed
  payment institution.
- **A firm daily outbound limit** reflects CBN consumer-protection tiering (mass-market wallets are
  capped well below what a fully KYC'd Tier 3 account can move) — `Wallet.DailyTransferLimitKoboOverride`
  leaves room for per-wallet KYC-tier limits without changing the enforcement path.
- **Idempotent transfers** matter specifically because of the NIP/USSD retry behavior described
  above — a duplicate debit is a CBN consumer-protection complaint waiting to happen.
- **NDPA 2023**: this sample stores only `OwnerName` on a wallet — no BVN/NIN, phone number, or
  other personal data — deliberately, since a take-home sample is not the place to model real KYC
  data handling, retention, or consent. A production NovaWallet would need those fields encrypted at
  rest with documented retention/consent handling before they ever entered a table like this.

## What's intentionally out of scope

- KYC tiering, BVN/NIN verification, and the alternative-credit-scoring pieces of NovaLend — this
  service is scoped to NovaWallet's core ledger primitives only, per the brief's four endpoints.
- Multi-instance horizontal scaling (see the `WalletLock` note above) — this is a single-process
  service backed by a local SQLite file, appropriate for the scope of this exercise.
- EF Core migrations: the schema is created via `EnsureCreated()` for zero-setup startup. A
  production service would use versioned migrations (`dotnet ef migrations add ...`) instead.
