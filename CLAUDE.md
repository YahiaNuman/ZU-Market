# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

ZU Market is a bilingual (English LTR / Arabic RTL) marketplace for verified Zarqa University students: a single ASP.NET Core 8 MVC project using Razor Views, Identity, EF Core 8 and SQL Server. `README.md` covers first-time setup and the production checklist, and `DOCUMENTATION.md` covers the full design.

## Commands

Run from this directory (the one with `ZuMarket.csproj`), which is the git repository root. `ZuMarket.slnx` is one level up and is not tracked.

```powershell
docker compose up --build                      # app, SQL Server and migrations at http://localhost:8080

dotnet restore
dotnet tool restore                            # installs the pinned local dotnet-ef
dotnet build
dotnet run --launch-profile https              # https://localhost:7278

dotnet ef migrations add DescriptiveName
dotnet ef database update
```

There is no test project and no linter configured, so `dotnet build` plus running the app is the only automated check.

`Program.cs` handles these flags and exits before serving requests. The last three report `DisabledOutsideDevelopment` outside Development.

| Flag (`dotnet run -- <flag>`) | Effect |
|---|---|
| `--migrate-database` | Applies EF migrations |
| `--check-ai-configuration` | Reports whether OpenAI settings loaded, with no network call |
| `--check-ai-provider` | Sends a real request to OpenAI; `AI_PROVIDER_STATUS=Ready` means it was accepted |
| `--inspect-database` | Prints the server, database and migration state |

## Configuration

- Development uses Windows Authentication against `localhost\SQLEXPRESS01`, database `ZuMarketDb`.
- Secrets live in User Secrets (`UserSecretsId` = `ZuMarket-Local-Development`) or environment variables such as `OpenAI__ApiKey`: `OpenAI:ApiKey`, `Email:*`, `InitialAdmin:*`, and any SQL password.
- `Registration:AllowedEmailDomain` is required (comma-separated exact domains). While it is empty, registration is closed in every environment. Never guess the domain.
- `IdentitySetupService` creates the `User` and `Admin` roles in every environment, but creates the bootstrap admin only in Development when `InitialAdmin:*` is set. It runs as a hosted service, so the flags above do not trigger it.
- Migrations seed only the categories (14 faculties and Other). Do not seed users, listings, messages or reports.

## Architecture

Controllers use `ApplicationDbContext` directly; there is no repository layer. Entities, the DbContext, view models and enums are all in `Models/`.

**Marketplace access is checked per action.** `[Authorize]` only proves the user is signed in. Selling and messaging also require `ApplicationUser.CanUseMarketplace` (verification approved and not disabled), which each action checks by hand, so new actions must do the same.

**University membership is proven by a confirmed university email.** `IUniversityEmailPolicy` holds the domain rule, and `CanAutoApprove` moves only `Pending` accounts to `Approved`, so an admin rejection is never overturned. It runs at email confirmation, at sign-in and on the verification page, which covers accounts created before this rule. Card upload is gone; `VerificationRequest` and the admin verification pages remain only for requests submitted earlier. OCR must not be treated as proof of identity.

**Listings have two status fields.** `ModerationStatus` controls public visibility, and public queries filter on `Approved`. `ReviewState` records how that outcome was reached (AI or manual). `AdminController` keeps them in step and logs a `ModerationAction`.

**Automated listing review** (`Services/AiListingReviewService.cs`) runs synchronously inside the `ListingsController` request, after the listing is saved:

- `ListingReviewOrchestrator.ReviewAsync` does nothing unless `ModerationStatus` is `Pending`, so any path that should re-trigger review must reset it.
- `AiReview:PostingRules` are evaluated locally in `ValidateLocally`, not by OpenAI. An unrecognized rule name fails, and an empty rule list sends every listing to manual review. New rules need a case there.
- This review calls OpenAI only through the moderation endpoint (`omni-moderation-latest`), which must be HTTPS. Confidence is `1 - highest category score`.
- Auto-approval requires an `Approved` decision, confidence at or above the threshold, and every local check passing. Any other result, including provider errors, leaves the listing `Pending` with `NeedsManualReview`.
- `ValidateLocally` hardcodes category IDs: 4 (Other) and 5-18 (faculties) are allowed. Adding a category means updating it, along with the ID map in `ListingDraftService` and the icon in `_Layout.cshtml`. Free listings use `IsFree`; no category forces a price.

**AI assistance** (`AiAssist:*`) goes through `IAiResponsesClient`, which returns `null` on any failure, refusal or truncated output, so every caller must leave the site working without AI. It reuses `OpenAI:ApiKey`, and AI output must never publish, approve or remove anything.

- User-written text goes to the model as JSON via `AiPrompt.AsData`, and instructions must treat it as untrusted. Validate output lengths and enum values in code.
- `ListingDraftService` backs `ListingsController.Draft`, limited per student by the `listing-draft` rate limiter. It stores nothing and never offers a price. The create form (`wwwroot/js/listing-draft.js`) runs it automatically once on the first photo and only fills fields that are still empty; on any failure the fields stay empty and the form still works.
- `ListingTranslationWorker` polls for approved listings with no translation. The database is the queue, with exponential backoff capped at one day. `Listing.ClearTranslation` must run wherever `Title` or `Description` changes, and the worker's update matches on the original text so an edit during a call discards the result.
- Public cards and details use the translation; admin pages use the original. Search matches translated text, using `ToLikePattern` so normalized Arabic letters match their stored spellings.
- `ReportSummaryService` is admin-triggered, summarizes all pending reports per listing, and reuses a saved summary while `IsCurrentFor` holds. It must not send names, emails or chats.

**Private files** are stored in `App_Data/private/{scope}`, outside `wwwroot`, with scopes `listings` and `verification` (older ID images). `IPrivateFileStorage` validates uploads (JPEG, PNG or WebP by file signature, 5 MB maximum) and safe paths, but performs no authorization. The controller action serving a file must enforce owner, participant, publication or admin rules. Data Protection keys are in `App_Data/keys`.

**Chat** uses SignalR at `/chat-hub`, but `ChatHub` only joins a user to a conversation group after a participant check. Messages are sent through a `MessagesController` POST, which saves them and then broadcasts `ReceiveMessage` via `IHubContext<ChatHub>`. The browser side is `wwwroot/js/chat.js` with the local `signalr-lite.js`.

**Localization** does not use the standard per-type resource lookup. `EmbeddedResourceStringLocalizerFactory` ignores the requested type and always reads `Resources/SharedResource.resx` or `SharedResource.ar.resx`.

- Every user-facing string needs an entry in both files. A missing key silently renders the key name.
- Views use the globally injected `@L["Key"]`.
- The Arabic file is embedded with a custom `LogicalName` in the `.csproj`, so keep that entry if it moves.
- Culture comes only from the ASP.NET Core culture cookie, with `en` as the default.

**Email** goes through `IAppEmailSender` (SMTP, `Email:*` settings) for confirmation and password reset, with a 60-second per-account resend cooldown held in memory. Development also shows the links on the page; production does not, and refuses new registrations if SMTP is not configured.
