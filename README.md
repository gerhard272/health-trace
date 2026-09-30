# HealthTrace

A cloud-based web app for keeping a **personal symptom diary** and exporting it as a **PDF** — for example to hand to your doctor.

Users sign up, log their symptoms (event, description, date and time) and request a PDF export of the whole diary, a date range or a hand-picked selection of entries. The PDF is generated in the background by an **Azure Function** and stored in **Azure Blob Storage**; the data lives in an **Azure SQL Database**. All three run in a dedicated Azure resource group.

> Team portfolio project (see [Credits](#credits)). The backend is .NET 10 (ASP.NET Core Web API + Azure Functions isolated worker), the frontend is Angular 22 with Tailwind CSS 4.

## Features

- Registration and login (HTTP Basic auth, BCrypt-hashed passwords)
- Symptom diary: create, read, update, soft-delete; search by date or by (partial) event name
- Asynchronous PDF export: all symptoms, a date range, or selected entries
- Export history with live status (`Pending` → `Processing` → `Completed` / `Failed`) and download
- Per-user data isolation: every query is scoped to the authenticated user (other users' data returns 404)
- Consistent error responses via RFC 7807 `ProblemDetails`, structured logging with Serilog

## Architecture

```
 HealthTrace.Web (Angular)
          │  HTTPS + Basic Auth
          ▼
 HealthTrace.PL.API ─────────────► Azure Queue Storage "export-requests"
          │                                   │  QueueTrigger
          ▼                                   ▼
 HealthTrace.BLL  ◄──────────────── HealthTrace.Functions (Azure Function App)
          │                          reuses BLL and DAL
          ▼
 HealthTrace.DAL ──► Azure SQL Database
                 └─► Azure Blob Storage "exports"
```

### Azure resources

Everything the app needs in the cloud lives in a single resource group:

| Resource | Used for |
| --- | --- |
| **Azure SQL Database** | Users, symptoms and export requests (EF Core, code-first migrations) |
| **Storage account** | Blob container `exports` for the generated PDFs, queue `export-requests` for export jobs, and the Functions host storage (`AzureWebJobsStorage`) |
| **Function App** | Runs `ProcessExportFunction` (.NET 10 isolated worker), deployed via Zip Deploy |
| **Application Insights** | Telemetry and logs of the Function App (OpenTelemetry exporter) |

### Projects

| Project | Role |
| --- | --- |
| `HealthTrace.PL.API` | ASP.NET Core Web API: controllers, Basic authentication, global exception handling, Serilog logging, Scalar API docs |
| `HealthTrace.BLL` | Services, DTOs, FluentValidation, AutoMapper, BCrypt, PDF generation with QuestPDF, queue dispatcher |
| `HealthTrace.DAL` | Entities, EF Core `HealthTraceDbContext` (SQL Server), generic repository + Unit of Work, Blob Storage access |
| `HealthTrace.Functions` | Azure Function with a QueueTrigger that generates the PDFs |
| `HealthTrace.Web` | Angular 22 + Tailwind CSS 4 frontend: login/registration, diary, exports |
| `HealthTrace.Test` | xUnit + Moq unit tests |

Dependencies flow one way: PL → BLL → DAL. The Function references BLL and DAL directly, so export logic is written once and shared by the API and the Function.

## Getting started

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (LTS) and npm
- SQL Server: Azure SQL, or LocalDB / a local SQL Server for development
- Azure Storage (Blob + Queue), or [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) locally
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) to run the Function locally
- `dotnet-ef` for migrations: `dotnet tool install --global dotnet-ef`

### Configuration

No connection strings are committed. The API and the Function must point to **the same database and the same storage account**.

| Key | API (User Secrets) | Function (`local.settings.json`) | Purpose |
| --- | --- | --- | --- |
| `ConnectionStrings:HealthTraceDb` | ✓ | ✓ | Database |
| `BlobStorage:ConnectionString` | ✓ | ✓ | PDFs in Blob Storage; in the API also used to enqueue export jobs |
| `BlobStorage:DefaultContainerName` | ✓ (default `exports`) | ✓ | PDF container |
| `AzureWebJobsStorage` | | ✓ | Storage the QueueTrigger reads `export-requests` from |

API:

```bash
cd HealthTrace.PL.API
dotnet user-secrets set "ConnectionStrings:HealthTraceDb" "<connection string>"
dotnet user-secrets set "BlobStorage:ConnectionString" "<storage connection string>"
```

Function: create `HealthTrace.Functions/local.settings.json` (git-ignored):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<storage connection string>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "BlobStorage:ConnectionString": "<storage connection string>",
    "BlobStorage:DefaultContainerName": "exports"
  },
  "ConnectionStrings": {
    "HealthTraceDb": "<connection string>"
  }
}
```

Locally you can use `UseDevelopmentStorage=true` (Azurite) instead of real storage connection strings. When using Azure SQL, your machine's IP must be allowed in the SQL server firewall rules.

In Azure, the same keys are set as **Application settings** / **Connection strings** of the Function App.

### Run locally

1. **Database** — apply the migrations:

   ```bash
   dotnet ef database update --project HealthTrace.DAL --startup-project HealthTrace.PL.API
   ```

2. **API** — https://localhost:7133, interactive docs at https://localhost:7133/api-docs (Scalar, Development only):

   ```bash
   dotnet run --project HealthTrace.PL.API --launch-profile https
   ```

3. **Function** — required for exports to move from `Pending` to `Completed` (skip it if you point the API at the Function deployed in Azure, since both read the same queue):

   ```bash
   cd HealthTrace.Functions
   func start
   ```

4. **Frontend** — http://localhost:4200 (the API already allows this CORS origin; the API URL is in `src/environments/`):

   ```bash
   cd HealthTrace.Web
   npm ci
   npx ng serve
   ```

In Visual Studio you can start the API and the Function together with multiple startup projects. With *Just My Code* enabled, the debugger breaks on domain exceptions thrown by controllers even though the global handler catches them: untick `HealthTrace.BLL.Exceptions.AppException` in *Exception Settings*, or run without debugging (Ctrl+F5).

### Deploy the Function

The Function App is published with **Zip Deploy** (Visual Studio → *Publish*, profile `func-health-trace - Zip Deploy`). Alternatively, with Core Tools:

```bash
cd HealthTrace.Functions
func azure functionapp publish <function-app-name>
```

Make sure the Function App settings contain `ConnectionStrings:HealthTraceDb`, `BlobStorage:ConnectionString` and `BlobStorage:DefaultContainerName`, and that the SQL server firewall allows access from Azure services.

## Tests

```bash
dotnet test HealthTrace.Test
```

Covers services (User, Symptom, Export, generic), controllers, validation, mapping, password hashing, exceptions, `ExceptionStatusMapper` and logging.

```bash
cd HealthTrace.Web
npx ng test --watch=false
```

Covers Angular services, guards, interceptors and pages.

## API

Every endpoint except `auth` requires `Authorization: Basic <base64(username:password)>`. Each query is scoped to the authenticated user: a user never sees another user's data (404).

| Method | Endpoint | Description |
| --- | --- | --- |
| POST | `/api/auth/register` | Register (201) |
| POST | `/api/auth/login` | Check credentials, returns the profile |
| GET | `/api/symptom` | List; filter by `?date=yyyy-MM-dd` **or** `?name=` (partial match), not both |
| GET | `/api/symptom/{id}` | Details |
| POST | `/api/symptom` | Create (201) |
| PUT | `/api/symptom/{id}` | Update (204); the route id must match the body id |
| DELETE | `/api/symptom/{id}` | Soft delete (204) |
| POST | `/api/exports/request` | Request an export: `{}` = everything, `symptomIds`, or `fromDate`/`toDate`. Returns 202 |
| GET | `/api/exports` | Export history |
| GET | `/api/exports/{id}` | Status: `Pending`, `Processing`, `Completed`, `Failed` |
| GET | `/api/exports/{id}/download` | The PDF if `Completed`, otherwise 409 |

### Export flow

1. `POST /api/exports/request` stores an `ExportRequest` with status `Pending`.
2. `QueueExportJobDispatcher` puts the id on the `export-requests` queue (Base64-encoded, as the QueueTrigger expects) and the API immediately returns 202.
3. `ProcessExportFunction` sets `Processing`, builds the PDF with QuestPDF and uploads it to Blob Storage as `{userId}/{guid}.pdf`.
4. The status becomes `Completed` (with `BlobName` and `FileName`) or `Failed` (with `ErrorMessage`).
5. The client polls `GET /api/exports/{id}` and downloads the file from `/download`.

The Function is idempotent: a redelivered message for a request that is already `Processing` or `Completed` is ignored. `InlineExportJobDispatcher` (PDF generated within the same HTTP request) is still available — just change the `IExportJobDispatcher` registration in the API's `Program.cs`.

## Error handling

Controllers and services never build error responses themselves: they throw domain exceptions (`HealthTrace.BLL.Exceptions`) that `GlobalExceptionHandler` turns into [ProblemDetails](https://www.rfc-editor.org/rfc/rfc7807) via `ExceptionStatusMapper`.

| Exception | Status |
| --- | --- |
| `ValidationException` | 400 (`ValidationProblemDetails` with per-field errors) |
| `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `NotFoundException` | 404 (`resourceName`, `resourceKey` extensions) |
| `ConflictException` | 409 |
| anything else | 500, no details (they stay in the logs) |

## Data and security

- Passwords stored as BCrypt hashes; username and fiscal code are unique.
- All entities inherit from `AuditEntity`: `CreatedAt/By`, `ModifiedAt/By`, `DeletedAt/By` are filled in `SaveChanges` (writes from the Function are attributed to the "system" user, id 0).
- Soft delete: a Delete sets `IsDeleted = true`; `Symptom` and `ExportRequest` have a global query filter.
- PDFs are never exposed through a public blob URL: the container stays private and downloads go through the authenticated API.
- Logs: Serilog to console and, outside Development, to daily-rolling JSON files in `logs/`.
- The frontend keeps Basic credentials in `sessionStorage` and sends them only to the API URL.

## What I learned

The main goal of this project was to get hands-on with **Azure** and build something that is actually cloud-native rather than a local app with a database connection string pointed at the cloud.

- **Designing around managed services.** I provisioned a dedicated resource group with Azure SQL Database, a Storage account and a Function App (plus Application Insights), and learned how the pieces fit together: which service owns which responsibility, how they authenticate to each other, and how to keep everything in one place so it is easy to reason about, monitor and tear down.
- **Asynchronous work with Queue Storage + Azure Functions.** PDF generation is moved out of the HTTP request: the API enqueues a job and returns `202 Accepted`, a QueueTrigger Function does the heavy lifting, and the client polls for status. This is the *asynchronous request-reply* pattern, and it keeps the API responsive no matter how big the export is.
- **At-least-once delivery is real.** Queues can deliver the same message more than once, and failed messages are retried and eventually moved to a poison queue. I made the Function idempotent (it skips requests already `Processing`/`Completed`) and made it drop structurally invalid messages instead of retrying them forever.
- **The small details that break integrations.** The Functions Queue extension expects Base64-encoded messages while `QueueClient` sends plain text by default: the messages were enqueued but never processed until I set `QueueMessageEncoding.Base64`. Debugging this taught me to read the host logs and to check the poison queue first.
- **Blob Storage for generated files.** PDFs are stored in a private container under a per-user path (`{userId}/{guid}.pdf`); the database only keeps the blob name. Downloads are streamed through the API so authorization stays in one place and no SAS or public URL is ever handed out.
- **Azure Functions isolated worker model.** Running on .NET 10 in the isolated worker let me reuse the same BLL/DAL, dependency injection and EF Core setup as the API. I also had to handle the fact that a Function has no `HttpContext`, so audit fields fall back to a "system" user.
- **Azure SQL in practice.** EF Core code-first migrations against Azure SQL, server firewall rules for local development and for Azure services, and keeping the API and the Function on the same database.
- **Configuration and secrets.** No secret is committed: User Secrets for the API locally, `local.settings.json` for the Function locally, and Application settings / Connection strings on the Function App in Azure. Azurite made it possible to develop without touching the cloud resources.
- **Deployment and observability.** Publishing the Function App with Zip Deploy, and using Application Insights (via OpenTelemetry) to follow each export from the queue message to the blob upload — and to find out *why* one failed.
- **Cost awareness.** Choosing a serverless, event-driven component for a bursty workload, and grouping everything in one resource group made it easy to keep an eye on what the project actually costs.

Beyond Azure, the project was also practice in layered architecture (PL/BLL/DAL, repository + Unit of Work), centralized error handling with `ProblemDetails`, and unit testing with xUnit and Moq.

## Workflow

The project was developed on Azure DevOps (Boards + Repos): work on `feature/*` (or `test/*`) branches, integrate through pull requests into `dev` and `main`, and link work items in commit messages (`closes #<id>`).

## Credits

HealthTrace was built as a team project by:

- **Gerhard Pirretti**
- **Davide Barbieri**
- **Edoardo Martino**
