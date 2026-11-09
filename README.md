# ZU Market

ZU Market is a bilingual ASP.NET Core MVC marketplace for verified Zarqa University students. It uses Razor Views, ASP.NET Core Identity, Entity Framework Core 8, and Microsoft SQL Server.

## Quick start with Docker

With Docker Desktop running, this starts the app, SQL Server and the database setup:

```bash
docker compose up --build
```

Open http://localhost:8080 and sign in with `admin@zumarket.local` / `Admin_Dev_2026!`. These credentials are for this local stack only.

See [DOCUMENTATION.md](DOCUMENTATION.md) for how to use the stack and how the code is designed. The rest of this file covers running without Docker.

## Prerequisites

- .NET 8 SDK
- SQL Server 2019 or newer, including SQL Server Express
- An HTTPS-capable browser for camera capture outside `localhost`

## Configure local development

The development configuration uses Windows Authentication with `localhost\SQLEXPRESS01` and database `ZuMarketDb`. The connection contains no password. Store an alternate SQL Server connection in User Secrets only when required:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER;Database=ZuMarketDb;Trusted_Connection=True;TrustServerCertificate=True"
```

For SQL authentication, place the password only in User Secrets or an environment variable. Do not add it to `appsettings.json`.

Registration is limited to university email addresses. Set the confirmed domain; separate several exact domains with commas. It must not be guessed, and registration stays closed while it is empty:

```powershell
dotnet user-secrets set "Registration:AllowedEmailDomain" "confirmed-domain.example"
```

A student is approved automatically when they confirm that address. Accounts already rejected by an administrator stay rejected.

Confirmation and password reset emails are sent over SMTP (port 587 with STARTTLS by default):

```powershell
dotnet user-secrets set "Email:Host" "smtp.provider.example"
dotnet user-secrets set "Email:UserName" "SMTP_USER"
dotnet user-secrets set "Email:Password" "SMTP_PASSWORD"
dotnet user-secrets set "Email:FromAddress" "no-reply@your-verified-domain.example"
```

Without these settings, Development shows the links on the page and Production refuses new registrations.

## Database

```powershell
dotnet restore
dotnet tool restore
dotnet ef database update
```

To create a future migration:

```powershell
dotnet ef migrations add DescriptiveMigrationName
dotnet ef database update
```

The existing database contains the initial Identity and marketplace migration. A new migration adds AI listing review records and review states without removing existing marketplace data. It adds only the required categories: the 14 university faculties and Other. It does not seed users, listings, messages, or reports.

To enable the optional AI listing reviewer in Development, configure the provider model and credentials through secure configuration:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "ENTER_THE_KEY_LOCALLY"
dotnet user-secrets set "OpenAI:Enabled" "true"
```

The model defaults to `omni-moderation-latest`; override it locally with `dotnet user-secrets set "OpenAI:Model" "MODEL_NAME"` only when needed. Environment-based configuration uses ASP.NET Core's hierarchical name `OpenAI__ApiKey`. AI review evaluates only one submitted listing, its own images, and the configured posting rules. If the provider is not configured or the response is uncertain or invalid, the listing remains pending for a human administrator. Automatic approval requires every configured rule check to pass and confidence to meet the configured threshold.

Check only whether Development configuration was loaded (this never prints the key and does not contact OpenAI):

```powershell
dotnet run -- --check-ai-configuration
```

Perform a real provider verification request (this reports `Ready` only after OpenAI accepts the request):

```powershell
dotnet run -- --check-ai-provider
```

## AI assistance

Three optional features use the OpenAI key above through the Responses API. They are switched on with `AiAssist:Enabled`, which is `true` in `appsettings.Development.json` and `false` in `appsettings.json`:

- **Fill from photo** runs automatically when a student adds the first photo on the create page. It uses up to 3 photos and fills only empty fields. Nothing is saved until the student submits, and each student is limited to `AiAssist:DraftDailyLimit` drafts a day.
- **Translation** runs in the background after a listing is approved and stores an Arabic or English version. Readers can always open the original, and editing the title or description clears the translation.
- **Report summaries** are requested by an administrator on the reports page. They are advice only and exclude names, emails and chats.

Each feature's model is set by `AiAssist:DraftModel`, `AiAssist:TranslationModel` and `AiAssist:SummaryModel`. If OpenAI is unavailable, students and administrators continue without these features. Set a monthly budget for the API key's OpenAI project before going live.

## Create the local development administrator

The startup initializer creates the `User` and `Admin` roles in any environment. It creates or bootstraps an administrator only in Development when secure settings are present. Store the credentials with User Secrets before running locally:

```powershell
dotnet user-secrets set "InitialAdmin:Email" "your-admin-address"
dotnet user-secrets set "InitialAdmin:Password" "a-strong-unique-password"
dotnet user-secrets set "InitialAdmin:FullName" "Administrator Name"
```

An existing account is never duplicated. Its password is not changed unless an explicit one-time reset key is configured. Remove bootstrap secrets after setup:

```powershell
dotnet user-secrets remove "InitialAdmin:Password"
dotnet user-secrets remove "InitialAdmin:ResetExistingPassword"
dotnet user-secrets remove "InitialAdmin:PasswordResetKey"
```

## Run

```powershell
dotnet run --launch-profile https
```

Open the HTTPS address shown in the terminal. The language switch in the main navigation sets an ASP.NET Core culture cookie and changes between English LTR and Arabic RTL.

## Private files and camera access

Listing photos, and university ID images submitted before email-based signup, are stored under `App_Data/private`, outside `wwwroot`. Controller actions enforce owner, participant, publication, and administrator access rules before returning a file. Back up this directory together with the database in production.

Camera capture uses `navigator.mediaDevices.getUserMedia`. It works only on HTTPS or `localhost`, requires explicit browser permission, and depends on browser and device camera support. File upload remains available when permission is denied or the camera is unavailable.

## Password reset and external delivery

In Development, confirmation and reset links are also shown on the page so the workflow can be tested without a mail server. Production does not expose those links. No SMTP, SMS, OCR, or API credentials are committed.

OCR is intentionally not used as proof of identity. University membership is established by a confirmed university email. Administrators can still review ID images submitted before this change. If optional OCR is added later, it must not send documents to an external service without explicit configuration and privacy review.

## Production checklist

- Set `ConnectionStrings__DefaultConnection` through a deployment secret store.
- Run `dotnet ef database update` as a controlled deployment step.
- Configure the `Email` settings through a deployment secret store, and send a real test message to a university address.
- Set `AllowedHosts` to the site's real host name, because confirmation and reset links are built from the request host.
- Use HTTPS and a persistent protected `App_Data` volume, and protect Data Protection keys at rest with an operating-system or certificate-backed mechanism.
- Set the confirmed university email domain.
- Remove the initial administrator password after bootstrap.
- Configure backups, log retention, upload malware scanning, and a reverse proxy request-size limit.
