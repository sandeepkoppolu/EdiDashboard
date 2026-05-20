# EDI Processor — 837 · TA1 · 999 · Multi-Partner Healthcare Clearinghouse

A full-stack **ASP.NET Core 8** application that ingests, parses, stores, and
surfaces metrics for HIPAA X12 EDI healthcare claim transactions.  
Supports multiple trading partners, a real-time dashboard, a REST API, and an
**automatic folder watcher** that picks up EDI files from disk.

---

## Features

| Feature | Detail |
|---|---|
| **Transaction types** | 837P, 837I, 837D, TA1, 999 |
| **Multi-partner** | Per-partner sub-folders + ISA06 auto-detection |
| **Folder Watcher** | FileSystemWatcher + polling background service |
| **Database** | SQLite (default, zero config) or SQL Server Express |
| **Dashboard** | KPIs, daily volume chart, partner breakdown, claim table |
| **REST API** | Full OpenAPI/Swagger UI at `/swagger` |
| **Claim status flow** | 837 → TA1/999 automatically cascades Accepted/Rejected |

---

## Prerequisites

| Tool | Version | Link |
|---|---|---|
| **.NET 8 SDK** | 8.x | https://dotnet.microsoft.com/download/dotnet/8.0 |
| **Git** | 2.x | https://git-scm.com/downloads |
| **Visual Studio 2022** *(optional)* | 17.x Community | https://visualstudio.microsoft.com/vs/community/ |
| **SQL Server Express** *(optional, prod)* | 2022 | https://www.microsoft.com/en-us/sql-server/sql-server-downloads |

---

## Quick Start (6 commands)

```bash
# 1. Clone
git clone https://github.com/your-org/EdiProcessor.git
cd EdiProcessor

# 2. Restore NuGet packages
dotnet restore

# 3. Build
dotnet build

# 4. Install EF Core CLI tool (once per machine)
dotnet tool install --global dotnet-ef

# 5. Apply database migrations
cd src/EdiProcessor.Infrastructure
dotnet ef database update --startup-project ../EdiProcessor.Web

# 6. Run
cd ../EdiProcessor.Web
dotnet run
```

Open your browser:
- **Dashboard** → http://localhost:5000  
- **Folder Watcher** → http://localhost:5000/WatcherView  
- **Swagger API** → http://localhost:5000/swagger  

---

## Folder Watcher

The watcher starts automatically with the application.

### Inbox Layout

```
edi-inbox/
  ├── BCBS001/          ← sub-folder = trading partner ISA06
  │     ├── claim.edi
  │     └── batch.x12
  ├── AETNA01/
  │     └── institutional.edi
  └── unknown.edi       ← root files: ISA06 auto-matched from content

edi-processed/
  └── 2024/01/15/
        ├── claim.edi
        └── claim.edi.result.json   ← processing result

edi-failed/
  ├── bad.edi
  └── bad.edi.error.txt             ← exception details
```

### Configuration (`appsettings.json`)

```json
"EdiWatcher": {
  "InboxPath":               "edi-inbox",
  "ProcessedPath":           "edi-processed",
  "FailedPath":              "edi-failed",
  "PollingIntervalSeconds":  30,
  "UseFileSystemWatcher":    true,
  "FileLockRetrySeconds":    5,
  "DefaultTradingPartnerId": 1,
  "Extensions": [".edi",".txt",".x12",".837",".999",".ta1"]
}
```

### Testing with Sample Files

```bash
# Copy sample files into the inbox to trigger processing
cp sample-edi/BCBS001/*.edi   edi-inbox/BCBS001/
cp sample-edi/AETNA01/*.edi   edi-inbox/AETNA01/
cp sample-edi/UHC0001/*.edi   edi-inbox/UHC0001/
cp sample-edi/UHC0001/*.ta1   edi-inbox/UHC0001/
```

Or use the **"Scan Inbox Now"** button on the Folder Watcher page.

---

## Switch to SQL Server (Production)

Edit `src/EdiProcessor.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=EdiProcessor;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Then re-run migrations:

```bash
cd src/EdiProcessor.Infrastructure
dotnet ef database update --startup-project ../EdiProcessor.Web
```

---

## REST API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/edi/upload` | Upload EDI file (multipart) |
| `POST` | `/api/edi/submit` | Submit raw EDI text (JSON) |
| `GET`  | `/api/metrics` | Dashboard metrics |
| `GET`  | `/api/claims` | Paginated claims |
| `GET`  | `/api/transactions` | Paginated transactions |
| `GET`  | `/api/acknowledgments` | TA1 + 999 records |
| `GET`  | `/api/watcher/status` | Watcher status + recent logs |
| `POST` | `/api/watcher/scan` | Trigger immediate inbox scan |
| `GET`  | `/api/watcher/logs` | File processing log |
| `GET/POST/PUT/DELETE` | `/api/tradingpartners` | CRUD trading partners |

---

## Project Structure

```
EdiProcessor/
├── src/
│   ├── EdiProcessor.Core/            # Domain models, parsers, interfaces
│   │   ├── Models/
│   │   │   ├── Models.cs             # Claims, transactions, acknowledgments
│   │   │   └── WatcherModels.cs      # Watcher config, file processing log
│   │   ├── Parsers/
│   │   │   ├── Edi837Parser.cs       # 837P/I/D parser
│   │   │   └── AcknowledgmentParsers.cs  # TA1 + 999 parsers
│   │   └── Services/
│   │       ├── Interfaces.cs         # Core service interfaces
│   │       └── WatcherInterfaces.cs  # Watcher service interfaces
│   │
│   ├── EdiProcessor.Infrastructure/  # EF Core, migrations, service impls
│   │   ├── Data/
│   │   │   └── EdiDbContext.cs
│   │   ├── Migrations/
│   │   ├── Repositories/
│   │   │   ├── Services.cs           # EDI, metrics, partner services
│   │   │   └── WatcherRepository.cs  # File log repo, watcher status
│   │   └── Services/
│   │       └── EdiFolderWatcherBackgroundService.cs
│   │
│   └── EdiProcessor.Web/             # ASP.NET Core MVC + REST API
│       ├── Controllers/
│       │   ├── Controllers.cs        # All main API controllers
│       │   └── WatcherController.cs  # Watcher API + MVC controller
│       ├── Views/
│       │   ├── Dashboard/Index.cshtml
│       │   └── WatcherView/Index.cshtml
│       ├── Program.cs
│       └── appsettings.json
│
└── sample-edi/                       # Ready-to-use test EDI files
    ├── BCBS001/claim_professional_sample.edi
    ├── BCBS001/999_functional_ack_sample.999
    ├── AETNA01/claim_institutional_sample.edi
    └── UHC0001/claim_dental_sample.edi
        UHC0001/ta1_acknowledgment_sample.ta1
```

---

## NuGet Packages Used

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | 8.0.0 | ORM |
| `Microsoft.EntityFrameworkCore.Sqlite` | 8.0.0 | SQLite provider (dev) |
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.0.0 | SQL Server provider (prod) |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.0 | EF CLI migrations |
| `Swashbuckle.AspNetCore` | 6.5.0 | Swagger/OpenAPI UI |

All packages are **open-source and free**. No paid dependencies.

---

## License

MIT — free to use, modify, and distribute.
