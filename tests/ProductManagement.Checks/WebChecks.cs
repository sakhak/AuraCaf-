using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

internal static class WebChecks
{
    public static async Task RunAsync(string apiUrl, int categoryId, int productId, Action<bool, string> check)
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (!File.Exists(Path.Combine(root.FullName, "AuraCafé.slnx")))
            root = root.Parent ?? throw new InvalidOperationException("Run checks from the solution directory.");
        var webPath = Path.Combine(root.FullName, "AuraCafé.Web");
        var dll = Path.Combine(webPath, "bin", "CatalogVerification", "net10.0", "AuraCafé.Web.dll");
        if (!File.Exists(dll)) throw new InvalidOperationException("Build Web with -c CatalogVerification before running checks.");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = webPath, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add($"http://127.0.0.1:{port}");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["ApiSettings__BaseUrl"] = apiUrl;
        start.Environment["Logging__EventLog__LogLevel__Default"] = "None";
        var keyDirectory = Path.Combine(Path.GetTempPath(), "aura-testkeys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keyDirectory);
        start.Environment["DataProtection__KeyDirectory"] = keyDirectory;
        var imageDirectory = Path.Combine(Path.GetTempPath(), "aura-testimages-" + Guid.NewGuid().ToString("N"));
        start.Environment["ProductImages__Directory"] = imageDirectory;
        var qrDirectory = Path.Combine(Path.GetTempPath(), "aura-testqr-" + Guid.NewGuid().ToString("N"));
        start.Environment["MerchantQr__Directory"] = qrDirectory;
        using var process = Process.Start(start)!;
        var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) logs.Enqueue(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) logs.Enqueue(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
        using var browser = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    using var ready = await browser.GetAsync("/Account/Login");
                    if (ready.IsSuccessStatusCode) break;
                    if (attempt >= 3) throw new Exception("Web login returned " + ready.StatusCode + ": " + string.Join(Environment.NewLine, logs.TakeLast(50)));
                }
                catch (HttpRequestException) when (attempt < 10) { }
                if (process.HasExited || attempt >= 9) throw new Exception("Web test host did not become ready: " + string.Join(Environment.NewLine, logs.TakeLast(50)));
                await Task.Delay(100);
            }
            var anonymous = await browser.GetAsync("/Admin/Products");
            check(anonymous.StatusCode == HttpStatusCode.Redirect && anonymous.Headers.Location!.ToString().Contains("/Account/Login"), "Anonymous product page redirects to login");
            static string Token(string html)
            {
                var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
                if (!match.Success) throw new Exception("No Razor antiforgery token rendered.");
                return WebUtility.HtmlDecode(match.Groups[1].Value);
            }
            async Task Login(string role)
            {
                var html = await browser.GetStringAsync("/Account/Login");
                var response = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = Token(html), ["Input.Email"] = role + "@test.invalid", ["Input.Password"] = "TestOnly!Password123"
                }));
                check(response.StatusCode == HttpStatusCode.Redirect, role + " can log in through Web → API");
            }
            await Login("Admin");
            var dialogAsset = await browser.GetStringAsync("/js/dialogs.js");
            var dialogStyles = await browser.GetStringAsync("/css/dialogs.css");
            check(dialogAsset.Contains("form.requestSubmit(submitter || undefined)") && dialogAsset.Contains("event.stopImmediatePropagation()"), "Shared confirmation preserves original form submission after approval");
            check(dialogAsset.Contains("previousFocus?.focus()") && dialogAsset.Contains("message.textContent"), "Shared dialog restores focus and renders messages as text");
            check(dialogStyles.Contains("var(--aura-primary)") && dialogStyles.Contains("::backdrop"), "Portal dialog stylesheet inherits Admin and Cashier themes");
            var staffDirectory = await browser.GetStringAsync("/Admin/Staff");
            check(Regex.Matches(staffDirectory, "id=\"auraActionDialog\"").Count == 1 && staffDirectory.Contains("/js/dialogs"), "Admin layout includes one reusable accessible confirmation dialog");
            check(staffDirectory.Contains("admin-theme") && staffDirectory.Contains("Your team") && staffDirectory.Contains("Linked accounts"), "Green Admin theme and real staff directory render");
            check((await browser.GetAsync("/css/design-system.css")).IsSuccessStatusCode && (await browser.GetAsync("/css/staff.css")).IsSuccessStatusCode,
                "Admin design system and staff stylesheet are served");
            var newEmployee = await browser.GetStringAsync("/Admin/Staff/Create");
            var employeeValues = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(newEmployee), ["Staff.FullName"] = "Web Barista", ["Staff.Email"] = "webbarista@test.invalid",
                ["Staff.PhoneNumber"] = "", ["Staff.Position"] = "Cashier", ["Staff.HireDate"] = "2026-01-01"
            };
            var missingPassword = await browser.PostAsync("/Admin/Staff/Create", new FormUrlEncodedContent(employeeValues));
            check(missingPassword.StatusCode == HttpStatusCode.OK && (await missingPassword.Content.ReadAsStringAsync()).Contains("A password is required for Cashiers"), "Staff form enforces existing cashier password requirement");
            employeeValues["Staff.Position"] = "Barista";
            var createdEmployee = await browser.PostAsync("/Admin/Staff/Create", new FormUrlEncodedContent(employeeValues));
            check(createdEmployee.StatusCode == HttpStatusCode.Redirect, "Staff create supports Barista without password or phone");
            var filteredTeam = await browser.GetStringAsync("/Admin/Staff?Search=Web%20Barista&Position=Barista&Status=Active");
            var employeeId = Regex.Match(filteredTeam, "/Admin/Staff/Edit/(\\d+)").Groups[1].Value;
            check(filteredTeam.Contains("1 matching employees") && !string.IsNullOrEmpty(employeeId), "Staff search and position/status filters combine using persisted records");
            var staffEdit = await browser.GetStringAsync("/Admin/Staff/Edit/" + employeeId);
            var employeeCode = Regex.Match(staffEdit, "EMP-\\d+").Value;
            var editEmployeeValues = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(staffEdit), ["Staff.FullName"] = "", ["Staff.PhoneNumber"] = "", ["Staff.Position"] = "Barista", ["Staff.HireDate"] = "2026-01-02"
            };
            var invalidEmployee = await browser.PostAsync("/Admin/Staff/Edit/" + employeeId, new FormUrlEncodedContent(editEmployeeValues));
            var invalidEmployeeHtml = await invalidEmployee.Content.ReadAsStringAsync();
            check(invalidEmployee.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(employeeCode) && invalidEmployeeHtml.Contains(employeeCode), "Staff validation retains immutable employee code");
            editEmployeeValues["Staff.FullName"] = "Web Barista Updated";
            check((await browser.PostAsync("/Admin/Staff/Edit/" + employeeId, new FormUrlEncodedContent(editEmployeeValues))).StatusCode == HttpStatusCode.Redirect, "Staff edit preserves optional phone and updates profile");
            var updatedEmployee = await browser.GetStringAsync("/Admin/Staff?Search=Web%20Barista");
            check((await browser.PostAsync("/Admin/Staff?handler=Status&id=" + employeeId + "&active=false", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(updatedEmployee) }))).StatusCode == HttpStatusCode.Redirect,
                "Staff deactivate action uses existing API service");
            var inactiveEmployee = await browser.GetStringAsync("/Admin/Staff?Search=Web%20Barista&Status=Inactive");
            check(inactiveEmployee.Contains("Web Barista Updated") && inactiveEmployee.Contains("1 matching employees") && inactiveEmployee.Contains(">Inactive</span>"), "Staff inactive filter shows actual employment status");
            check((await browser.PostAsync("/Admin/Staff?handler=Status&id=" + employeeId + "&active=true", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(inactiveEmployee) }))).StatusCode == HttpStatusCode.Redirect,
                "Staff reactivation works through protected form");
            var emptyStaff = await browser.GetStringAsync("/Admin/Staff?Search=NoMatchingStaff");
            check(emptyStaff.Contains("No employees found") && emptyStaff.Contains("0 matching employees"), "Staff directory renders honest empty search state");
            check((await browser.PostAsync("/Admin/Staff?handler=Status&id=" + employeeId + "&active=false", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest,
                "Staff account-status form requires antiforgery token");
            foreach (var categoryRoute in new[] { "/Categories", "/Admin/Categories" })
            {
                var response = await browser.GetAsync(categoryRoute);
                var html = await response.Content.ReadAsStringAsync();
                check(response.IsSuccessStatusCode && html.Contains("Your categories") && html.Contains("Products organized"), "Category redesigned route renders real management UI: " + categoryRoute);
            }
            var categoryPage = await browser.GetStringAsync("/Admin/Categories");
            var invalidCategory = await browser.PostAsync("/Admin/Categories", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(categoryPage), ["Name"] = "   " }));
            check(invalidCategory.StatusCode == HttpStatusCode.OK && (await invalidCategory.Content.ReadAsStringAsync()).Contains("field-validation-error"), "Category form displays server validation");
            var categoryCreated = await browser.PostAsync("/Admin/Categories", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(categoryPage), ["Name"] = "Web Drinks" }));
            check(categoryCreated.StatusCode == HttpStatusCode.Redirect, "Category create works through Razor/API form");
            var categoryResults = await browser.GetStringAsync("/Admin/Categories?Search=Web%20Drinks");
            var categoryIdForEdit = Regex.Match(categoryResults, "EditId=(\\d+)").Groups[1].Value;
            check(!string.IsNullOrEmpty(categoryIdForEdit) && categoryResults.Contains("Web Drinks"), "Category search renders persisted category with Edit action");
            var categoryEdit = await browser.GetStringAsync("/Admin/Categories?EditId=" + categoryIdForEdit);
            check(categoryEdit.Contains("value=\"Web Drinks\"") && categoryEdit.Contains("Save changes"), "Category editor loads saved name");
            var renamedCategory = await browser.PostAsync("/Admin/Categories", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(categoryEdit), ["EditId"] = categoryIdForEdit, ["Name"] = "Web Beverages" }));
            check(renamedCategory.StatusCode == HttpStatusCode.Redirect, "Category rename works through Razor/API form");
            var renamedResults = await browser.GetStringAsync("/Admin/Categories?Search=Web%20Beverages");
            check(renamedResults.Contains("Web Beverages"), "Category renamed value persists in search");
            check((await browser.PostAsync("/Admin/Categories?handler=Delete&id=" + categoryIdForEdit, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest,
                "Category deletion requires Razor antiforgery token");
            check((await browser.PostAsync("/Admin/Categories?handler=Delete&id=" + categoryIdForEdit, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(renamedResults) }))).StatusCode == HttpStatusCode.Redirect,
                "Unused category deletes through protected Razor form");
            var protectedCategory = await browser.GetStringAsync("/Admin/Categories?Search=Meals");
            check(protectedCategory.Contains("View products") && !protectedCategory.Contains("data-category-delete"), "Categories with products show product navigation instead of Delete");
            var inventoryPage = await browser.GetStringAsync("/Admin/Inventory");
            check(inventoryPage.Contains("Inventory Overview") && !inventoryPage.Contains("Inventory request failed"), "Inventory Admin overview loads real counts and ingredients");
            var attentionInventory = await browser.GetStringAsync("/Admin/Inventory?LowOnly=true&Search=Coffee");
            check(attentionInventory.Contains("Coffee Beans") && attentionInventory.Contains("1 matching ingredients") && !attentionInventory.Contains(">Sugar<"),
                "Inventory needs-attention quick filter combines with real ingredient search");
            var noInventoryMatches = await browser.GetStringAsync("/Admin/Inventory?Search=NoMatchingIngredient");
            check(noInventoryMatches.Contains("No ingredients found") && noInventoryMatches.Contains("0 matching ingredients"), "Inventory redesign displays honest filtered empty state");
            var ingredientForm = await browser.GetStringAsync("/Admin/Inventory/Edit");
            var ingredientValues = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(ingredientForm), ["Input.Name"] = "Web Flour", ["Input.Unit"] = "g", ["Input.MinimumQuantity"] = "100", ["Input.IsActive"] = "true"
            };
            check((await browser.PostAsync("/Admin/Inventory/Edit", new FormUrlEncodedContent(ingredientValues))).StatusCode == HttpStatusCode.Redirect, "Inventory ingredient created through Razor/API flow");
            var flourList = await browser.GetStringAsync("/Admin/Inventory?Search=Web%20Flour");
            var flourId = Regex.Match(flourList, "/Admin/Inventory/Edit/(\\d+)").Groups[1].Value;
            check(!string.IsNullOrEmpty(flourId) && flourList.Contains("Out of stock"), "Inventory newly created ingredient renders zero-stock alert");
            var moveUrl = "/Admin/Inventory/Movements/" + flourId;
            var movePage = await browser.GetStringAsync(moveUrl);
            var requestId = Regex.Match(movePage, "name=\"Input.RequestId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var moveValues = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(movePage), ["Input.RequestId"] = requestId, ["Input.Type"] = "StockIn", ["Input.Quantity"] = "500", ["Input.Reason"] = "Receive web flour"
            };
            check((await browser.PostAsync(moveUrl, new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Quantity"] = "500" }))).StatusCode == HttpStatusCode.BadRequest, "Inventory stock form requires antiforgery token");
            check((await browser.PostAsync(moveUrl, new FormUrlEncodedContent(moveValues))).StatusCode == HttpStatusCode.Redirect, "Inventory stock-in submitted through Razor/API flow");
            check((await browser.PostAsync(moveUrl, new FormUrlEncodedContent(moveValues))).StatusCode == HttpStatusCode.Redirect, "Inventory browser retry safely confirms same request");
            var flourLedger = await browser.GetStringAsync(moveUrl);
            check(Regex.Matches(flourLedger, "Receive web flour").Count == 1 && flourLedger.Contains("500 g"), "Inventory browser retry creates one ledger row and one balance change");
            var recipeUrl = "/Admin/Inventory/Recipes/" + productId;
            var recipePage = await browser.GetStringAsync(recipeUrl);
            var recipeForm = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(recipePage), ["IngredientId"] = flourId, ["QuantityRequired"] = "18"
            };
            check((await browser.PostAsync(recipeUrl + "?handler=Save", new FormUrlEncodedContent(recipeForm))).StatusCode == HttpStatusCode.Redirect, "Inventory recipe ingredient saved through Razor/API flow");
            recipePage = await browser.GetStringAsync(recipeUrl);
            check(recipePage.Contains("Web Flour") && recipePage.Contains("18 g"), "Inventory recipe displays persisted quantity and unit");
            check((await browser.PostAsync(recipeUrl + "?handler=Clear", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(recipePage) }))).StatusCode == HttpStatusCode.Redirect,
                "Inventory recipe clear preserves stock and succeeds through Razor");
            var flourOverview = await browser.GetStringAsync("/Admin/Inventory?Search=Web%20Flour");
            var deactivateFlour = await browser.PostAsync("/Admin/Inventory?handler=Deactivate&id=" + flourId,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(flourOverview) }));
            check(deactivateFlour.StatusCode == HttpStatusCode.Redirect, "Inventory row action deactivates through protected Razor form");
            var inactiveFlour = await browser.GetStringAsync("/Admin/Inventory?Search=Web%20Flour&Active=false");
            check(inactiveFlour.Contains("Web Flour") && inactiveFlour.Contains("Inactive") && inactiveFlour.Contains("500"), "Inventory inactive filter preserves ingredient balance in redesigned list");
            var inactiveLedger = await browser.GetStringAsync(moveUrl);
            check(inactiveLedger.Contains("Reactivate it before recording movements") && inactiveLedger.Contains("Receive web flour"), "Inventory inactive state retains ledger and blocks new movement form");
            var flourEdit = await browser.GetStringAsync("/Admin/Inventory/Edit/" + flourId);
            var reactivateFlour = await browser.PostAsync("/Admin/Inventory/Edit/" + flourId, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(flourEdit), ["Input.Name"] = "Web Flour", ["Input.Unit"] = "g", ["Input.MinimumQuantity"] = "100", ["Input.IsActive"] = "true"
            }));
            check(reactivateFlour.StatusCode == HttpStatusCode.Redirect && (await browser.GetStringAsync(moveUrl)).Contains("500 g"), "Inventory edit/reactivation keeps stock unchanged");
            var reportPage = await browser.GetStringAsync("/Admin/Reports?From=2026-11-14&To=2026-11-14");
            var expectedRevenue = "USD " + (((long)int.MaxValue + 201 + 325 + 3) / 100m).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
            check(reportPage.Contains(expectedRevenue) && reportPage.Contains("Sales by collecting cashier"), "Lesson12 Razor report displays real aggregate and cashier breakdown");
            check(reportPage.Contains("Last 7 days") && reportPage.Contains("Export CSV") && reportPage.Contains("printSalesReport"), "Sales report exposes date presets, export and print controls");
            check(reportPage.Contains("Boundary months cover only dates inside") && reportPage.Contains("Bakong test confirmations are excluded"), "Sales report explains partial months and cash-only revenue");
            var exportedSales = await browser.GetAsync("/Admin/Reports?handler=Export&From=2026-11-14&To=2026-11-14");
            var salesCsv = await exportedSales.Content.ReadAsStringAsync();
            check(exportedSales.IsSuccessStatusCode && exportedSales.Content.Headers.ContentType?.MediaType == "text/csv"
                && exportedSales.Content.Headers.ContentDisposition!.ToString().Contains("aura-sales-2026-11-14-to-2026-11-14.csv"), "Sales CSV downloads with selected date range and content type");
            check(salesCsv.Contains(expectedRevenue.Replace("USD ", "").Replace(",", "")) && salesCsv.Contains("ProductTop20") && salesCsv.Contains("Summary"), "Sales CSV uses actual report totals and clearly typed breakdown rows");
            check(salesCsv.Contains("\"'=1+1,\"\"Cafe\"\"\nTeam (#"), "Sales CSV quotes commas/newlines/quotes and neutralizes spreadsheet formula names");
            check(exportedSales.Headers.CacheControl?.NoStore == true, "Sales CSV is not cached as public financial data");
            check((await browser.GetAsync("/Admin/Reports?handler=Export&From=2026-11-15&To=2026-11-14")).StatusCode == HttpStatusCode.BadRequest, "Sales CSV rejects invalid date ranges");
            var emptySales = await browser.GetStringAsync("/Admin/Reports?From=1900-01-01&To=1900-01-02");
            check(emptySales.Contains("No completed payments in this period") && emptySales.Contains("No cashier sales yet"), "Sales report empty period retains clear honest empty states");
            var badSales = await browser.GetStringAsync("/Admin/Reports?From=2026-11-16&To=2026-11-15");
            check(badSales.Contains("Choose a valid date range") && !badSales.Contains("report-kpis"), "Invalid report dates do not display invented metrics");
            var dashboard = await browser.GetStringAsync("/Admin/Dashboard");
            check(Regex.Matches(dashboard, "class=\"dash-chart-column\"").Count == 7 && dashboard.Contains("View daily sales figures"), "Admin dashboard renders seven real daily revenue columns and accessible figures");
            check(!dashboard.Contains("seven-day sales view is unavailable") && dashboard.Contains("Opening floats and unpaid orders are excluded"), "Admin dashboard weekly reporting loads with correct revenue guidance");
            check(dashboard.Contains("Today's revenue") && dashboard.Contains("Pending orders now") && !dashboard.Contains("Not connected"), "Lesson12 dashboard loads database-backed metrics");
            var shiftDirectory = await browser.GetStringAsync("/Admin/Shifts");
            check(shiftDirectory.Contains("Summary cards cover the loaded recent records") && shiftDirectory.Contains("Opening drawer"), "Shift directory labels recent-record scope and separate opening currencies");
            check(Regex.Matches(shiftDirectory, "aria-label=\"View drawer for ").Count is > 0 and <= 12, "Shift directory pages real records to twelve rows");
            var openShifts = await browser.GetStringAsync("/Admin/Shifts?Status=Open&Search=TEST-1");
            check(openShifts.Contains("shift-status is-open") && !openShifts.Contains("shift-status is-closed"), "Shift status and staff-code search combine on real recent records");
            var closedShifts = await browser.GetStringAsync("/Admin/Shifts?Status=Closed");
            check(closedShifts.Contains("shift-status is-closed") && !closedShifts.Contains("shift-status is-open"), "Closed shift filter excludes working shifts");
            check((await browser.GetStringAsync("/Admin/Shifts?Search=unmatched-shift-xyz")).Contains("No matching shifts"), "Shift search shows useful empty state");
            check((await browser.GetStringAsync("/Admin/Shifts?From=1900-01-01&To=1900-01-02")).Contains("No matching shifts"), "Shift local clock-in date filters exclude other days");
            check((await browser.GetStringAsync("/Admin/Shifts?From=2026-11-16&To=2026-11-15")).Contains("From must be on or before To"), "Shift invalid date range rejected clearly");
            check((await browser.GetStringAsync("/Admin/Shifts?PageNumber=0")).Contains("Check the search"), "Shift invalid pagination rejected");
            var adminCash = await browser.GetAsync($"/Admin/Shifts/Reconciliation/{productId}");
            check(adminCash.IsSuccessStatusCode, "Lesson12 Admin can view read-only drawer summary");
            var drawerMarkup = await adminCash.Content.ReadAsStringAsync();
            check(drawerMarkup.Contains("US dollars") && drawerMarkup.Contains("Khmer riel") && drawerMarkup.Contains("Read-only review"), "Admin drawer review separates USD/KHR and retains read-only behavior");
            foreach (var route in new[] { "/Admin/Staff", "/Admin/Shifts", "/Admin/Payments" })
            {
                var response = await browser.GetAsync(route);
                var html = await response.Content.ReadAsStringAsync();
                check(response.StatusCode == HttpStatusCode.OK && !html.Contains("Unable to load", StringComparison.OrdinalIgnoreCase),
                    "Admin integration page/API client resolves: " + route);
            }
            foreach (var route in new[] { "/Admin/Products", "/Products", "/Admin/Products/Create", "/Products/Create", $"/Admin/Products/Edit/{productId}", $"/Products/Edit/{productId}" })
            {
                var response = await browser.GetAsync(route);
                check(response.StatusCode == HttpStatusCode.OK, "Admin product route works: " + route);
            }
            var page = await browser.GetStringAsync("/Admin/Products/Create");
            check(page.Contains("data-image-upload") && page.Contains("multipart/form-data") && page.Contains("Product.IsActive"), "Product form renders computer file picker and active controls");
            var noToken = await browser.PostAsync("/Admin/Products/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Product.Name"] = "Forged" }));
            check(noToken.StatusCode == HttpStatusCode.BadRequest, "Product Razor POST requires antiforgery token");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(page), ["Product.Name"] = "Web Form Coffee",
                ["Product.Description"] = "", ["Product.ImageUrl"] = "", ["Product.PriceCents"] = "450",
                ["Product.CategoryId"] = categoryId.ToString(), ["Product.IsActive"] = "true", ["Product.IsAvailable"] = "true"
            };
            var save = await browser.PostAsync("/Admin/Products/Create", new FormUrlEncodedContent(form));
            check(save.StatusCode == HttpStatusCode.Redirect, "Razor create saves through API with optional blank image/description");
            var list = await browser.GetStringAsync("/Admin/Products?Search=Web%20Form");
            check(list.Contains("Web Form Coffee") && list.Contains("1 matching products"), "Razor search renders actual persisted product");
            var uploadPage = await browser.GetStringAsync("/Admin/Products/Create");
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6+QAAAABJRU5ErkJggg==");
            var qrSettings = await browser.GetStringAsync("/Admin/Settings/PaymentQr");
            var probe = await browser.PostAsync("/Admin/Settings/PaymentQr?handler=Connection", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = Token(qrSettings) }));
            check(probe.StatusCode == HttpStatusCode.Redirect && (await browser.GetStringAsync("/Admin/Settings/PaymentQr")).Contains("Connected to Bakong sandbox"), "Admin can test Bakong connection through Web without entering credentials in browser");
            check((await browser.PostAsync("/Admin/Settings/PaymentQr?handler=Connection", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest, "Admin Bakong connection probe requires antiforgery");
            check((await browser.GetAsync("/Cashier/Payments/Sandbox/1")).Headers.Location!.ToString().Contains("/Account/Denied"), "Admin cannot use Cashier sandbox checkout");
            check(qrSettings.Contains("No merchant QR saved yet"), "Merchant QR starts unconfigured in isolated storage");
            check((await browser.PostAsync("/Admin/Settings/PaymentQr", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest,
                "Merchant QR upload requires antiforgery");
            async Task<HttpResponseMessage> UploadQr(byte[] bytes, string mime, string name)
            {
                var settings = await browser.GetStringAsync("/Admin/Settings/PaymentQr");
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(Token(settings)), "__RequestVerificationToken");
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
                form.Add(content, "QrImage", name);
                return await browser.PostAsync("/Admin/Settings/PaymentQr", form);
            }
            check((await UploadQr(png, "image/png", "merchant.png")).StatusCode == HttpStatusCode.Redirect, "Admin saves merchant QR from computer");
            var savedQrPage = await browser.GetStringAsync("/Admin/Settings/PaymentQr");
            var savedQrUrl = Regex.Match(savedQrPage, "/product-images/[a-f0-9]{32}\\.png").Value;
            check(savedQrUrl.Length > 0 && (await browser.GetByteArrayAsync(savedQrUrl)).SequenceEqual(png), "Saved QR preserves exact original image bytes");
            var persistedQr = await File.ReadAllTextAsync(Path.Combine(qrDirectory, "current.json"));
            check(persistedQr.Contains(savedQrUrl) && persistedQr.Contains("UpdatedBy"), "Merchant QR settings persist on disk with update attribution");
            var badQr = await UploadQr(System.Text.Encoding.UTF8.GetBytes("<svg></svg>"), "image/svg+xml", "bad.svg");
            check(badQr.StatusCode == HttpStatusCode.OK && (await browser.GetStringAsync("/Admin/Settings/PaymentQr")).Contains(savedQrUrl), "Unsupported QR image rejected without replacing saved QR");
            check((await UploadQr(png, "image/png", "replacement.png")).StatusCode == HttpStatusCode.Redirect, "Admin can replace merchant QR");
            check(!(await browser.GetStringAsync("/Admin/Settings/PaymentQr")).Contains(savedQrUrl), "Future checkout settings use replacement QR");
            async Task<HttpResponseMessage> Upload(byte[] bytes, string mime, string filename, string productName)
            {
                using var content = new MultipartFormDataContent();
                foreach (var pair in new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = Token(uploadPage), ["Product.Name"] = productName,
                    ["Product.Description"] = "", ["Product.PriceCents"] = "500", ["Product.CategoryId"] = categoryId.ToString(),
                    ["Product.IsAvailable"] = "true", ["Product.IsActive"] = "true"
                }) content.Add(new StringContent(pair.Value), pair.Key);
                var file = new ByteArrayContent(bytes);
                file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
                content.Add(file, "ImageUpload", filename);
                return await browser.PostAsync("/Admin/Products/Create", content);
            }
            var invalidUpload = await Upload(System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"), "image/svg+xml", "unsafe.svg", "Rejected SVG");
            check(invalidUpload.StatusCode == HttpStatusCode.OK && (await invalidUpload.Content.ReadAsStringAsync()).Contains("SVG and other file types are not supported"), "Upload rejects SVG content");
            var forgedUpload = await Upload(System.Text.Encoding.UTF8.GetBytes("<html>not a PNG</html>"), "image/png", "fake.png", "Rejected forged image");
            check((await forgedUpload.Content.ReadAsStringAsync()).Contains("SVG and other file types are not supported"), "Upload checks file signature rather than filename alone");
            var oversized = await Upload(new byte[5 * 1024 * 1024 + 1], "image/png", "large.png", "Rejected large image");
            check((await oversized.Content.ReadAsStringAsync()).Contains("no larger than 5 MB"), "Upload rejects files above 5 MB");
            var uploaded = await Upload(png, "image/png", "../../photo.png", "Uploaded POS Coffee");
            check(uploaded.StatusCode == HttpStatusCode.Redirect, "Product image uploads from computer through multipart Razor form");
            var uploadedList = await browser.GetStringAsync("/Admin/Products?Search=Uploaded%20POS%20Coffee");
            var imageUrl = Regex.Match(uploadedList, "/product-images/[a-f0-9]{32}\\.png").Value;
            check(!string.IsNullOrEmpty(imageUrl), "Uploaded image receives generated safe local URL");
            var uploadedProductId = Regex.Match(uploadedList, "/Admin/Products/Edit/(\\d+)").Groups[1].Value;
            var uploadedEdit = await browser.GetStringAsync("/Admin/Products/Edit/" + uploadedProductId);
            check(uploadedEdit.Contains(imageUrl) && uploadedEdit.Contains("data-current-image"), "Product edit shows current saved image preview");
            var editWithoutFile = await browser.PostAsync("/Admin/Products/Edit/" + uploadedProductId, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(uploadedEdit), ["Product.Name"] = "Uploaded POS Coffee",
                ["Product.Description"] = "Updated without replacing photo", ["Product.PriceCents"] = "500",
                ["Product.CategoryId"] = categoryId.ToString(), ["Product.IsActive"] = "true", ["Product.IsAvailable"] = "true"
            }));
            check(editWithoutFile.StatusCode == HttpStatusCode.Redirect
                && (await browser.GetStringAsync("/Admin/Products?Search=Uploaded%20POS%20Coffee")).Contains(imageUrl), "Editing product without a file retains its uploaded image");
            using (var anonymousImages = new HttpClient { BaseAddress = browser.BaseAddress })
            {
                var imageResponse = await anonymousImages.GetAsync(imageUrl);
                check(imageResponse.IsSuccessStatusCode && imageResponse.Content.Headers.ContentType?.MediaType == "image/png"
                    && (await imageResponse.Content.ReadAsByteArrayAsync()).SequenceEqual(png), "Uploaded image is served correctly without API-origin confusion");
                check(imageResponse.Headers.GetValues("X-Content-Type-Options").Contains("nosniff"), "Uploaded image served with nosniff header");
                check((await anonymousImages.GetAsync("/product-images/invalid-name.png")).StatusCode == HttpStatusCode.NotFound, "Image handler rejects non-generated paths");
            }
            var logout = await browser.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(list) }));
            check(logout.StatusCode == HttpStatusCode.Redirect, "Admin logout works");
            await Login("Cashier");
            var cashierHome = await browser.GetStringAsync("/Cashier/Dashboard");
            check(Regex.Matches(cashierHome, "id=\"auraActionDialog\"").Count == 1 && cashierHome.Contains("aria-describedby=\"auraDialogMessage\""), "Cashier layout includes shared labelled confirmation dialog");
            check(cashierHome.Contains("portal-body cashier-theme") && cashierHome.Contains("Cashier corner") && cashierHome.Contains("Open POS"), "Pink Cashier portal renders friendly dashboard with real workflow links");
            check(cashierHome.Contains("Your recent orders") && cashierHome.Contains("Ready for the day?") && !cashierHome.Contains("could not be loaded") && !cashierHome.Contains("status is unavailable"), "Cashier dashboard loads ownership-protected recent orders and current shift");
            foreach (var staffRoute in new[] { "/Admin/Staff", "/Admin/Staff/Create", "/Admin/Staff/Edit/" + employeeId })
            {
                var response = await browser.GetAsync(staffRoute);
                check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString().Contains("/Account/Denied"), "Cashier denied staff administration: " + staffRoute);
            }
            foreach (var categoryRoute in new[] { "/Categories", "/Admin/Categories" })
            {
                var response = await browser.GetAsync(categoryRoute);
                check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString().Contains("/Account/Denied"), "Cashier denied category management page: " + categoryRoute);
            }
            check((await browser.GetAsync("/Admin/Settings/PaymentQr")).Headers.Location!.ToString().Contains("/Account/Denied"), "Cashier cannot change saved merchant QR");
            var posWithImage = await browser.GetStringAsync("/Cashier/POS");
            check(posWithImage.Contains(imageUrl) && posWithImage.Contains("data-pos-image"), "Cashier POS renders uploaded product image instead of icon only");
            check(posWithImage.Contains("id=\"posLanguage\"") && posWithImage.Contains("value=\"km\" lang=\"km\""), "POS renders accessible English/Khmer language selector");
            var posLanguageScript = await browser.GetStringAsync("/js/pos-language.js");
            check(posLanguageScript.Contains("\u1785\u17c6\u178e\u17bb\u1785\u179b\u1780\u17cb") && posLanguageScript.Contains("aura.posLanguage"), "POS serves UTF-8 Khmer translations and browser language preference support");
            check(posLanguageScript.Contains("data-pos-no-translate") && posWithImage.Contains("data-pos-no-translate>Uploaded POS Coffee"), "POS preserves database product names instead of translating catalog data");
            check(posLanguageScript.Contains("aura:pos-language-changed") && posLanguageScript.Contains("document.documentElement.lang"), "POS language changes expose accessible language metadata and cart refresh event");

            check(posWithImage.Contains("data-category-filter=\"all\"") && posWithImage.Contains("data-category-filter=\"" + categoryId + "\"") && posWithImage.Contains("aria-pressed=\"true\""), "POS category navigation uses actual API categories with accessible active state");
            check(Regex.Matches(posWithImage,"id=\"checkoutForm\"").Count == 1 && posWithImage.Contains("id=\"posCartDialog\"") && posWithImage.Contains("aria-controls=\"posCartDialog\""), "Responsive POS uses a single order form and accessible mobile cart dialog");
            check(posWithImage.Contains("Order estimate") && posWithImage.Contains("final total confirmed at checkout") && posWithImage.Contains("Saved QR") && posWithImage.Contains("Display only"), "POS clearly distinguishes order estimate, cash checkout and display-only QR");
            check(posWithImage.Contains("id=\"noProductMatches\"") && posWithImage.Contains("data-product-cart-count"), "POS includes filter empty state and in-cart product indicators");

            check(posWithImage.Contains("name=\"ProceedToPayment\" value=\"true\"") && posWithImage.Contains("Payment stays pending until verified."), "POS checkout continues to payment and describes QR as pending until verified");
            var qrCheckoutScript = await browser.GetStringAsync("/js/checkout.js");
            check(qrCheckoutScript.Contains("selectMethod(true)") && !qrCheckoutScript.Contains("createObjectURL") && qrCheckoutScript.Contains("dialog.showModal()"), "QR checkout automatically displays saved image without cashier file selection");
            var receiptStyles = await browser.GetStringAsync("/css/receipt.css");
            check(receiptStyles.Contains("@media print") && receiptStyles.Contains("72mm"), "Customer receipt has thermal print stylesheet");
            foreach (var inventoryRoute in new[] { "/Admin/Inventory", "/Admin/Inventory/Edit", "/Admin/Inventory/Movements", "/Admin/Inventory/Recipes" })
            {
                var deniedInventory = await browser.GetAsync(inventoryRoute);
                check(deniedInventory.StatusCode == HttpStatusCode.Redirect && deniedInventory.Headers.Location!.ToString().Contains("/Account/Denied"), "Inventory Cashier page denied: " + inventoryRoute);
            }
            check((await browser.GetAsync("/Admin/Reports")).StatusCode == HttpStatusCode.Redirect, "Lesson12 Cashier denied Admin report page");
            check((await browser.GetAsync("/Admin/Reports?handler=Export&From=2026-11-14&To=2026-11-14")).Headers.Location!.ToString().Contains("/Account/Denied"), "Cashier denied sales CSV export");
            foreach (var route in new[] { "/Cashier/POS", "/Cashier/MyShift", "/Cashier/Orders", $"/Cashier/Payments/Create/{productId}" })
            {
                var response = await browser.GetAsync(route);
                var html = await response.Content.ReadAsStringAsync();
                check(response.StatusCode == HttpStatusCode.OK && !html.Contains("Unable to load", StringComparison.OrdinalIgnoreCase),
                    "Cashier integration page/API client resolves: " + route);
            }
            foreach (var route in new[] { "/Admin/Products", "/Products", "/Admin/Products/Create", $"/Admin/Products/Edit/{productId}" })
            {
                var response = await browser.GetAsync(route);
                check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString().Contains("/Account/Denied"), "Cashier denied product page: " + route);
            }
            var shiftPage = await browser.GetStringAsync("/Cashier/MyShift");
            var shiftId = Regex.Match(shiftPage, "name=\"ClosingShiftId\" value=\"(\\d+)\"").Groups[1].Value;
            check(!string.IsNullOrEmpty(shiftId), "Lesson12 drawer count form targets current shift");
            var forgedClose = await browser.PostAsync("/Cashier/MyShift?handler=CloseAndReconcile", new FormUrlEncodedContent(new Dictionary<string, string> { ["ClosingShiftId"] = shiftId, ["CountedCash"] = "1.23" }));
            check(forgedClose.StatusCode == HttpStatusCode.BadRequest, "Lesson12 drawer count form requires antiforgery");
            var closeForm = new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(shiftPage), ["ClosingShiftId"] = shiftId, ["CountedCash"] = "1.23" };
            var closeResult = await browser.PostAsync("/Cashier/MyShift?handler=CloseAndReconcile", new FormUrlEncodedContent(closeForm));
            check(closeResult.StatusCode == HttpStatusCode.Redirect, "Lesson12 Cashier closes shift through Razor/API flow");
            var closed = await browser.GetStringAsync("/Cashier/MyShift?ShiftId=" + shiftId);
            check(closed.Contains("USD 1.23") && closed.Contains("Recorded"), "Lesson12 confirmed drawer difference renders after redirect");
            var invalidOpening = await browser.PostAsync("/Cashier/MyShift?handler=ClockIn", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(closed), ["OpeningCash"] = "$50 opening + $75 sales = $125 expected"
            }));
            var invalidHtml = await invalidOpening.Content.ReadAsStringAsync();
            check(invalidOpening.StatusCode == HttpStatusCode.OK && invalidHtml.Contains("validation-summary-errors") && !invalidHtml.Contains("The Note field is required"),
                "Opening float rejects a formula without requiring unrelated Note");
            var clockIn = await browser.PostAsync("/Cashier/MyShift?handler=ClockIn", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(invalidHtml), ["OpeningCash"] = "50.00", ["OpeningRiel"] = "200000"
            }));
            check(clockIn.StatusCode == HttpStatusCode.Redirect, "Clock-in accepts numeric opening float with Note omitted");
            var openedHtml = await browser.GetStringAsync("/Cashier/MyShift");
            check(openedHtml.Contains("USD 50.00") && !openedHtml.Contains("The Note field is required"), "Opening float persists as USD 50.00");
            check(openedHtml.Contains("KHR 200,000"), "KHR opening float persists separately without USD conversion");
            var rielShiftId = Regex.Match(openedHtml, "name=\"ClosingShiftId\" value=\"(\\d+)\"").Groups[1].Value;
            var checkoutPage = await browser.GetStringAsync("/Cashier/POS");
            var qrOrder = await browser.PostAsync("/Cashier/POS?handler=Checkout", new FormUrlEncodedContent(new Dictionary<string,string>
            {
                ["__RequestVerificationToken"] = Token(checkoutPage), ["ProceedToPayment"] = "true", ["PaymentChoice"] = "qr",
                ["CartJson"] = "[{\"productId\":" + uploadedProductId + ",\"quantity\":1}]"
            }));
            check(qrOrder.StatusCode == HttpStatusCode.Redirect && qrOrder.Headers.Location!.ToString().Contains("method=qr"), "POS QR order redirects directly to QR checkout");
            var qrPaymentPage = await browser.GetStringAsync(qrOrder.Headers.Location);
            var activeQrSettings = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(qrDirectory, "current.json")));
            var activeQrUrl = activeQrSettings.RootElement.GetProperty("ImageUrl").GetString()!;
            check(qrPaymentPage.Contains(activeQrUrl) && qrPaymentPage.Contains("customerQrDialog") && !qrPaymentPage.Contains("merchantQrFile"), "Cashier checkout uses persisted merchant QR without uploading again");
            check(qrPaymentPage.Contains("type=\"button\" id=\"closeCustomerQr\"") && !qrPaymentPage.Contains("method=\"dialog\""), "Return to checkout uses explicit non-submitting dialog close button");
            check(qrCheckoutScript.Contains("dialog.close()") && qrCheckoutScript.Contains("closeCustomerQr"), "Checkout asset connects Return button to dialog close");
            check(qrPaymentPage.Contains("This order stays pending") && !qrPaymentPage.Contains("Print customer receipt"), "QR display does not settle order or create paid receipt");
            var sandboxRoute = Regex.Match(qrPaymentPage, "/Cashier/Payments/Sandbox/\\d+").Value;
            var sandboxPage = await browser.GetStringAsync(sandboxRoute);
            check(sandboxPage.Contains("SANDBOX") && sandboxPage.Contains("Generate test QR"), "Bakong sandbox Razor checkout displays test-only disclaimer");
            check((await browser.PostAsync(sandboxRoute + "?handler=Start", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest, "Bakong sandbox Razor start requires antiforgery");
            var sandboxStart = await browser.PostAsync(sandboxRoute + "?handler=Start", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = Token(sandboxPage) }));
            check(sandboxStart.StatusCode == HttpStatusCode.Redirect, "Bakong sandbox Razor starts through authenticated API service");
            var renderedSandbox = await browser.GetStringAsync(sandboxRoute);
            check(renderedSandbox.Contains("data:image/png;base64,") && renderedSandbox.Contains("sandboxCheck"), "Bakong sandbox Razor renders generated order-specific QR and verification form");

            var rielClose = await browser.PostAsync("/Cashier/MyShift?handler=CloseAndReconcile", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(openedHtml), ["ClosingShiftId"] = rielShiftId,
                ["CountedCash"] = "50.00", ["CountedRiel"] = "190000"
            }));
            check(rielClose.StatusCode == HttpStatusCode.Redirect, "USD and KHR counts close together through Razor/API flow");
            var separateCounts = await browser.GetStringAsync("/Cashier/MyShift?ShiftId=" + rielShiftId);
            check(separateCounts.Contains("KHR -10,000") && separateCounts.Contains("USD 50.00"), "KHR shortage displayed independently of USD drawer amount");
            var blankUsd = Regex.Match(separateCounts, "<input[^>]*name=\"OpeningCash\"[^>]*>").Value;
            var blankRiel = Regex.Match(separateCounts, "<input[^>]*name=\"OpeningRiel\"[^>]*>").Value;
            check(blankUsd.Contains("value=\"\"") && blankRiel.Contains("value=\"\"") && !blankUsd.Contains("placeholder=") && !blankRiel.Contains("placeholder="),
                "USD and KHR opening inputs both start blank without default examples");
            var offShiftPos = await browser.GetStringAsync("/Cashier/POS");
            check(offShiftPos.Contains("Clock in required") && offShiftPos.Contains("Let's open your shift first."), "POS explains closed shift requirement instead of allowing checkout");
            var invalidPos = await browser.PostAsync("/Cashier/POS?handler=Checkout", new FormUrlEncodedContent(new Dictionary<string,string>
            {
                ["__RequestVerificationToken"] = Token(offShiftPos), ["CartJson"] = "[{\"productId\":" + uploadedProductId + ",\"quantity\":0}]", ["ProceedToPayment"] = "true"
            }));
            check(invalidPos.StatusCode == HttpStatusCode.OK && (await invalidPos.Content.ReadAsStringAsync()).Contains("Cart contains invalid products or quantities"), "Redesigned POS retains server-side cart validation");

        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            foreach (var file in Directory.GetFiles(keyDirectory)) File.Delete(file);
            Directory.Delete(keyDirectory);
            if (Directory.Exists(qrDirectory))
            {
                foreach (var file in Directory.GetFiles(qrDirectory)) File.Delete(file);
                Directory.Delete(qrDirectory);
            }
            if (Directory.Exists(imageDirectory))
            {
                foreach (var file in Directory.GetFiles(imageDirectory)) File.Delete(file);
                Directory.Delete(imageDirectory);
            }
        }
    }
}
