# Aura Café POS & Management System

An existing ASP.NET Core 10 application for cafe administration and cashier workflows. Admin uses a green theme; Cashier uses a pink theme with a cafe-style login and shared coffee-cup branding.

**Real payments currently support cash in USD cents. Bakong is a separate test-only integration.** Test confirmations do not mark real orders Paid, contribute revenue or change drawer cash. This project has not received production-readiness sign-off.

## Contents

- [Project structure](#project-structure)
- [Setup and run](#setup-and-run)
- [Database backups and migrations](#database-backups-and-migrations)
- [Pages and workflows](#pages-and-workflows)
- [Money, reporting and shifts](#money-reporting-and-shifts)
- [Inventory rules](#inventory-rules)
- [Images and saved merchant QR](#images-and-saved-merchant-qr)
- [Bakong sandbox connection](#bakong-sandbox-connection)
- [API overview](#api-overview)
- [Verification](#verification)
- [Troubleshooting and remaining work](#troubleshooting-and-remaining-work)

## Project structure

```text
AuraCafé/
  AuraCafé.slnx
  README.md
  AuraCafé.API/
    Auth/                    Identity, JWT and development seed accounts
    Controllers/             Auth, catalog, staff, shifts, orders, payments,
                             reports, inventory and Bakong sandbox
    DTOs/                    API request/response contracts
    Models/                  EF Core entities
    Interfaces/              Repository/service contracts
    Repositories/            Database queries
    Services/                Server-side business rules; Bakong/ provider adapter
    Data/AppDbContext.cs     Identity and application SQLite context
    Migrations/              Existing schema and additive feature migrations
    Extensions/              DI, startup migrations and API middleware
  AuraCafé.Web/
    Areas/Admin/Pages/       Dashboard, Staff, Inventory, Reports, Shifts,
                             Payments and Settings/PaymentQr
    Areas/Cashier/Pages/     Dashboard, POS, Orders, Payments and MyShift
    Pages/                   Public site, Account, Products, Categories, layouts
    Models/                  Web form and API response models
    Services/                API clients, session store and image/QR storage
    wwwroot/                 CSS, JavaScript and Bootstrap
    App_Data/                Runtime image files and saved merchant QR settings
  tests/ProductManagement.Checks/
                             Executable HTTP/JWT/SQLite/Razor regression suite
  tools/
    phase0_sqlite_audit.py    Read-only source audit and consistent SQLite backup
    start-bakong-sandbox.ps1  Hidden test-token prompt and API launcher
```

Stack: .NET 10, C#, Razor Pages, Bootstrap 5, EF Core, SQLite, ASP.NET Core Identity and JWT. Bakong test QR generation uses NBC's .NET SDK and QRCoder.

Typical flow: **Razor Page -> Web API service -> API controller -> application service -> repository -> EF Core -> SQLite.** Some existing modules use DbContext directly; the code retains those existing patterns. The API calculates order prices, totals and cash change.

## Setup and run

Prerequisites: .NET 10 SDK, the EF CLI tool when managing migrations, and Python 3 for the optional backup/audit tool. Run commands from the solution directory. Restore the pinned project dependencies rather than installing old packages manually.

```powershell
dotnet restore
dotnet build
```

Configure API secrets locally using API User Secrets, deployment secrets or environment variables. The API project already has a UserSecretsId. Do not commit credentials or paste tokens into chat.

| API configuration | Purpose |
| --- | --- |
| ConnectionStrings:DefaultConnection | SQLite connection string; existing local database is AuraCafé.API/auracafe.db |
| Jwt:Key | Strong secret signing key matching the application's JWT requirements |
| Jwt:Issuer / Jwt:Audience | JWT issuer and audience |
| SeedAdmin:Email / SeedAdmin:Password | Initial Development Admin account; required by the current seeder |
| SeedCashier:Email / SeedCashier:Password | Development Cashier seed configuration; see IdentitySeeder.cs |

Web configuration `ApiSettings:BaseUrl` must point to the API. The current local setting is `http://localhost:5100/`. Optional Web settings include `DataProtection:KeyDirectory`, `ProductImages:Directory` and `MerchantQr:Directory`.

Start the API in one terminal and Web in another:

```powershell
dotnet run --project AuraCafé.API
dotnet run --project AuraCafé.Web
```

Use the URLs printed by each terminal. The inspected API HTTP profile uses port 5100; the Web launch profile uses HTTP 61545 and HTTPS 61544. Local IDE profiles can differ. Sign in at `/Account/Login`; Admin and Cashier are redirected to their respective dashboards. Restart Web after compiled Razor changes and hard-refresh stale CSS/JavaScript.

**Development API startup automatically applies pending EF migrations and runs the Identity seeder. Back up the existing database before starting after schema changes.** Never delete or reset the database to solve startup or UI problems.

## Database backups and migrations

Use SQLite's backup API so committed WAL contents are included. The supplied tool opens the source read-only, creates a separate backup, and audits the backup:

```powershell
python tools/phase0_sqlite_audit.py "AuraCafé.API/auracafe.db" --backup-directory Backups
```

It reports integrity, foreign keys, migration history, record counts and payment anomalies. Inspect actual results; historical successful checks are not proof that today's database is clean.

Inspect migrations without deleting data:

```powershell
dotnet ef migrations list --project AuraCafé.API --startup-project AuraCafé.API
```

After taking a backup and reviewing pending migrations, apply them explicitly when needed:

```powershell
dotnet ef database update --project AuraCafé.API --startup-project AuraCafé.API
```

Recent additive migrations include inventory management, separate KHR drawer counts and `20261115085247_AddBakongSandboxIntents`. The last adds only the test-intent table, constraints and indexes. Its creation was tested on disposable SQLite; deployment state must be checked on your actual instance.

## Pages and workflows

| Role | Pages | Main behavior |
| --- | --- | --- |
| Admin | /Admin/Dashboard | Real daily metrics, seven-day completed-payment chart, popular products and cash flow |
| Admin | /Admin/Products (also /Products) | Search/filter/pagination, create/edit, computer image uploads and activation |
| Admin | /Admin/Categories (also /Categories) | Category management; deletion protects referenced products |
| Admin | /Admin/Staff | Real team counts, search, filters, create/edit and activate/deactivate |
| Admin | /Admin/Inventory | Ingredient directory, stock ledger and recipes |
| Admin | /Admin/Reports | Date-filtered real sales reports and CSV export |
| Admin | /Admin/Shifts /Admin/Payments | Shift monitoring and payment history |
| Admin | /Admin/Settings/PaymentQr | Save merchant image and test Bakong connection |
| Cashier | /Cashier/Dashboard | Recent own orders, payment/receipt links and actual shift details |
| Cashier | /Cashier/MyShift | Opening floats, clock-in, full-drawer closing and reconciliation |
| Cashier | /Cashier/POS | Category/search catalog, cart, server-priced order and checkout |
| Cashier | /Cashier/Orders | Own order history and eligible payment collection |
| Cashier | /Cashier/Payments/Create/{orderId} | Existing cash collection, saved QR display and paid cash receipt |
| Cashier | /Cashier/Payments/Sandbox/{orderId} | Separate Bakong test QR generation and verification |

Cashier workflow: clock in with counted opening floats, add products in POS, create an order, then collect cash using the server-confirmed total. Enter cash physically received; the API calculates change. After payment, use **Print customer receipt** only when requested. Thermal CSS targets 80mm paper; select the correct printer in the browser dialog. Physical printing still needs testing.

Cash payment requires a valid Cashier JWT, active Cashier staff profile, open shift, ownership and a Pending order. Payment and Paid order state are saved transactionally. The unique Payment.OrderId constraint prevents duplicate payment rows. When a submission result is unknown, check the existing order/payment before retrying; a timeout is not proof of failure.

Staff behavior matches the current backend: creating a Cashier requires a password and creates a linked Identity login; creating a Barista creates a profile without a new login. Cashier account-type changes require a separate workflow and are not editable in the profile form. Staff directory paging currently happens in the Web server after loading the existing full staff endpoint; large workforces need API/database pagination.

Point of Sale uses actual category buttons with available-product counts, combined name/description search, product image cards and visible in-cart quantities. Desktop shows categories, catalog and one cart; tablet/mobile use horizontal categories and a View cart bar opening the same cart in a native dialog. Quantity edits, category/search changes and drawer resizing preserve cart state. Clear cart uses the shared confirmation dialog. Only a server-returned cart after a failed POST is restored; there is no automatic local-storage resubmission. The order estimate is explicitly a preview, and the existing API confirms final prices. Cash and saved QR have separate guidance; QR display does not confirm payment. All payment endpoints, authorization, USD cents and optional paid receipt behavior remain unchanged. Files are the Cashier POS Razor view, `wwwroot/css/pos.css` and `wwwroot/js/pos.js` in Web.

POS has an English / Khmer language selector in its header. It remembers `en` or `km` in browser local storage and translates the POS controls, search hints, cart counters, accessible labels, cashier navigation on that page and POS confirmation dialogs. It updates the document language and uses Khmer-capable system-font fallbacks. Switching languages keeps the cart, filters and selected payment method. Database product/category names, descriptions, staff names, URLs, form values and USD amounts are not translated or converted. API validation messages and subsequent payment/receipt pages remain English; this is POS interface localization, not a whole-application translation. Dictionary and switching behavior live in Web `wwwroot/js/pos-language.js`; dynamic cart text uses the same dictionary. Interactive switching, Khmer typography and narrow-screen layout still need manual browser review.

Sales Reports uses the green Admin design with Today / Last 7 days / This month presets, custom inclusive dates, real completed-payment KPI cards, daily revenue bars, net cash retained, monthly totals, cashier results and ranked top-20 products. Export CSV downloads the current selected period through an Admin-only handler, with UTF-8 encoding, quoted cells and spreadsheet formula protection for names. Each CSV RecordType is an alternative breakdown; do not add Summary, Daily, Monthly, Cashier and ProductTop20 revenues together. Print report restores natural page flow and expands scrollable data; physical print output still needs manual verification. No report calculation or payment API was replaced.

Shift Management uses real recent records with name/staff-code/shift-number search, Open/Closed filters, inclusive Bangkok clock-in date filters and twelve-row pages. Open shifts sort first. Summary cards describe the API's loaded recent set (up to 200), before filters; these are not all-time totals. Filtering and paging occur in the Web server after the existing capped API response. The read-only drawer view separates USD and KHR, expected versus counted amounts, signed differences and closing notes. Missing legacy values remain Not recorded/Not submitted. The shared UI is in `wwwroot/css/shift-management.css` and the Admin Shifts Razor pages.

Portal sidebars and headers remain in place while main content scrolls. Long sidebars scroll independently; mobile navigation remains accessible above the content. Print styles restore natural page flow. Layout rules are shared in Web `wwwroot/css/portals.css`.

### Styled confirmations and notices

Admin and Cashier use a shared in-page dialog instead of the browser's localhost confirmation/alert popup. The Admin dialog inherits green; Cashier inherits pink; destructive actions use red. It covers order creation, cash collection, cart clearing, product/category changes, staff actions, inventory confirmations, shifts and cart limits.

Cancel, the close button and Escape dismiss the dialog; confirmation resumes the original form with its submitter, validation and antiforgery fields. Existing submission guards run after approval. Keyboard focus starts on the safe cancellation action and returns to the trigger on close. Messages are inserted as text rather than HTML.

Reusable files: `Pages/Shared/_ActionDialog.cshtml`, `wwwroot/css/dialogs.css` and `wwwroot/js/dialogs.js` within Web. Both portal layouts load them. Adjust styling there to customize all portal dialogs consistently. This is a presentation change; all authorization and payment validation stay on the server.

## Money, reporting and shifts

- Real payment currency is USD, stored as integer cents. Never accept browser-calculated prices, totals or change as authoritative.
- Reports use Asia/Bangkok (UTC+07:00). Inclusive local From/To dates become a half-open UTC range; confirmed revenue uses PaidAtUtc. Requests are limited to 366 days.
- Orders created and completed-payment counts are different metrics. Pending orders now is an all-date count. Revenue excludes unpaid orders and opening floats.
- Existing reports validate cash-only completed payments, paid order state, ownership/shift attribution and historical line arithmetic. Integrity errors must be investigated rather than hidden with zero totals.
- Product sales use historical order snapshots. The reporting API limits popular products to its top-product result set; full report totals are not derived from that truncated list.
- Opening USD and KHR fields start blank and are tracked independently. No KHR-to-USD conversion is performed. Enter numbers only, not expressions such as `50 + 75`.
- For recorded opening floats: **expected USD closing = opening USD + completed cash sales**. Cash received minus change is already net cash sales; do not count it twice. Difference = counted full drawer minus expected full drawer.
- KHR has no implemented sales/change workflow: expected KHR equals opening KHR. Count it separately at close.
- Legacy shifts without opening figures retain Not recorded; never silently treat missing historical values as zero.
- Closing reconciliations are immutable snapshots. Unsupported tax, discount, refund, void and currency-change workflows must be designed separately.

## Inventory rules

Create an ingredient with one base unit: **g**, **ml** or **pcs**. Use whole quantities: 1.5 kg becomes 1500 g; one liter becomes 1000 ml; cups are normally counted in pieces.

Units stay fixed after creation to preserve history. New ingredients start at zero available stock. Minimum quantity is a low-stock warning threshold, not an opening balance. After saving, record supplies using **Stock Movements > Stock In**.

StockIn and StockOut use positive quantities; Adjustment uses a signed quantity change. A reason is required. Movements record before/after balances, actor and timestamp; negative balances are blocked. Each movement has a unique request ID. If the response is unknown, inspect the ledger and reuse the unchanged request ID for a controlled retry. Ingredient deletion deactivates it and retains history.

Recipes specify positive whole quantities per product. Recipe edits do not change stock. **Sales/payments do not automatically deduct ingredients yet.** A future deduction workflow must handle atomicity, retries and order/payment transitions.

## Images and saved merchant QR

Upload original PNG, JPG or WebP files up to 5 MB. The server validates supported image signatures, generates safe filenames and rejects SVG. Product images remain available to Cashier POS; upload/edit can retain existing images.

Admin saves a merchant QR once under **Merchant QR**. Cashiers select QR at checkout and the saved image opens automatically. Return to checkout closes the dialog; Show QR to customer reopens it. This static display cannot confirm payment and does not produce a paid receipt.

Back up both Web runtime directories together and preserve them across deployment:

- `App_Data/product-images/`: original product and merchant image bytes.
- `App_Data/merchant-qr/current.json`: active merchant QR pointer, update time and Admin identity.

Both directories are ignored by Git. File-backed merchant configuration targets the current single Web instance; multiple instances need shared storage and coordinated updates.

## Bakong sandbox connection

This implementation is fixed to **https://sit-api-bakong.nbc.gov.kh/** and USD. It defaults to disabled. Receiving account supplied for testing: `sakhak_loeung@bkrt`. The account must exist in SIT; a real banking account/image alone does not establish SIT access.

1. Obtain or renew a test token through the [official SIT registration portal](https://sit-api-bakong.nbc.gov.kh/register). Use your actual project, organization and email details and complete provider verification. Confirm that the token is for SIT, not production or another service.
2. Stop the existing API on port 5100. Back up SQLite before startup migrations.
3. From the solution directory run:

   ```powershell
   .\tools\start-bakong-sandbox.ps1
   ```

4. Paste only the test token at the hidden local prompt, without `Bearer` or quotation marks. The launcher passes it to its API child process, does not save it to disk and restores shell environment variables on exit. Keep that terminal running. Starting a separate API from the IDE will not inherit those settings.
5. Start/restart Web. Open **Admin > Merchant QR > Test Bakong connection**. A successful check confirms the receiving account exists; it does not transfer money.
6. As Cashier, open a pending order's checkout, choose **Open Bakong test checkout**, then **Generate test QR**. Use the provider's test banking environment, not a production banking app or real money.

API options: `BakongSandbox:Enabled`, `ApiToken`, `ReceiverAccountId`, `ReceiverName`, `ReceiverCity`. Environment variable names use double underscores, for example `BakongSandbox__ApiToken`. Keep the token in server secrets/configuration, never Web fields, source files, screenshots or chat. Confirm the merchant name/city; current defaults are Sakhak Loeung and PHNOM PENH.

Each order has a persisted unique dynamic QR, MD5 identifier, server amount and five-minute expiry. The API checks provider responses for matching receiver, exact USD amount, transaction time and a unique transaction hash. Browser polling uses antiforgery and a server throttle. Repeated starts reuse the same intent; expiry does not automatically create a second payable QR. Manual checks can reconcile a transfer made inside the original time window after expiry.

**TestVerified is a sandbox status only.** It never creates a real Payment or marks an order Paid. Real automatic QR settlement still requires approved production access, verified provider behavior, durable payment references, atomic cash-versus-QR handling, separate electronic/cash accounting, late-payment reconciliation and production testing.

| Connection message | Check |
| --- | --- |
| Configure BakongSandbox... | Enable settings/token in the actual API process using the launcher |
| Rejected test credentials | Correct SIT token, full copy without Bearer/quotes, expiry and provider access |
| Receiving account not found | Account must exist in SIT; confirm provider test account details |
| Address already in use | Stop the old API instance before launching the configured one |
| Provider timeout/unavailable | Leave state unresolved and check again; never mark Paid from a screenshot |

An exposed token must be revoked/replaced through the issuing provider. Never troubleshoot by publishing it or decoding it into logs.

Official references: [Bakong Open API](https://sit-api-bakong.nbc.gov.kh/document), [NBC payment integration guide](https://bakong.nbc.gov.kh/en/download/KHQR/integration/QR%20Payment%20Integration.pdf), [NBC KHQR SDK guide](https://bakong.nbc.gov.kh/download/KHQR/integration/KHQR%20SDK%20Document.pdf).

## API overview

Controllers are the authoritative route inventory; see [API controllers](AuraCafé.API/Controllers). Key routes:

| Area | Routes and access |
| --- | --- |
| Authentication | `/api/auth` controller; existing Identity/JWT login |
| Catalog | `/api/products`, `/api/categories`; management operations require Admin |
| Staff | `/api/staff`; Admin management |
| Shifts | `/api/shifts`; Cashier own shift operations and Admin monitoring |
| Orders | `/api/orders`, `/api/orders/my`, `/api/orders/{id}`; existing role/ownership rules |
| Cash payments | POST `/api/payments/cash`; Cashier only |
| Payment lookup | GET `/api/payments/order/{orderId}`; Admin or owning Cashier |
| Payment history | GET `/api/payments`, `/api/payments/{id}`; Admin only |
| Reports | GET `/api/reports/sales?from=yyyy-MM-dd&to=yyyy-MM-dd`; Admin |
| Reconciliation | GET/POST `/api/shifts/{id}/reconciliation`; role/ownership and close rules apply |
| Inventory | `/api/inventory/summary`, `/ingredients`, `/ingredients/{id}`, `/movements`, `/ingredients/{id}/movements`, `/products/{id}/recipe`; Admin |
| Sandbox connection | POST `/api/payments/bakong-sandbox/connection`; Admin |
| Sandbox checkout | GET/POST `/api/payments/bakong-sandbox/orders/{orderId}` and POST `.../{orderId}/check`; owning Cashier |

## Verification

The latest executed feature verification including POS Khmer interface support passed **405 checks** using disposable SQLite and simulated Bakong responses. Both API and Web builds passed with zero warnings/errors. EF reported no model changes pending after the generated migration. Shared-dialog checks validate rendered markup/assets and integration; a headless browser attempt exited before rendering, so interactive dialog/keyboard behavior still requires manual checking. These are historical executed results, not a fresh production audit or proof of successful live Bakong authentication.

The executable suite covers real HTTP/JWT/EF Core/SQLite and Web-to-API calls, permissions, validation, historical references, images, stock movements/recipes, sales/reconciliation, QR settings, antiforgery and sandbox matching/duplicate/expiry behavior. It does not use the working database.

To rerun, build the Web DLL expected by the harness, then run the suite:

```powershell
dotnet restore
dotnet build AuraCafé.API/AuraCafé.API.csproj --no-restore -c CatalogVerification -p:UseAppHost=false
dotnet build AuraCafé.Web/AuraCafé.Web.csproj --no-restore -c CatalogVerification -p:UseAppHost=false
dotnet run --project tests/ProductManagement.Checks --no-restore -c CatalogVerification -p:UseAppHost=false
```

Use project-level builds for the custom CatalogVerification configuration. They avoid replacing running Debug DLLs. If tests report a missing Web DLL, run the Web build above first. NuGet access is required when dependencies are not already cached; no machine-specific temporary package feed is committed.

Lesson 11's full verification checklist remains useful: successful cash payment, change, duplicates, insufficient/zero cash, invalid order, no shift, inactive cashier, ownership, own lookup, Cashier list denial, Admin read access, Admin collection denial, anonymous denial, Cashier UI, server-total integrity, atomic state, timeout reconciliation, antiforgery and Admin UI authorization. Passing this targeted suite is not a claim that every workbook test or deployment scenario has been independently verified.

## Troubleshooting and remaining work

- **API startup/seeding failure:** check configuration keys and database access. Do not copy passwords into bug reports.
- **Login unavailable:** confirm API is running and Web ApiSettings:BaseUrl matches its URL.
- **Schema/table missing:** back up, review migration history, then apply the intended additive migration.
- **Opening float says enter a number:** enter just the numeric USD/KHR amount; explanations/formulas belong outside the input.
- **Stock stays zero:** creating an ingredient does not receive supplies; record Stock In. Incorrect units need a history-aware correction, not relabelling historical movement quantities.
- **Stale UI or QR return button:** restart Web and hard-refresh. QR Return only closes the display; it does not confirm payment.
- **Unknown payment result:** inspect the existing order/payment or original sandbox intent before submitting another transfer.

Still requiring verification/design: interactive browser/mobile/keyboard accessibility; actual receipt printer output; live Bakong SIT authentication and banking-app scans; production TLS/reverse-proxy/secret/logging review; broad payment concurrency/timeout regression; staff-sensitive-change auditing; inventory automatic deductions; refunds/voids/tax/discount rules; and production QR accounting/settlement. Development seeding and in-memory Web sessions need a deployment review. Do not treat successful builds as production certification.

The former `docs` folder was consolidated here. Old source-code copies, phase-by-phase notes and verification logs were removed; current source, migrations, runnable checks and audit/launcher tools remain in their project directories.
