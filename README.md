# Transactions Ingest

A .NET 10 Console application that ingests card transaction snapshots, reconciles changes, and maintains a full audit trail. Built with Entity Framework Core (code-first) and SQLite.

## Prerequisites

- .NET 10 SDK

## Build

```bash
dotnet build
```

## Run

```bash
dotnet run --project src/TransactionsIngest
```

By default, the app reads from the mock JSON feed at `src/TransactionsIngest/mock-data/transactions.json`. To switch between mock data files or point at a real API, edit `src/TransactionsIngest/appsettings.json`:

```json
{
  "Ingestion": {
    "ApiBaseUrl": "https://api.example.com",
    "ApiPath": "/api/transactions/last24h",
    "UseMockFeed": true,
    "MockFeedPath": "mock-data/transactions.json",
    "ConnectionString": "Data Source=transactions.db",
    "LookBackHours": 24
  }
}
```


| Setting                  | Description                                                         |
| ------------------------ | ------------------------------------------------------------------- |
| `UseMockFeed`            | `true` to read from a local JSON file; `false` to call the HTTP API |
| `MockFeedPath`           | Path to the mock JSON file (relative to the output directory)       |
| `ApiBaseUrl` / `ApiPath` | The transaction API endpoint used when `UseMockFeed` is `false`     |
| `ConnectionString`       | SQLite connection string                                            |
| `LookBackHours`          | The rolling window size (default 24 hours)                          |


## Test

```bash
dotnet test
```

30 automated tests covering:

- **Insert** — new transactions are persisted with hashed card numbers and audit entries
- **Update** — field-level change detection (amount, product name, location, card number, timestamp) with audit recording
- **Revocation** — transactions absent from the snapshot within the 24-hour window are marked revoked; reappearing transactions are reactivated
- **Finalization** — transactions older than 24 hours are finalized and protected from further changes
- **Idempotency** — repeated runs with identical data produce no duplicates or spurious audit entries

## Approach

The application is designed as a single-run job meant to be executed once per hour by an external scheduler. Each run:

1. **Fetches** the latest 24-hour transaction snapshot (from a mock JSON file or HTTP API).
2. **Upserts** each transaction by `TransactionId` — inserts new records and detects field-level changes on existing ones.
3. **Revokes** any active transactions within the 24-hour window that are no longer present in the snapshot.
4. **Finalizes** active transactions whose `TransactionTime` has fallen outside the 24-hour window, preventing further modifications.
5. **Commits** all changes atomically within a single database transaction to ensure idempotency.

Every state change (created, updated, revoked, finalized) is recorded in an `Auditlogs` table with the field name, old value, and new value where applicable.

### Key design decisions

- `IClock` **abstraction**: Allows tests to control time, which is critical for testing finalization and revocation logic without relying on real clock values.
- `ITransactionFetcher` **interface**: Abstracts the data source so the same ingestion logic works against both the mock JSON feed and a real HTTP API. Tests use a simple stub implementation.
- **Card number privacy** — Raw card numbers are never stored. The database holds only a SHA-256 hash (for comparison) and the last 4 digits (for display).
- **Intermediate** `SaveChangesAsync`:After upserting, changes are flushed to the database before running revocation and finalization queries. This ensures newly inserted records are visible to those queries while still being wrapped in the same database transaction.
- `DatabaseGeneratedOption.None` **on** `TransactionId`:The transaction ID is an external identifier from the API, not an auto-increment value.

## Assumptions

- The transaction API returns a complete snapshot of the last 24 hours on each call (not a delta).
- `TransactionId` is a stable, unique identifier provided by the upstream system.
- The application is the only writer to the SQLite database (no concurrent access concerns).
- Finalization is a one-way operation, once finalized, a transaction is permanently locked.

## Project Structure

```
src/TransactionsIngest/
├── Configuration/          # IngestionSettings (bound from appsettings.json)
├── Data/                   # EF Core DbContext
├── Models/                 # Transaction, TransactionDto, Auditlog entities
├── Services/               # IngestionService, fetchers, CardNumberHelper, IClock
├── mock-data/              # Sample JSON feeds for local testing
├── appsettings.json
└── Program.cs              # Entry point with DI setup
```

