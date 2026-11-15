# Aura Café Web

Razor Pages frontend for the existing Aura Café POS & Management System.

See the [main project README](../README.md) for setup, configuration, backups/migrations, Admin and Cashier workflows, inventory rules, cash payments, Bakong sandbox setup, tests and known limitations.

Run from the solution directory:

```powershell
dotnet run --project AuraCafé.Web
```

Start the API separately and ensure Web `ApiSettings:BaseUrl` matches it. Read the main README's migration/startup notes before restarting the API after schema changes.
