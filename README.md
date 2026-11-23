# Bab Al Hara (.NET 10 Backend API)

REST + SignalR backend for **باب الحارة (Bab Al Hara — Sinnuris, Fayoum)**.

## Prerequisites
- .NET 10 SDK (`dotnet --version`)

## Running Locally
```powershell
cd f:\Money\Ahmed Hamedy\BabAlHara\backend\BabAlHara.Api
dotnet run
```

- API base URL: `http://localhost:5080`
- Swagger UI (Development): `http://localhost:5080/swagger`
- OpenAPI spec: `http://localhost:5080/openapi/v1.json`
- Health check: `http://localhost:5080/health`
- SignalR Orders Hub: `http://localhost:5080/hubs/orders`

On first startup, `DbSeeder` creates `babalhara.db` (SQLite) and seeds:
- Real restaurant profile, coordinates, opening hours (Cairo time), phones, ratings, and images from `Data/seed-data.json`.
- 14 real menu categories: 10 priced categories from the March 31, 2026 menu (live for customers) and 4 unpriced categories (`الشاورما`, `الفحم`, `البروست`, `وجبات وسندوتشات غربي`) marked `isAvailable = false` / `needsPricing = true` for the owner to price in the admin app.
- 2 real offers (`الدليفري بـ 10 جنيه` active, `كريباية والتانية بجنيه` inactive).
- Default owner account (configured in `appsettings.json`):
  - **Username:** `admin`
  - **Password:** `BabAlHara@2026` *(change immediately via `/api/auth/change-password` or the admin app's Change Password page)*

## Running Tests
```powershell
cd f:\Money\Ahmed Hamedy\BabAlHara\backend
dotnet test
```

## Switching to SQL Server (Production)
1. Install `Microsoft.EntityFrameworkCore.SqlServer` (`10.0.*`).
2. Update `ConnectionStrings:Default` in `appsettings.json` to your SQL Server connection string.
3. Replace `options.UseSqlite(connectionString)` with `options.UseSqlServer(connectionString)` in `Program.cs`.
4. Run EF Core migrations (`dotnet ef migrations add InitialCreate && dotnet ef database update`) or keep `EnsureCreatedAsync` for greenfield databases.
5. Set a strong 64+ character secret in `Jwt:Key` and configure production `Cors:Origins` via environment variables.
