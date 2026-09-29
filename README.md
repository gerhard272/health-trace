# HealthTrace

Diario dei sintomi personale: l'utente si registra, annota i propri sintomi (evento, descrizione, data e ora) e può chiedere un **export PDF** del diario, filtrato per sintomi o per intervallo di date, da consegnare per esempio al medico. Il PDF viene generato in background da una Azure Function.

## Architettura

```
 HealthTrace.Web (Angular)
          │  HTTP + Basic Auth
          ▼
 HealthTrace.PL.API ──────────────► Queue Storage "export-requests"
          │                                   │  QueueTrigger
          ▼                                   ▼
 HealthTrace.BLL  ◄──────────────── HealthTrace.Functions
          │                          (riusa BLL e DAL)
          ▼
 HealthTrace.DAL ──► Azure SQL Database
                 └─► Blob Storage "exports"
```

| Progetto | Ruolo |
| --- | --- |
| `HealthTrace.PL.API` | Web API ASP.NET Core (.NET 10): controller, autenticazione Basic, gestione errori globale, logging Serilog, documentazione Scalar |
| `HealthTrace.BLL` | Servizi, DTO, validazioni FluentValidation, mapping AutoMapper, hash password BCrypt, generazione PDF QuestPDF |
| `HealthTrace.DAL` | Entità, `HealthTraceDbContext` EF Core (SQL Server), repository generico + Unit of Work, accesso a Blob Storage |
| `HealthTrace.Functions` | Azure Function (isolated worker) `ProcessExportFunction` con QueueTrigger: genera i PDF |
| `HealthTrace.Web` | Frontend Angular 22 + Tailwind CSS 4: login/registrazione, diario sintomi, export |
| `HealthTrace.Test` | Test unitari xUnit + Moq |

I riferimenti vanno in una sola direzione: PL → BLL → DAL. La Function referenzia BLL e DAL e non duplica logica.

## Requisiti

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (LTS) e npm
- SQL Server: Azure SQL, oppure LocalDB / SQL Server locale per sviluppo
- Storage Azure (Blob + Queue), oppure [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) in locale
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) per eseguire la Function in locale
- `dotnet-ef` per le migration: `dotnet tool install --global dotnet-ef`

## Configurazione

Nessuna stringa di connessione è nel repository. API e Function devono puntare **allo stesso database e allo stesso storage account**.

| Chiave | API (User Secrets) | Function (`local.settings.json`) | Uso |
| --- | --- | --- | --- |
| `ConnectionStrings:HealthTraceDb` | ✓ | ✓ | Database |
| `BlobStorage:ConnectionString` | ✓ | ✓ | PDF su Blob; nell'API anche l'invio in coda |
| `BlobStorage:DefaultContainerName` | ✓ (default `exports`) | ✓ | Container dei PDF |
| `AzureWebJobsStorage` | | ✓ | Storage da cui il QueueTrigger legge `export-requests` |

API:

```bash
cd HealthTrace.PL.API
dotnet user-secrets set "ConnectionStrings:HealthTraceDb" "<connection string>"
dotnet user-secrets set "BlobStorage:ConnectionString" "<storage connection string>"
```

Function: crea `HealthTrace.Functions/local.settings.json` (è escluso da git):

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

In locale si può usare `UseDevelopmentStorage=true` (Azurite) al posto delle stringhe dello storage. Se usi Azure SQL, l'IP della tua macchina deve essere nelle regole firewall del server.

## Avvio in locale

1. **Database**: applica le migration.

   ```bash
   dotnet ef database update --project HealthTrace.DAL --startup-project HealthTrace.PL.API
   ```

2. **API**: https://localhost:7133, documentazione interattiva su https://localhost:7133/api-docs (Scalar, solo in Development).

   ```bash
   dotnet run --project HealthTrace.PL.API --launch-profile https
   ```

3. **Function**: necessaria perché gli export passino da Pending a Completed.

   ```bash
   cd HealthTrace.Functions
   func start
   ```

4. **Frontend**: http://localhost:4200 (CORS dell'API già abilitato per questa origine; l'URL dell'API è in `src/environments/`).

   ```bash
   cd HealthTrace.Web
   npm ci
   npx ng serve
   ```

Da Visual Studio si possono avviare API e Function insieme con più progetti di avvio. Il debugger si ferma sulle eccezioni di dominio lanciate dai controller anche se il gestore globale le gestisce: vedi [Gestione degli errori](#gestione-degli-errori).

## Test

```bash
dotnet test HealthTrace.Test
```

Coprono servizi (User, Symptom, Export, generico), controller, validazioni, mapping, hash delle password, eccezioni, `ExceptionStatusMapper` e logging.

```bash
cd HealthTrace.Web
npx ng test --watch=false
```

Coprono servizi Angular, guard, interceptor e pagine.

## API

Tutti gli endpoint tranne `auth` richiedono l'header `Authorization: Basic <base64(username:password)>`. Ogni query filtra per l'utente autenticato: un utente non vede mai i dati di un altro (risponde 404).

| Metodo | Endpoint | Descrizione |
| --- | --- | --- |
| POST | `/api/auth/register` | Registrazione (201) |
| POST | `/api/auth/login` | Verifica credenziali, restituisce il profilo |
| GET | `/api/symptom` | Lista; filtro `?date=yyyy-MM-dd` **oppure** `?name=` (anche parte del nome), non entrambi |
| GET | `/api/symptom/{id}` | Dettaglio |
| POST | `/api/symptom` | Inserimento (201) |
| PUT | `/api/symptom/{id}` | Modifica (204); l'id della rotta deve coincidere con quello del body |
| DELETE | `/api/symptom/{id}` | Eliminazione logica (204) |
| POST | `/api/exports/request` | Richiede un export: `{}` = tutti, `symptomIds`, oppure `fromDate`/`toDate`. Risponde 202 |
| GET | `/api/exports` | Cronologia degli export |
| GET | `/api/exports/{id}` | Stato: `Pending`, `Processing`, `Completed`, `Failed` |
| GET | `/api/exports/{id}/download` | PDF se `Completed`, altrimenti 409 |

### Flusso dell'export

1. `POST /api/exports/request` salva un `ExportRequest` in stato `Pending`.
2. `QueueExportJobDispatcher` mette l'id nella coda `export-requests` (in Base64, come si aspetta il QueueTrigger) e l'API risponde subito 202.
3. `ProcessExportFunction` imposta `Processing`, genera il PDF con QuestPDF e lo carica su Blob come `{userId}/{guid}.pdf`.
4. Lo stato diventa `Completed` (con `BlobName` e `FileName`) oppure `Failed` (con `ErrorMessage`).
5. Il client interroga `GET /api/exports/{id}` e scarica il file da `/download`.

La Function è idempotente: un messaggio consegnato di nuovo per una richiesta già `Processing` o `Completed` viene ignorato. `InlineExportJobDispatcher` (PDF generato nella stessa richiesta HTTP) è ancora disponibile: basta cambiare la registrazione di `IExportJobDispatcher` in `Program.cs` dell'API.

## Gestione degli errori

Controller e servizi non costruiscono risposte d'errore: lanciano eccezioni di dominio (`HealthTrace.BLL.Exceptions`) che `GlobalExceptionHandler` traduce in [ProblemDetails](https://www.rfc-editor.org/rfc/rfc7807) tramite `ExceptionStatusMapper`.

| Eccezione | Status |
| --- | --- |
| `ValidationException` | 400 (`ValidationProblemDetails` con gli errori per campo) |
| `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `NotFoundException` | 404 (estensioni `resourceName`, `resourceKey`) |
| `ConflictException` | 409 |
| qualsiasi altra | 500, senza dettagli (restano nel log) |

Con il debugger di Visual Studio e *Just My Code* attivo, l'esecuzione si ferma su queste eccezioni anche se sono gestite. Per evitarlo, in *Impostazioni eccezioni* togli la spunta su `HealthTrace.BLL.Exceptions.AppException`, oppure avvia senza debug (Ctrl+F5).

## Dati e sicurezza

- Password salvate come hash BCrypt; username e codice fiscale univoci.
- Tutte le entità ereditano da `AuditEntity`: `CreatedAt/By`, `ModifiedAt/By`, `DeletedAt/By` sono compilati in `SaveChanges` (le scritture della Function risultano all'utente "sistema", id 0).
- Cancellazione logica: una Delete diventa `IsDeleted = true`; `Symptom` ed `ExportRequest` hanno un query filter globale.
- Log: Serilog su console e, fuori da Development, su file JSON in `logs/` con rotazione giornaliera.
- Il frontend salva le credenziali Basic in `sessionStorage` e le invia solo all'URL dell'API.

## Workflow

Repository su Azure DevOps. Si lavora su branch `feature/*` (o `test/*`) e si integra con pull request verso `dev` e `main`, collegando i work item nel messaggio (`closes #<id>`).
