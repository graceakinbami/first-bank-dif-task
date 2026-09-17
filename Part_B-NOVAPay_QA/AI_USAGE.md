# AI Usage — NovaPay API Test Engineering Challenge

## PART A — .NET API Implementation

### Purpose

AI was used to accelerate the development of the minimal NovaPay Transfer API using .NET / ASP.NET Core.

The API was intentionally kept lightweight because Part A exists primarily to provide a system that can be tested in Part B.

AI was used to help with:

- Initial ASP.NET Core API scaffolding.
- Designing the wallet and transfer models.
- Creating API controllers and endpoints.
- Implementing wallet creation and credit operations.
- Implementing transfer logic.
- Implementing integer-kobo money handling.
- Implementing idempotency handling.
- Implementing bearer-token authentication.
- Implementing the daily outbound transfer limit.
- Reviewing the implementation against the assessment requirements.

The generated code was reviewed and adapted before use.

### AI Prompt 1 — .NET API Scaffolding

#### Prompt

> "Create a minimal ASP.NET Core Web API in .NET for a fictional fintech application called NovaPay. The API should have POST /wallets to create a wallet, POST /wallets/{id}/credit to credit a wallet, POST /transfers to transfer money between wallets, and GET /wallets/{id} to retrieve a wallet. Use C# and keep the implementation simple because this API will be used for an API testing assessment."

#### What Came Back

The AI generated a basic ASP.NET Core Web API structure containing:

- Controllers.
- Request/response models.
- Wallet and transfer models.
- API endpoints.
- Basic in-memory data storage.
- HTTP status-code handling.

#### How I Used It

I used the generated structure as a starting point and adapted it to the NovaPay assessment.

The final implementation exposed the required endpoints:

- POST /wallets
- POST /wallets/{id}/credit
- POST /transfers
- GET /wallets/{id}

I also adapted the request and response structures to match the expected NovaPay API contract.

### AI Prompt 2 — Integer Kobo and Transfer Logic

#### Prompt

> "Implement the NovaPay transfer logic in C#. Money must be represented as an integer number of kobo. A transfer should move money from one wallet to another, must not allow the sender's balance to become negative, and should return HTTP 201 when successful. Show how to validate insufficient funds and avoid floating-point currency calculations."

#### What Came Back

The AI recommended representing monetary values as integers rather than floating-point numbers.

For example:

```csharp
public long BalanceKobo { get; set; }
```

and:

```csharp
public long AmountKobo { get; set; }
```

It also suggested checking that the sender has sufficient funds before completing the transfer.

#### How I Used It

I used integer kobo throughout the implementation.

For example:

- ₦1 = 100 kobo
- ₦10 = 1,000 kobo
- ₦500,000 = 50,000,000 kobo

This allowed the API and the automated tests to avoid floating-point currency calculations.

I also included validation to prevent transfers where:

```
amountKobo > sender.balanceKobo
```

from resulting in a successful transfer.

### AI Prompt 3 — Idempotency and Daily Transfer Limit

#### Prompt

> "For a .NET fintech Transfer API, implement a simple idempotency mechanism using an Idempotency-Key HTTP header. Replaying the same key with the same request should return the original transaction result instead of creating another transfer. Also implement a daily outbound transfer limit of 50,000,000 kobo that resets at midnight WAT (UTC+1). Keep the implementation suitable for a small assessment project."

#### What Came Back

The AI suggested:

- Reading the `Idempotency-Key` header from the transfer request.
- Storing previously processed keys.
- Returning the previously created transfer for a repeated key.
- Tracking the amount transferred by a wallet during the current day.
- Using a WAT/UTC conversion when determining the daily reset.

#### How I Used It

I used the suggested approach as a starting point and reviewed it against the assessment requirements.

The transfer API accepts:

- `Idempotency-Key`

and the daily outbound limit was represented as:

- 50,000,000 kobo

I also identified the timezone requirement as an important area requiring explicit testing because the reset must occur at midnight WAT rather than simply at midnight according to the server's local timezone.

### AI Review and Validation

The AI-generated implementation was not accepted without review.

I checked the implementation against the Part A requirements:

| Requirement | Status |
|-------------|--------|
| Create wallet | Verified |
| Starting balance is zero | Verified |
| Credit wallet | Verified |
| Transfer between wallets | Verified |
| Integer kobo | Verified |
| Idempotency-Key | Reviewed/tested |
| Atomic transfer | Reviewed/tested |
| Negative balance prevention | Reviewed/tested |
| Daily transfer limit | Reviewed/tested |
| WAT midnight reset | Identified as requiring specific boundary testing |
| Bearer authentication | Implemented/tested |
| Get wallet balance | Verified |

---

## PART B — API Test Automation

### Purpose

AI tools were used as an engineering productivity aid during the NovaPay API test exercise.

The AI was used to accelerate:

- Test design.
- Postman JavaScript scripting.
- Test-data handling.
- Dynamic variable management.
- API assertions.
- Test documentation.

All generated suggestions were reviewed and adapted to the requirements of the assessment before being included in the test suite.

AI-generated output was treated as a starting point rather than as a source of truth.

### Tools Used

#### ChatGPT

ChatGPT was used for:

- Designing the risk-based API test approach.
- Identifying happy-path, boundary, negative, security, idempotency, concurrency, and daily-limit scenarios.
- Creating Postman JavaScript assertions.
- Creating dynamic test data using Postman variables.
- Saving wallet IDs and balances between requests.
- Developing the structure of the automated Postman collection.
- Supporting the design of concurrency and daily-limit tests.
- Structuring bug reports and test documentation.

#### Postman

Postman was used for:

- Sending API requests.
- Managing environments and collection variables.
- Automating API assertions using JavaScript.
- Running the API test suite through the Collection Runner.
- Capturing request and response evidence.
- Testing idempotency scenarios.
- Testing authentication scenarios.
- Testing wallet creation, credit, transfer, and balance retrieval.

### Concrete AI Prompts and Results

#### Prompt 1 — API Automation Design

##### Prompt

> "I have a fictional Nigerian fintech Transfer API with Create Wallet, Credit Wallet, Transfer and Get Wallet endpoints. The Transfer API uses integer kobo, has a ₦500,000 daily outbound limit, requires idempotency, must be atomic and must not allow negative balances. Help me design a Postman API automation suite covering functional, boundary, negative, security, idempotency and concurrency tests."

##### What Came Back

The AI suggested organizing the collection into:

- Functional tests.
- Daily-limit tests.
- Idempotency tests.
- Security tests.
- Concurrency tests.

It also highlighted the importance of testing the ₦500,000 limit as:

- ₦500,000 = 50,000,000 kobo

and testing the concurrency behavior of two transfers originating from the same wallet.

##### How I Used It

I adapted the suggested structure to the actual NovaPay API endpoints and created a sequential Postman collection.

The generated test ideas were reviewed against the assessment requirements before implementation.

#### Prompt 2 — Dynamic Postman Test Data

##### Prompt

> "How can I create two wallets dynamically in Postman, save their generated IDs from the response, and use those IDs in subsequent credit and transfer requests without hardcoding wallet IDs?"

##### What Came Back

The AI suggested extracting the wallet ID from the JSON response and storing it as a Postman environment variable.

Example:

```javascript
const response = pm.response.json();
pm.environment.set("walletA", response.id);
```

It also suggested using dynamic values such as timestamps and UUIDs for wallet names and idempotency keys.

##### How I Used It

I implemented dynamic wallet creation so that the tests do not depend on previously created wallet IDs.

Subsequent requests reference variables such as:

- `{{walletA}}`
- `{{walletB}}`

This makes the collection more repeatable and suitable for unattended execution.

### Example of an AI-Suggested Test That Was Incomplete

#### Currency Precision

An early AI-generated test approach treated the transfer amount as a normal monetary decimal value and suggested scenarios such as:

- ₦10.50
- ₦100.25

This was incomplete for the NovaPay requirements because the assessment explicitly states that money must be represented as integer kobo everywhere.

The API contract uses:

- `amountKobo`

Therefore:

- ₦10.50 = 1,050 kobo

rather than sending a floating-point value such as:

```json
{
  "amount": 10.50
}
```

#### How I Caught and Corrected It

I reviewed the assessment requirements before implementing the generated test cases and identified that the system's financial representation is integer kobo.

I therefore changed the test design to explicitly verify that monetary values are represented as integer kobo values.

For example:

```javascript
const response = pm.response.json();

pm.test("Balance is an integer kobo value", function () {
    pm.expect(Number.isInteger(response.balanceKobo)).to.eql(true);
});
```

I also used integer values such as:

- 1,000 kobo
- 100,000 kobo
- 50,000,000 kobo

rather than relying on floating-point arithmetic.

This demonstrated the need to review AI-generated test suggestions against the specific financial-domain requirements of the application.

#### WAT vs UTC Daily-Limit Consideration

The initial daily-limit test approach focused primarily on the numerical boundary:

- 49,999,999 kobo
- 50,000,000 kobo
- 50,000,001 kobo

This was useful but incomplete.

The assessment specifically requires the daily outbound transfer limit to reset at midnight WAT (UTC+1) regardless of the timezone in which the server is running.

The important time boundary is:

- 23:00 UTC = 00:00 WAT

Therefore:

- 22:59 UTC → 23:59 WAT
- 23:00 UTC → 00:00 WAT

#### How I Caught and Corrected It

I reviewed the requirement and added the WAT-versus-UTC boundary to the test strategy.

The deployed API does not provide a controllable server clock or time-injection mechanism. Therefore, I did not treat a normal date/time test as sufficient evidence of the WAT reset behavior.

The deterministic test would require a test environment with a controllable clock or equivalent time-injection mechanism.

This was identified as a testability/environment consideration rather than assuming the behavior was correct.

### Human Review and Validation

AI-generated suggestions were reviewed against:

- The assessment requirements.
- The actual API request and response contracts.
- Financial-domain requirements.
- Integer-kobo currency handling.
- Idempotency behavior.
- Atomicity and money conservation.
- Daily transfer-limit requirements.
- WAT versus UTC time handling.
- Security and authorization requirements.

The final test suite was therefore not generated blindly by AI.

AI was used to accelerate implementation and generate ideas, while the test scenarios, expected behavior, and final implementation were reviewed against the requirements and observed API behavior.

### Key Lesson

The main lesson from using AI during this exercise was that AI is useful for generating test ideas and implementation patterns, but domain-specific QA judgment is still required.

For a financial API, particular attention is required for:

- Integer currency representation.
- Balance accuracy.
- Money conservation.
- Atomic transactions.
- Concurrent transactions.
- Idempotency.
- Daily cumulative transfer limits.
- Timezone-specific business rules.
- Authentication and authorization.

AI output was therefore treated as a starting point for investigation rather than as evidence that a test was correct.

## Conclusion

AI helped accelerate the development of the NovaPay API and the automated API test suite.

In Part A, it helped with the initial .NET API structure and implementation of the required functionality.

In Part B, it helped with Postman automation, dynamic test data, assertions, and test-scenario design.

The exercise also demonstrated the importance of validating AI-generated suggestions against the actual requirements and the financial domain.

Human review remained responsible for deciding what should be tested, what constituted sufficient evidence, and whether an AI-generated suggestion was complete and appropriate for the NovaPay API.
