# EDI Processor — 837 · TA1 · 999 · 277CA · Multi-Partner Healthcare Claims Operations Platform

EDI Processor is a full-stack **ASP.NET Core 8** application for healthcare X12 claim workflows. It ingests, parses, stores, reconciles, and surfaces operational insight for EDI transactions across multiple trading partners.

It supports API-based uploads, watched-folder ingestion, acknowledgment reconciliation, operational dashboards, and REST APIs so integration teams, claims analysts, and support users can track file- and claim-level outcomes end to end.

---

## Purpose of the product

The main purpose of EDI Processor is to provide a dashboard of processed EDI files so teams can quickly understand what is **accepted, rejected, and pending**, while drilling into operational and claim-level details.

It helps teams:
- Reliably ingest inbound EDI claim and acknowledgment files
- Trace transactions through claim-level outcomes
- Monitor processing through a live folder watcher dashboard
- Access APIs for automation and integrations
- Review uploads, statuses, and operational history through a UI

---

## What this product does

At a high level, EDI Processor performs five core functions:

1. Ingests EDI files through API upload or watched folders
2. Detects transaction type and parses supported X12 payloads
3. Stores transaction, claim, and acknowledgment data in SQL Server or SQLite
4. Links acknowledgments to original claims and transactions, including late-arrival reconciliation
5. Exposes dashboards and APIs for querying status, metrics, and history

---

## Features

| Feature | Detail |
|---|---|
| **Transaction types** | 837P, 837I, 837D, TA1, 999, 277CA |
| **Multi-file upload** | Upload one or many files with per-file processing results |
| **Multi-partner support** | Per-partner sub-folders + ISA06 auto-detection |
| **Folder Watcher** | FileSystemWatcher + polling background service |
| **Acknowledgment linkage** | TA1/999/277CA updates matched transactions and claims |
| **Late reconciliation** | Supports acknowledgments arriving before 837 files |
| **Database** | SQL Server (primary) with SQLite fallback support |
| **Dashboard** | KPIs, status trends, transaction/claim/ack visibility |
| **REST API** | Endpoints for upload, metrics, claims, transactions, acks, watcher ops |
| **UI** | Dashboard and watcher pages with filtering, pagination, light/dark mode |

---

## Supported EDI types

Current processing support includes:
- 837P (Professional claim)
- 837I (Institutional claim)
- 837D (Dental claim)
- TA1 (Interchange acknowledgment)
- 999 (Functional acknowledgment)
- 277CA (Claim acknowledgment)

---

## Key features

### 1) Multi-file EDI upload
- Upload one or many files in a single action
- Per-file processing result in batch responses
- Supported extensions include `.edi`, `.txt`, `.x12`, `.837`, `.999`, `.ta1`, `.dat`, `.277`

### 2) Folder watcher processing
- Watches an inbox path and optional partner subfolders
- Supports FileSystemWatcher near-real-time pickup plus polling fallback
- Routes files by partner folder and ISA06 resolution logic
- Moves successful files into date-based archive folders
- Moves failed files into a failure folder with error artifacts
- Persists processing attempts in file processing logs

### 3) Trading partner management
- CRUD APIs for partner setup
- Partner matching by interchange identifiers
- Default partner fallback support in watcher settings

### 4) Acknowledgment and linkage logic
- TA1 and 999 acknowledgments update matched 837 transaction and claim status
- Retroactive reconciliation supports late-arriving 837 files after acknowledgment files
- 277CA claim-level statuses link to 837 claims using submitter claim ID mapping
- Rejection reasons and error descriptions are persisted for analysis

### 5) Dashboard and operations UI
- Dashboard view of all processed EDI transactions with accepted, rejected, and pending counts
- KPI cards for volume, status trends, and detailed performance metrics
- Transactions, claims, and acknowledgments tables with filters and pagination
- Folder watcher operations page with scan-now, status, and log visibility
- Theme toggle support for light and dark modes

### 6) Metrics and reporting APIs
- Date-range and partner-filtered metrics endpoints
- Claims and transaction query endpoints
- Acknowledgment query endpoints
- Watcher status, scan, inbox, and log endpoints

---

## Operational outcomes this product targets

- Lower manual triage effort for EDI intake
- Faster identification of rejections and pending claims
- Better auditability for file-by-file processing
- Improved resilience to out-of-order acknowledgment events
- Better partner-level visibility for support and operations teams

---

## Ingestion flows

### API upload flow
1. User uploads one or more files with partner ID
2. Type detector identifies the transaction set
3. Parser extracts transaction and record data
4. Service writes data and applies linkage updates
5. API returns per-file success or failure details

### Folder watcher flow
1. Background service scans inbox and partner subfolders
2. File lock and extension checks are applied
3. Trading partner is resolved from folder or content
4. EDI file is processed by the same processing service
5. File is archived or failed, with logs persisted

---

## 837, TA1, 999, and 277CA connectivity behavior

- **837** creates claim records and baseline transaction status
- **TA1** links by interchange control number and updates matched 837 status
- **999** links by transaction set control number and updates matched 837 and claims
- **277CA** links by submitter claim ID to 837 claim number and updates claim lifecycle status
- If acknowledgments arrive before 837, reconciliation logic updates status when the 837 arrives later

---

## Prerequisites

| Tool | Version | Link |
|---|---|---|
| **.NET 8 SDK** | 8.x | https://dotnet.microsoft.com/download/dotnet/8.0 |
| **Git** | 2.x | https://git-scm.com/downloads |
| **Visual Studio 2022** *(optional)* | 17.x Community | https://visualstudio.microsoft.com/vs/community/ |
| **SQL Server Express** *(optional / production)* | 2022 | https://www.microsoft.com/en-us/sql-server/sql-server-downloads |

---

## Quick Start

```bash
# 1. Clone
git clone https://github.com/sandeepkoppolu/EdiProcessor.git
cd EdiProcessor

# 2. Restore packages
dotnet restore

# 3. Build
dotnet build

# 4. Install EF Core CLI tool (once per machine)
dotnet tool install --global dotnet-ef

# 5. Apply database migrations
dotnet ef database update --project src/EdiProcessor.Infrastructure/EdiProcessor.Infrastructure.csproj --startup-project src/EdiProcessor.Web/EdiProcessor.Web.csproj

# 6. Run
dotnet run --project src/EdiProcessor.Web/EdiProcessor.Web.csproj
```

Open your browser:
- **Dashboard** → http://localhost:5000
- **Folder Watcher** → http://localhost:5000/WatcherView
- **Swagger API** → http://localhost:5000/swagger

---

## Configuration

Primary settings are in `src/EdiProcessor.Web/appsettings.json`.

Important sections:
- `ConnectionStrings.DefaultConnection`
- `EdiWatcher.InboxPath`
- `EdiWatcher.ProcessedPath`
- `EdiWatcher.FailedPath`
- `EdiWatcher.PollingIntervalSeconds`
- `EdiWatcher.UseFileSystemWatcher`
- `EdiWatcher.FileLockRetrySeconds`
- `EdiWatcher.DefaultTradingPartnerId`
- `EdiWatcher.Extensions`

Example:

```json
"EdiWatcher": {
  "InboxPath": "edi-inbox",
  "ProcessedPath": "edi-processed",
  "FailedPath": "edi-failed",
  "PollingIntervalSeconds": 30,
  "UseFileSystemWatcher": true,
  "FileLockRetrySeconds": 5,
  "DefaultTradingPartnerId": 1,
  "Extensions": [".edi", ".txt", ".x12", ".837", ".999", ".ta1", ".dat", ".277"]
}
```

---

## Folder Watcher

The watcher starts automatically with the application.

### Inbox layout

```text
edi-inbox/
  ├── BCBS001/
  │   ├── claim.edi
  │   └── batch.x12
  ├── AETNA01/
  │   └── institutional.edi
  └── unknown.edi

edi-processed/
  └── 2024/01/15/
      ├── claim.edi
      └── claim.edi.result.json

edi-failed/
  ├── bad.edi
  └── bad.edi.error.txt
```

### Testing with sample files

```bash
cp sample-edi/BCBS001/*.edi edi-inbox/BCBS001/
cp sample-edi/AETNA01/*.edi edi-inbox/AETNA01/
cp sample-edi/UHC0001/*.edi edi-inbox/UHC0001/
cp sample-edi/UHC0001/*.ta1 edi-inbox/UHC0001/
```

Or use the **Scan Inbox Now** button on the Folder Watcher page.

---

## Switch to SQL Server

Edit `src/EdiProcessor.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=EdiProcessor;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Then re-run migrations:

```bash
dotnet ef database update --project src/EdiProcessor.Infrastructure/EdiProcessor.Infrastructure.csproj --startup-project src/EdiProcessor.Web/EdiProcessor.Web.csproj
```

---

## API summary

Main API groups:
- `/api/edi`
  - `POST /upload`
  - `POST /submit`
- `/api/metrics`
  - `GET /`
- `/api/tradingpartners`
  - `GET`, `GET by id`, `POST`, `PUT`, `DELETE`
- `/api/claims`
  - `GET list with filters`
  - `GET by id`
- `/api/transactions`
  - `GET list with filters`
- `/api/acknowledgments`
  - `GET list with filters`
- `/api/watcher`
  - `GET /status`
  - `POST /scan`
  - `GET /logs`
  - `GET /inbox`

---

## Solution structure

- `EdiProcessor.Core`
  - Domain models
  - Parser logic for EDI transaction types
  - Service interfaces and watcher models
- `EdiProcessor.Infrastructure`
  - EF Core DbContext and migrations
  - Repository and service implementations
  - Background folder watcher service
- `EdiProcessor.Web`
  - ASP.NET Core host
  - MVC views for Dashboard and WatcherView
  - REST APIs for upload, metrics, transactions, claims, acknowledgments, and watcher operations

### Repository layout

```text
EdiProcessor/
├── src/
│   ├── EdiProcessor.Core/
│   ├── EdiProcessor.Infrastructure/
│   └── EdiProcessor.Web/
└── sample-edi/
```

---

## Technology stack

- .NET 8
- ASP.NET Core MVC + Web API
- Entity Framework Core
- SQL Server (primary) with SQLite fallback support
- Background hosted service using FileSystemWatcher and polling
- Razor Views with custom JavaScript front-end behavior

---

## Data model overview

Core entities include:
- TradingPartner
- EdiTransaction
- Claim837
- ServiceLine
- AcknowledgmentRecord
- Claim277CA
- FileProcessingLog

Relationship highlights:
- One trading partner to many transactions
- One 837 transaction to many claims
- One acknowledgment transaction to many acknowledgment records
- One 277CA transaction to many claim-level status records
- Claim-level status records may link back to original 837 transaction records

---

## Current status

The product currently includes:
- Multi-file upload
- Folder watcher ingestion
- 837, TA1, 999, and 277CA processing
- Acknowledgment-to-claim linkage, including retroactive reconciliation
- Dashboard and watcher UI with light and dark mode toggle
- SQL migrations for the current schema

---

## Notes for future enhancements

Potential next improvements:
- Dedicated 277CA analytics and claim status timeline views
- Expanded validation and duplicate prevention policies
- Exportable operational reports
- Role-based access control and authentication
- Additional X12 transaction support beyond the current scope

---

## License

MIT — free to use, modify, and distribute.
