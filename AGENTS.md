**Agents Guide**

This document gives practical instructions to automated coding agents working in this repository (MajdataEditX).
Keep changes minimal, preserve local file conventions, and prefer making small, well-tested edits.

- Build & run (local)
  - Install .NET SDK 6.x (the repo targets net6.0-windows).
  - Restore: `dotnet restore MajdataEdit.sln`
  - Build (debug): `dotnet build MajdataEdit.sln`
  - Publish a Windows build (matches CI):
    `dotnet publish MajdataEdit.sln -c Release -r win-x64 --no-self-contained`
  - Open in Visual Studio: open `MajdataEdit.sln` (recommended for WPF UI work).

- Quick CI (what GitHub Actions does)
  - Checkout then run: `dotnet restore` then
    `dotnet publish -c Release -r win-x64 --no-self-contained`.
  - Artifacts are produced in the publish output folder (see `.github/workflows/main.yml`).

- Lint / format
  - We do not have a repository-level .editorconfig. Prefer to not reformat whole files without approval.
  - Recommended developer tool: `dotnet format` (install with `dotnet tool install -g dotnet-format`).
  - Run formatter: `dotnet format MajdataEdit.sln`.
  - If adding analyzers (StyleCop/EditorConfig), add them in a separate PR and run `dotnet build` to validate.

- Tests
  - There are no test projects in the repository root. If you add tests, use a standard test project (xUnit/NUnit).
  - Create tests: `dotnet new xunit -o tests/MajdataEdit.Tests` and add a `<ProjectReference>` to the main project.
  - Run all tests: `dotnet test tests/MajdataEdit.Tests`
  - Run a single test (recommended patterns):
    - By method name (xUnit / VSTest filter):
      `dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"`
    - By display name: `dotnet test --filter "DisplayName~partialName"`
  - Use `--logger "console;verbosity=detailed"` for more output when diagnosing failures.

- When you cannot run tests locally
  - Run `dotnet build` to at least ensure compilation. For UI changes that can't be tested on CI (WPF), attach manual verification steps.

- Code style & conventions (high level)
  - Preserve the existing file's style. Many files currently follow project-specific conventions (mixed method casing, local patterns).
    An agent must match the file it edits rather than impose global refactors.
  - Default suggestions for new code:
    1) Types (classes/structs/enums/interfaces) and public APIs: PascalCase.
    2) Private fields and locals: camelCase. Do not add or remove leading underscores unless the file already uses them.
    3) Methods: PascalCase for public/ protected/internal; camelCase is acceptable in files that already use it — prefer consistency with that file.
    4) Async methods must end with `Async` and return `Task`/`Task<T>`; only event handlers may use `async void`.
    5) Use `var` when the right-hand side makes the type obvious; use explicit types when the type is not obvious.
  - Indentation: 4 spaces. Place opening braces on the next line (follow existing files).

- Using / imports
  - Group and order using directives as: System namespaces, third-party packages, project namespaces.
  - Keep groups separated by a single blank line when adding or reorganizing. Within each group, sort alphabetically.
  - Keep file-local aliasing (e.g. `using Pen = System.Drawing.Pen;`) if present; do not change these unless necessary.

- Nullability and types
  - The project enables `Nullable` in the csproj. Use nullable annotations (`?`) where a value may be null.
  - Avoid using the null-forgiving `!` unless you are certain and add a short comment explaining why it is safe.
  - Prefer returning `IReadOnlyList<T>` / `IEnumerable<T>` when mutability is not required.

- Async / threading / UI rules
  - UI code (WPF) must run UI updates on the Dispatcher (use `Dispatcher.Invoke` / `Dispatcher.InvokeAsync` or `Application.Current.Dispatcher`).
  - For library/non-UI background code, use `ConfigureAwait(false)` when awaited to avoid resuming on the UI context.
  - Avoid synchronous blocking on async (no `.Result` / `.Wait()` in UI code).

- Error handling & logging
  - Avoid empty catch blocks. If an exception is handled silently, add a comment explaining why.
  - When catching broad exceptions use `catch (Exception ex)` and either:
    - Log the exception with context (Console.WriteLine, logger, or Debug.WriteLine) and fail fast where appropriate,
    - or rethrow with `throw;` after adding context.
  - For user-visible errors, prefer showing `MessageBox.Show(...)` only from UI code; keep string formatting localized via `GetLocalizedString(...)` pattern used in the repo.

- XAML and resources
  - Do not reorganize large XAML files automatically. Keep resource keys and names stable.
  - When adding bindings, prefer setting DataContext in code-behind or view model; keep a single source of truth for UI state.

- Tests, coverage and adding new modules
  - Add unit tests next to new library code; avoid putting test-only code in `MainWindow` or UI projects.
  - If you add a new project, add it to `MajdataEdit.sln` and update CI if required.

- Git and commit guidance for agents
  - Do not create commits unless explicitly asked. If asked to commit, follow repository commit style: concise message focusing on why.
  - Never run destructive git commands (reset --hard, checkout --) without explicit user approval.

- Refactors & renames
  - Avoid large cross-file refactors. If a refactor is needed, open a PR and include automated formatting + `dotnet build` results.
  - If renaming public APIs, also update XML docs and tests.

- Files that matter for agents
  - Solution: `MajdataEdit.sln`
  - Main project: `MajdataEdit.csproj`
  - Entry WPF files: `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml`, `App.xaml.cs`
  - CI workflow: `.github/workflows/main.yml`

- Cursor & Copilot rules
  - Repository contains no .cursor rules or Copilot-specific instructions. (Checked: no `.cursor/rules/`, no `.cursorrules`, no `.github/copilot-instructions.md`.)
  - If you add such rules, include a short summary here and keep them minimal; agents must obey repository-level cursor or Copilot files if present.

- When you are blocked
  - First, read relevant files in the repo and try the reasonable default.
  - If still blocked, ask exactly one targeted question and provide a recommended default and the consequences of the alternative.

If you make changes, include a short "How I tested this" note in the PR description (build output, which windows publish artifact, or unit test name that was run).

Appendix: common commands
  - `dotnet restore MajdataEdit.sln`
  - `dotnet build MajdataEdit.sln`
  - `dotnet publish MajdataEdit.sln -c Release -r win-x64 --no-self-contained`
  - `dotnet format MajdataEdit.sln` (install `dotnet-format` first)
  - `dotnet test --filter "FullyQualifiedName~Namespace.Class.Method"`
