# Repository Guidelines

## Project Structure & Module Organization
`Program.cs` wires ASP.NET Core Identity, EF Core, and Razor Components. Persistent models live in `Entities/` and the `ApplicationDbContext` & `ApplicationUser` types live in `Data/`. UI is split into Razor components under `Components/` (pages + shared UI), while cross-page form models (`TaskInputModel`) sit in `Models/` and EF-backed orchestrators in `Services/`. Client assets and Tailwind output continue to reside in `wwwroot/`. Update configuration or secrets through `appsettings*.json` and environment variables (e.g., `ConnectionStrings__DefaultConnection`).

## Build, Test, and Development Commands
Run `dotnet restore` after cloning to fetch Identity/EF packages. `dotnet watch run` starts the interactive Blazor server and refreshes as you edit `.razor` files or services. Use `dotnet ef migrations add <Name>` followed by `dotnet ef database update` whenever you change the schema; both commands assume the `DefaultConnection` variable points at your SQL Server. Tailwind is still bundled with `npx tailwindcss -i wwwroot/app.css -o wwwroot/app.bundle.css --watch`. Execute `dotnet test --collect:"XPlat Code Coverage"` when test projects are present.

## Coding Style & Naming Conventions
Use 4-space indentation, file-scoped namespaces, and nullable reference types. Entity and component classes stay in PascalCase, local variables & private fields in camelCase, and interfaces retain the `I` prefix. Favor async APIs (e.g., `TaskService`) and let model binding validations live in the shared `Models/` layer. Keep Tailwind class lists ordered by layout → spacing → color to minimize noisy diffs.

## Testing Guidelines
xUnit is the default for service/component tests (`TaskServiceTests.cs`, `LoginPageTests.cs`). Mock EF contexts with the in-memory provider or `Testcontainers` SQL, and exercise Razor components via `bUnit` to validate the login/CRUD surfaces. Guard new logic with >80% statement coverage and capture auth-edge cases (unauthorized redirects, validation failures) in tests before merging.

## Commit & Pull Request Guidelines
Follow Conventional Commits (`feat: enable identity login`, `fix: persist reminder preferences`). Summaries stay under 72 chars; bodies should document migrations run (`dotnet ef database update`), manual verification steps (`dotnet watch run`, `npx tailwindcss …`), and any Docker commands executed. Every PR should link a tracking issue, attach UI screenshots/GIFs for visual tweaks, list new environment variables (e.g., `ConnectionStrings__DefaultConnection`), and describe the CRUD/auth flows tested locally.

## Security & Configuration Tips
Use `dotnet user-secrets` or environment variables for secrets—never check credentials into `appsettings*.json`. Passwords are hashed via ASP.NET Core Identity (PBKDF2); rotate them through the Settings page or `UserManager`. When containerizing, pass a secure SQL connection string with `docker build --build-arg CONNECTIONSTRING="Server=..."` or override `ConnectionStrings__DefaultConnection` at runtime. Keep HTTPS enabled (`dotnet dev-certs https --trust`) to protect cookies during local development.
