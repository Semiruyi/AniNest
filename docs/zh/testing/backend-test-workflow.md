# Backend Test Workflow

The backend refactor is now validated mainly through:

- `src/AniNest.Host`
- `src/AniNest.Application`
- `src/AniNest.Contracts`
- `src/Tests.Backend`

The old UI projects are no longer the acceptance baseline for backend work.

## Common Commands

Run these from the repo root.

### 1. Run all backend tests

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj -m:1
```

Notes:
- Use `-m:1` to avoid the occasional Windows file-lock issue around `MvcTestingAppManifest.json`.
- This is the default pre-commit check.

### 2. Run only Library tests

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj --filter Library -m:1
```

Use this when changing:
- `src/AniNest.Application/Library`
- `src/AniNest.Host/Modules/FileLibraryCatalogStore.cs`
- `src/AniNest.Host/Endpoints/LibraryEndpoints.cs`

### 3. Build only the Host

```powershell
dotnet build src/AniNest.Host/AniNest.Host.csproj
```

Use this when changing:
- Host DI registration
- Host modules
- API endpoints

### 4. Run one test class

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj --filter "FullyQualifiedName~LibraryCatalogServiceTests" -m:1
```

Use this for tight iteration on a single service.

## Current Library Coverage

The backend Library tests now verify real behavior:

- scanning real temp folders
- rejecting empty folders
- batch import only for folders containing video files
- pruning missing folders on load
- refreshing `VideoCount` and cover path
- end-to-end `/api/library/*` host API checks

## Suggested Routine

Recommended flow while working:

1. Run the target module tests first
2. Run the full backend suite
3. If DI or endpoints changed, also run:

```powershell
dotnet build src/AniNest.Host/AniNest.Host.csproj
```

## Not Part of Backend Acceptance Anymore

These are not the main validation target for backend refactor work now:

- `src/AniNest`
- `src/AniNest.App`
- `src/Tests`

They can be removed or retired module by module later.
