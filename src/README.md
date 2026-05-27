# EDI Processor

## Purpose of the product
EDI Processor is a healthcare EDI intake and operations platform for X12 claim workflows.
Its main purpose is to provide a dashboard of all processed EDI files so teams can quickly see how many are accepted, rejected, and pending, while also drilling into detailed operational and claim-level insights.
It also helps teams ingest inbound EDI files, parse and persist claim data, track acknowledgments, monitor operational status, and resolve claim lifecycle visibility gaps when files arrive out of order.

The system is designed for day-to-day operational use by integration teams, claims analysts, and support users who need:
- Reliable ingestion of EDI claim and acknowledgment files
- Traceability from transaction to claim-level outcomes
- Live monitoring through a folder watcher dashboard
- API access for automation and integrations
- A UI for upload, filtering, and review

## What this product does
At a high level, EDI Processor performs five core functions:

1. Ingests EDI files through API upload or watched folders
2. Detects transaction type and parses supported X12 payloads
3. Stores transaction, claim, and acknowledgment data in SQL Server or SQLite
4. Links acknowledgments to original claims and transactions, including late-arrival reconciliation
5. Exposes operational dashboards and APIs for querying status and history

## Supported EDI types
Current processing support includes:
- 837P (Professional claim)
- 837I (Institutional claim)
- 837D (Dental claim)
- TA1 (Interchange acknowledgment)
- 999 (Functional acknowledgment)
- 277CA (Claim acknowledgment)

## Key features

### 1) Multi-file EDI upload
- Upload one or many files in a single action
- Per-file processing result in batch responses
- Supported extensions include .edi, .txt, .x12, .837, .999, .ta1, .dat, .277

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
- Retroactive reconciliation supports late-arriving 837 files after ack files
- 277CA claim-level statuses link to 837 claims using submitter claim id mapping
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

## Solution structure
- EdiProcessor.Core
  - Domain models
  - Parser logic for EDI transaction types
  - Service interfaces and watcher models
- EdiProcessor.Infrastructure
  - EF Core DbContext and migrations
  - Repository and service implementations
  - Background folder watcher service
- EdiProcessor.Web
  - ASP.NET Core host
  - MVC views for Dashboard and WatcherView
  - REST APIs for upload, metrics, transactions, claims, acks, and watcher operations

## Technology stack
- .NET 8
- ASP.NET Core MVC + Web API
- Entity Framework Core
- SQL Server (primary) with SQLite fallback support
- Background hosted service using FileSystemWatcher and polling
- Razor Views with custom JavaScript front-end behavior

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

## Ingestion flows

### API upload flow
1. User uploads one or more files with partner id
2. Type detector identifies transaction set
3. Parser extracts transaction and records
4. Service writes data and applies linkage updates
5. API returns per-file success or failure details

### Folder watcher flow
1. Background service scans inbox and partner subfolders
2. File lock and extension checks are applied
3. Trading partner is resolved from folder or content
4. EDI file is processed by the same processing service
5. File is archived or failed with logs persisted

## 837, TA1, 999, and 277CA connectivity behavior
- 837 creates claim records and baseline transaction status
- TA1 links by interchange control number and updates matched 837 status
- 999 links by transaction set control number and updates matched 837 and claims
- 277CA links by submitter claim id to 837 claim number and updates claim lifecycle status
- If acknowledgments arrive before 837, reconciliation logic updates status when 837 arrives later

## Configuration
Primary settings are in appsettings.json.
Important sections:
- ConnectionStrings.DefaultConnection
- EdiWatcher.InboxPath
- EdiWatcher.ProcessedPath
- EdiWatcher.FailedPath
- EdiWatcher.PollingIntervalSeconds
- EdiWatcher.UseFileSystemWatcher
- EdiWatcher.FileLockRetrySeconds
- EdiWatcher.DefaultTradingPartnerId
- EdiWatcher.Extensions

## Setup and run
Prerequisites:
- .NET SDK 8+
- SQL Server instance (or use SQLite connection string)

Recommended steps:
1. Configure connection string in appsettings.json
2. Ensure required inbox/processed/failed folders exist or are creatable
3. Run database migrations
4. Build and run the web project
5. Open Dashboard and WatcherView for operations

Typical commands:
- dotnet restore
- dotnet build
- dotnet ef database update --project EdiProcessor.Infrastructure/EdiProcessor.Infrastructure.csproj --startup-project EdiProcessor.Web/EdiProcessor.Web.csproj
- dotnet run --project EdiProcessor.Web/EdiProcessor.Web.csproj

## API summary
Main API groups:
- /api/edi
  - POST /upload
  - POST /submit
- /api/metrics
  - GET /
- /api/tradingpartners
  - GET, GET by id, POST, PUT, DELETE
- /api/claims
  - GET list with filters
  - GET by id
- /api/transactions
  - GET list with filters
- /api/acknowledgments
  - GET list with filters
- /api/watcher
  - GET /status
  - POST /scan
  - GET /logs
  - GET /inbox

## Operational outcomes this product targets
- Lower manual triage effort for EDI intake
- Faster identification of rejections and pending claims
- Better auditability for file-by-file processing
- Improved resilience to out-of-order acknowledgment events
- Better partner-level visibility for support and operations teams

## Current status
The product currently includes:
- Multi-file upload
- Folder watcher ingestion
- 837, TA1, 999, and 277CA processing
- Acknowledgment-to-claim linkage, including retroactive reconciliation
- Dashboard and watcher UI with light and dark mode toggle
- SQL migrations for current schema

## Notes for future enhancements
Potential next improvements:
- Dedicated 277CA analytics and claim status timeline views
- Expanded validation and duplicate prevention policies
- Exportable operational reports
- Role-based access control and authentication
- Additional X12 transaction support beyond current scope

## Separate Web and API deployment
The solution is now split into two deployable hosts:
- EDIDashboard.Web: MVC/Razor UI only
- EDIDashboard.Api: REST API, DB access, Swagger, and optional watcher background service

### Local run with fixed ports
Launch profiles are configured as:
- Web: http://localhost:5000 (and https://localhost:7000)
- API: http://localhost:5001 (and https://localhost:7001)

Run API:
- dotnet run --project EDIDashboard.Api/EDIDashboard.Api.csproj

Run Web:
- dotnet run --project EDIDashboard.Web/EDIDashboard.Web.csproj

### Environment config mapping
Web project:
- appsettings.Development.json sets Api.BaseUrl to https://localhost:5001
- appsettings.Production.json should set Api.BaseUrl to your deployed API URL

API project:
- appsettings.Development.json sets CORS origins for localhost web URLs
- appsettings.Production.json should set CORS to your deployed web URL(s)
- EdiWatcher.Enabled controls whether the background watcher runs in the API host

### Publish and deploy separately
Publish API:
- dotnet publish EDIDashboard.Api/EDIDashboard.Api.csproj -c Release -o out/api

Publish Web:
- dotnet publish EDIDashboard.Web/EDIDashboard.Web.csproj -c Release -o out/web

Deploy `out/api` and `out/web` to separate sites/apps (IIS, App Service, or containers).

### IIS example topology
- dashboard.yourdomain.com -> EDIDashboard.Web
- api.yourdomain.com -> EDIDashboard.Api

For IIS or App Service production, make sure these are set correctly:
1. Web: Api.BaseUrl points to https://api.yourdomain.com
2. API: Cors.AllowedOrigins includes https://dashboard.yourdomain.com
3. ConnectionStrings in API point to production database
4. EdiWatcher.Enabled is true in only one host that should process files

### Database migrations
Run migrations using the API startup project:
- dotnet ef database update --project EDIDashboard.Infrastructure/EDIDashboard.Infrastructure.csproj --startup-project EDIDashboard.Api/EDIDashboard.Api.csproj --context EdiDbContext
