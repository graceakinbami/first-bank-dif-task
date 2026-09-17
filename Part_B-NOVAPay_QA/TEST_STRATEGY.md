# NovaPay API — Test Strategy

## 1. Objective

The objective of this test effort is to provide confidence that the NovaPay Wallet Transfer API is functionally correct, secure against common API-level issues, and reliable when handling financial transactions.

The highest priority is protecting money integrity — ensuring that balances are accurate, transfers are atomic, duplicate requests do not create duplicate transactions, and a wallet cannot be overdrawn.

## 2. Scope

The following APIs are in scope:

- `POST /wallets` — Create wallet.
- `POST /wallets/{id}/credit` — Credit wallet.
- `POST /transfers` — Transfer funds.
- `GET /wallets/{id}` — Retrieve wallet and balance.

The following business requirements are also in scope:

- Integer kobo currency representation.
- Insufficient-funds protection.
- Atomic transfers.
- Daily outbound transfer limit of ₦500,000 (50,000,000 kobo).
- Daily limit reset at midnight WAT (UTC+1).
- Idempotency using `Idempotency-Key`.
- Bearer-token authentication.
- Basic wallet authorization/security checks.
- Concurrent transfers.

## 3. Risk-Based Prioritization

Testing is prioritized according to the potential impact on customers and financial data.

### Priority 1 — Money Integrity

- Transfer accuracy.
- Balance accuracy.
- Insufficient funds.
- Negative balance prevention.
- Concurrency/double-spend.
- Integer-kobo precision.
- Idempotency.

These are highest priority because defects could result in incorrect financial balances or duplicate transactions.

### Priority 2 — Business Rules

- ₦500,000 daily outbound limit.
- Limit boundary values.
- WAT versus UTC midnight reset.

### Priority 3 — Security

- Missing authentication token.
- Invalid authentication token.
- Access to another wallet.
- Invalid wallet IDs.
- Basic input/injection attempts.

### Priority 4 — Functional and Validation

- Wallet creation.
- Wallet credit.
- Wallet retrieval.
- Required fields.
- Invalid amounts.
- Invalid wallet IDs.
- HTTP status codes and response structure.

## 4. Test Approach

### Functional Testing

Verify that each endpoint performs its intended operation and returns the expected HTTP status, response structure, and data.

Also verify that balances and transaction amounts remain integer kobo values.

### Negative Testing

Verify that invalid requests are rejected appropriately, including:

- Insufficient funds.
- Invalid wallet IDs.
- Invalid or missing amounts.
- Missing authentication.
- Invalid authentication.
- Invalid request payloads.

### Idempotency Testing

Verify:

- A transfer with a unique `Idempotency-Key` succeeds.
- Repeating the same key with the same payload does not create another transaction.
- Reusing the same key with a different payload is handled safely according to the API contract.

### Concurrency Testing

Start with a known wallet balance and issue simultaneous transfers from the same wallet.

Verify using the resulting wallet balances that:

- The sender never becomes negative.
- The total balance before and after the operation is consistent.
- The same funds cannot be spent twice.

### Security Testing

Perform a focused API security pass covering:

- Missing token.
- Invalid token.
- Access to another wallet.
- Invalid wallet identifiers.
- Basic injection-style input attempts.

This is a functional security check, not a full penetration test.

### Exploratory Testing

Conduct a time-boxed exploratory session of up to 60 minutes focusing on:

- Unexpected combinations of valid and invalid inputs.
- Boundary conditions.
- State transitions.
- Repeated requests.
- Financial consistency.
- Error handling.
- API response consistency.

All reproducible defects will be documented with severity, priority, reproduction steps, expected result, actual result, and evidence.

## 5. Automation Approach

Postman is used for API automation.

The collection uses dynamic variables for:

- Base URL.
- Authentication token.
- Wallet IDs.
- Wallet names.
- Balances.
- Idempotency keys.

Tests are written using Postman JavaScript assertions.

The collection is organized in execution order so that it can be executed unattended through the Postman Collection Runner.

The intention is for the same collection to be usable locally and in CI with environment-specific configuration.

## 6. Test Data

Test data is generated dynamically where possible to avoid dependencies on hardcoded wallet IDs.

Example:

| Wallet | Creation Method |
|--------|-----------------|
| Wallet A | Dynamically created |
| Wallet B | Dynamically created |

Wallet IDs returned by the API are stored as variables and reused by subsequent requests.

Financial amounts are represented as integer kobo.

## 7. Entry Criteria

Testing can begin when:

- The API environment is available.
- Required endpoints are deployed.
- A valid test authentication token is available.
- API request/response contracts are known.
- The Postman environment is configured.

## 8. Exit Criteria

Testing is considered complete when:

- All planned automated scenarios have been executed.
- Critical money-integrity scenarios have passed or have documented defects.
- Concurrency testing has been performed.
- Idempotency testing has been performed.
- Security checks have been completed.
- Exploratory testing has been completed.
- All reproducible defects have been documented.
- The final collection can be executed successfully as an automated suite.

## 9. Out of Scope

Due to the time-boxed nature of the assessment, the following are outside the primary scope:

- Full penetration testing.
- DDoS testing.
- Production infrastructure testing.
- Database performance testing.
- NIBSS integration testing.
- Real BVN/NIN verification.
- Full CBN/NDPA compliance assessment.
- Large-scale load and endurance testing.

## 10. One More Week — Priorities

With an additional week, I would prioritize:

- Concurrency testing with higher request volumes and repeated runs.
- Add deterministic WAT-midnight testing using a controllable test clock.
- Add load/performance testing using JMeter.
- Add more property-based and data-driven tests.
- Improve test-data management and reporting.
- Investigate and retest all identified defects.

## 11. Definition of Success

The API should demonstrate that valid transactions work as expected while preserving financial integrity under invalid, repeated and concurrent conditions.

The most important evidence is not simply that HTTP requests return successful status codes, but that wallet balances and transaction amounts remain correct before and after each operation.
