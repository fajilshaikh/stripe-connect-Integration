# Jyrney · Stripe Connect Test Project

A proof of concept of the Stripe payment module for Jyrney, a managed ground-transport platform (taxi, ride-hail, chauffeur). It assumes the Jyrney booking platform already exists and adds only the **Stripe / Stripe Connect** integration. **Jyrney (the platform) runs supplier onboarding.**

| | |
|---|---|
| Backend | .NET 9 Web API (controllers), Stripe.net 50.1.0; `API/` + `Core/` + `Infrastructure/` (`Jyrney.sln`), port 4242 |
| Frontend | React 18 + TypeScript + Vite + Tailwind CSS 4, Stripe Payment Element (`client/`, port 5173) |
| Stripe mode | Test mode, Connect enabled, Express connected accounts |
| Status | All flows tested and working (onboarding, authorise, capture, cancel, refund) |

---

## 1. Client requirements (from the brief)

**In scope**
- Card payment against a booking, frontend (Jyrney app and white-label apps) and backend.
- Job lifecycle: quote → book → fulfil. The final price can differ from the quote, and the booking can be cancelled at any point.
- Two commercial models:
  - **Seller**: Jyrney's revenue is the full fare.
  - **Agent**: Jyrney earns commission only. This needs **Stripe Connect** and supplier onboarding.
- The payer is often **not** the traveller.
- Refunds, cancellation charges, webhooks, reconciliation, and ops visibility into failed payments.

**Constraints**
- **PCI SAQ-A**: no card data on Jyrney servers, logs or databases.
- Secrets are stored in **Azure Key Vault** only.
- Deliverables: documentation, a failure runbook, and a handover session with Jyrney's developers.

**Core difficulty:** this is a payment *lifecycle* problem. When the card is authorised, how far the final price moves from the quote, and when cancellation happens all interact with two different legal models of who the seller is.

---

## 2. Key decisions

| Topic | Decision | Reason |
|---|---|---|
| Connected account type | **Express**, hosted onboarding via Account Links | Jyrney controls onboarding; Stripe hosts KYC. |
| Agent charge type | **Destination charge** + `on_behalf_of` + `transfer_data.destination` + `application_fee_amount` | ⚠️ Stripe **blocks direct charges on Express/Custom accounts for new platforms** (error: *"Creating direct charges with type=express … is not supported for new platforms"*). Destination charges are the supported pattern for Express. |
| Seller charge type | Plain platform charge (no transfer, no fee) | Jyrney is the merchant of record. |
| Capture | `capture_method = manual` | Hold the money at booking and capture the final price after the trip. |
| Price variance | Authorise **quote + buffer** (default 20%) | The final price can be captured up to the held amount. |
| Card entry | Stripe **Payment Element** (iframes) | Card data goes from the browser to Stripe, which keeps us in SAQ-A. |
| Idempotency | Key `booking:{id}:{step}` on every Stripe write | Retries can never double-charge. |
| Webhooks | Separate platform and Connect endpoints; verify on the raw body; queue; dedupe; re-read the Stripe object | Duplicate and out-of-order events are harmless. |
| Persistence | **SQL Server** via EF Core 9 (database `JyrneyPayments`); migrations applied on startup | Same engine as the Azure SQL target. Suppliers are also refreshed from Stripe on startup (`accounts.list`), because Stripe is the source of truth for account status. |
| Concurrency | `rowversion` on Suppliers and Bookings | A webhook and an ops action on the same row can't silently overwrite each other; the loser gets a 409. |

> **Open legal question (brief A1):** with destination charges, the platform carries more refund and dispute liability than with direct charges. Confirm with Jyrney's legal and tax advisers. If direct charges are mandatory, suppliers must be **Standard** accounts, which is a poor fit for platform-led onboarding.

---

## 3. Calculations

All amounts are stored in **minor units** (pence): `4500` = £45.00.

```
AuthorizedAmount = Quote + Quote × AuthBufferPercent / 100
                 = 45.00 + 20% (9.00) = 54.00

Commission (application fee) = CapturedAmount × CommissionBps / 10,000      (Agent only)
                             = 50.00 × 1500 / 10,000 = 7.50   → Jyrney
Supplier share               = 50.00 − 7.50 = 42.50          → supplier's connected account
Released to payer            = 54.00 − 50.00 = 4.00

Refund 20.00 (Agent):
  Commission returned  = 7.50 × 20.00 / 50.00 = 3.00   (refund_application_fee)
  Pulled from supplier = 20.00 − 3.00 = 17.00          (reverse_transfer)
```

- A commission is **provisional** at authorisation (calculated on the held amount) and **recalculated on the final price** at capture.
- With destination charges, the **platform pays the Stripe processing fee** out of its commission.
- Seller model: commission is 0 and Jyrney keeps the full amount.

Config (`API/appsettings.json` → `Stripe`): `AuthBufferPercent` (20), `DefaultCommissionBps` (1500 = 15%), `Currency` (gbp), `Country` (GB).

---

## 4. Operations (flows)

### 4.1 Supplier onboarding (platform-led)
1. Ops creates the supplier: `POST /api/suppliers` creates an Express account with `card_payments` and `transfers` capabilities and MCC 4121 (taxi/limousine). The metadata includes `supplier_id`.
2. Ops gets an **onboarding link** (`POST /api/suppliers/{id}/onboarding-link`) and sends it to the supplier.
3. The supplier completes Stripe-hosted onboarding and is returned to `/?onboarded={id}`, where the app syncs the status automatically.
4. An expired link goes to `/api/suppliers/{id}/onboarding-refresh`, which mints a new link and redirects.
5. Status is kept up to date by the `account.updated` webhook, plus the manual **Refresh** button.

**State machine:**

| Status | Condition |
|---|---|
| `Invited` | `details_submitted = false` |
| `PendingVerification` | Details submitted; `disabled_reason` is null or `requirements.pending_verification` |
| `Enabled` | `charges_enabled` **and** `payouts_enabled` |
| `Restricted` | Any other `disabled_reason` (e.g. `requirements.past_due`) |

**Gating:** an Agent booking is rejected (409) unless the supplier is `Enabled`.

### 4.2 Booking payment lifecycle

| Step | Endpoint | Stripe call | Booking status |
|---|---|---|---|
| Book | `POST /api/bookings` | Create PaymentIntent (manual capture, quote + buffer) | `PendingPayment` |
| Pay | Frontend `confirmPayment` (handles 3DS) | PaymentIntent → `requires_capture` | `Authorized` |
| Trip completed | `POST /bookings/{id}/complete {amount}` | Capture `amount_to_capture` + `application_fee_amount` | `Captured` |
| Cancel, no fee | `POST /bookings/{id}/cancel {0}` | Cancel PaymentIntent (hold released) | `Cancelled` |
| Cancel with fee | `POST /bookings/{id}/cancel {fee}` | Partial capture of the fee | `CancelledWithFee` |
| Cancel after capture | `POST /bookings/{id}/cancel {fee}` | Refund everything except the fee | `PartiallyRefunded` |
| Refund | `POST /bookings/{id}/refund {amount}` | Refund + `reverse_transfer` + `refund_application_fee` | `PartiallyRefunded` / `Refunded` |
| Decline | (webhook / sync) | `requires_payment_method` + `last_payment_error` | `Failed`, with a plain-language reason |

**Rules**
- A final price **above** the held amount returns 409 *"needs off-session top-up"* (not built yet).
- Status only moves forward. Webhook syncs never overwrite actions the app has already applied.
- An authorisation expires after about 7 days; Stripe then cancels the PaymentIntent and the booking becomes `Cancelled`.

### 4.3 Webhooks

| Endpoint | Secret | Receives |
|---|---|---|
| `/api/webhooks/platform` | `Stripe:PlatformWebhookSecret` | `payment_intent.*` (all payments, because destination charges live on the platform) |
| `/api/webhooks/connect` | `Stripe:ConnectWebhookSecret` | `account.updated` (connected accounts) |

Pipeline: read the raw body → verify the signature (400 if invalid or missing) → queue → return 200 straight away → background processor → dedupe by event id → **re-read the current object from Stripe** → update the booking or supplier.

**Webhook secrets are optional for local testing.** The app also syncs on demand: the Refresh and Sync buttons, the onboarding return, and right after authorisation.

### 4.4 Bookings tab: money columns

| Column | Meaning |
|---|---|
| Agent / supplier | Supplier name and connected account (Agent model), or Jyrney (Seller model) |
| Paid by traveller | Captured minus refunded |
| Stripe fee | Real processing fee from the charge's balance transaction, paid by Jyrney in both models |
| Commission | Application fee kept after refunds (`refund_application_fee` returns it proportionally) |
| Platform net | Agent: commission − Stripe fee. Seller: amount paid − Stripe fee. Can go negative after a full refund, because Stripe keeps its fee |
| Supplier net | Agent: amount paid − commission (what stays in the supplier's account after reversals). Seller: none |
| Settlement | `available_on` of the balance transaction: when the funds become available in the Stripe balance |

The Stripe fee and settlement date are read when the payment is captured (`latest_charge.balance_transaction` is expanded). If Stripe hasn't created the balance transaction yet, **Sync** picks them up later. Clicking a booking ID shows the money-flow breakdown and the timeline.

### 4.5 Payment flow log
Every step adds a human-readable entry with the calculation breakdown to **`API/logs/payment-flow.txt`**. The UI timeline shows the short version: click a booking ID on the Bookings tab.

---

## 5. Project structure

Layered like the skinet reference: `API` → `Infrastructure` → `Core` (Core has no dependencies).

```
Jyrney.sln
Core/                                   Domain only, no Stripe or ASP.NET references
  Entities/                             BaseEntity, Supplier, Booking, CommercialModel, OnboardingStatus, BookingStatus
  Interfaces/                           IPaymentService, ISupplierOnboardingService, IGenericRepository,
                                        ISupplierRepository, IBookingRepository, IStripeEventStore, IPaymentFlowLog
Infrastructure/                         Stripe.net and storage implementations
  Data/                                 PaymentsContext (EF Core) + Supplier/Booking repositories + StripeEventStore (webhook dedupe)
  Config/                               EF entity configurations (column sizes, enums as strings, indexes, rowversion)
  Migrations/                           EF Core migrations (InitialCreate)
  Services/
    PaymentService.cs                   Authorise / capture / cancel / refund / sync, calculations
    SupplierOnboardingService.cs        Express account, Account Links, status sync, import from Stripe
    StripeEventProcessor.cs             Background webhook event processor
    PaymentFlowLog.cs                   logs/payment-flow.txt writer
  Settings/StripeSettings.cs            "Stripe" config section
API/
  Program.cs                            DI, Key Vault, middleware, supplier import on startup
  Controllers/                          Config, Suppliers, Bookings, Webhooks (BaseApiController = api/[controller])
  DTOs/                                 Request/response contracts (entities are never returned directly)
  Extensions/                           Entity → DTO mapping
  Errors/ApiErrorResponse.cs            Error body: { statusCode, message, details }
  Middleware/ExceptionMiddleware.cs     Exception → HTTP status (402 / 409 / 404 / 500)
  Properties/launchSettings.json        http://localhost:4242, Development
  appsettings.json                      Config (keys go in user-secrets / Key Vault)
client/src/
  main.tsx                              Mounts <App />
  app/
    App.tsx                             Tabs + data loading
    app.config.ts                       Tabs, white-label tenant theming (Appearance API)
    core/services/                      http (fetch + error handling), config, supplier, booking, stripe services
    features/suppliers/Suppliers.tsx    Ops: create, onboard, refresh, re-load from Stripe
    features/book/Book.tsx              New booking + payment
    features/bookings/Bookings.tsx      Ops: complete, cancel, refund, sync, timeline
    layout/header/Header.tsx            Title, tenant picker, tab nav
    shared/components/payment-form/     Shared Payment Element form (main + white-label)
    shared/models/                      Supplier, Booking, AppConfig types
    shared/utils/money.ts               Money formatting, amount prompt
```

### API reference
```
GET  /api/config                          GET  /api/suppliers
POST /api/suppliers {name,email}          POST /api/suppliers/import
POST /api/suppliers/{id}/onboarding-link  GET  /api/suppliers/{id}/onboarding-refresh
POST /api/suppliers/{id}/sync
GET  /api/bookings
POST /api/bookings {model,supplierId,payerEmail,travellerName,quoteAmount,commissionBps}
POST /api/bookings/{id}/complete|cancel|refund {amount}
POST /api/bookings/{id}/sync
POST /api/webhooks/platform               POST /api/webhooks/connect
```
Errors are returned as `ApiErrorResponse` (`{ statusCode, message, details }`): Stripe errors are 402, invalid state is 409, not found is 404. Request validation failures (e.g. a bad email) are 400 ProblemDetails with an `errors` map.

---

## 6. Run and test

```bash
# Keys (prefer user-secrets over appsettings.json; never commit keys)
cd API
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet run                                 # http://localhost:4242 (launchSettings.json, Development)
                                           # first run creates the JyrneyPayments database (Windows auth, localhost)

# Schema changes (from the solution root; dotnet-ef is a local tool in .config/dotnet-tools.json)
dotnet tool restore
dotnet dotnet-ef migrations add <Name> -p Infrastructure -s API -o Migrations

cd client && npm install && npm run dev    # http://localhost:5173 (proxies /api to API_URL, default http://localhost:4242)

# Optional: webhooks (one whsec_ is used for both secrets)
stripe login
stripe listen --forward-to localhost:4242/api/webhooks/platform --forward-connect-to localhost:4242/api/webhooks/connect
```

**Test cards:** `4242 4242 4242 4242` (success) · `4000 0025 0000 3155` (3DS) · `4000 0000 0000 9995` (insufficient funds).
**Test onboarding:** SMS code `000 000`; choose the test bank account.

**Test script:** create or import a supplier → onboard to `Enabled` → book an Agent fare of £45 at 15% → pay → complete at £50 → refund £20 → check `API/logs/payment-flow.txt` and, in the Dashboard, **Connect → Collected fees**.

---

## 7. Problems hit and fixes

| Problem | Cause | Fix |
|---|---|---|
| API won't start: *"API key cannot be empty"* | `Stripe:SecretKey` not set | Set the key; the API now fails fast with a clear message. |
| Webhook returned 500 with no signature | Stripe.net threw a null reference | A missing or invalid signature now returns 400. |
| `stripe trigger` → *"Expired API Key rk_test_…"* | The CLI's own key from `stripe login` expires after 90 days (not shown on the Dashboard) | Run `stripe login` again, or pass `--api-key sk_test_...`. |
| *"Direct charges with type=express not supported for new platforms"* | Stripe policy for new platforms | Switched the Agent model to **destination charges + on_behalf_of**. |
| Build error MSB3021 *"file is locked"* | The API is still running | Stop `dotnet run` (Ctrl+C) before rebuilding. |
| Suppliers and bookings disappeared on restart | In-memory store | Now stored in SQL Server; suppliers are also refreshed from Stripe on startup. |
| API starts but every call fails with *"Invalid object name 'Suppliers'"* | Started with `dotnet run --no-build` after adding a migration, so the new migration wasn't compiled in | Build first (plain `dotnet run` does), then start. |

---

## 8. Not built yet (next phases from the brief)

- Further SQL tables (PaymentAttempt, Refund, LedgerEntry) and a transactional outbox. *Done so far: Suppliers, Bookings (timeline stored as JSON) and StripeEvents in SQL Server.*
- **Azure Service Bus** for webhook processing (an in-process `Channel` is used now).
- **SetupIntent** for bookings more than 7 days ahead, then an off-session charge closer to travel.
- **Off-session top-up** when the final price exceeds the hold.
- Versioned **cancellation policy engine** (time bands, supplier terms, no-show).
- **Lodge / Digital Lodge** cards: the biggest unknown, and a risk to SAQ-A if a PAN is received.
- Daily **reconciliation** (balance transactions, payouts) with an exceptions queue.
- Ops alerts (Application Insights), authentication and roles, a dispute workflow.
- Key Vault in Azure: set `KeyVault:Uri`; secrets are named `Stripe--SecretKey` and so on.

## 9. Questions for Jyrney before committing to a timeline
1. Merchant of record in the agent model: are destination charges acceptable legally and for tax?
2. Markets, currencies and cross-border payouts per model.
3. How Lodge and Digital Lodge cards work technically: who holds the card, and how does it reach Stripe?
4. Spread of booking lead times, and the typical and maximum difference between quote and final price.
5. Cancellation and no-show rules per client contract.
6. Commission rules: percentage, fixed or tiered; fixed at quote or recalculated on the final price?
7. Supplier volume and type (operators vs owner-drivers), and payout schedule and reserves.
8. Who owns the PCI SAQ-A attestation and the SCA exemption policy.
