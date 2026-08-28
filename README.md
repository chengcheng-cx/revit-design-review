# Revit Design Review

Revit Design Review is an Autodesk Revit add-in for turning model observations into durable, locatable review records.

This repository currently contains the M0 technical milestone for Revit 2026. It proves that a user can select elements, create a review in local SQLite storage, then reopen the latest review and return to its saved view and elements.

## M0 features

- `Design Review` ribbon tab in Revit 2026.
- Quick `Create Review` command.
- Related element `UniqueId` persistence.
- Existing Revit view persistence.
- 3D camera orientation and section-box persistence.
- `Open Latest Review` command with missing-element reporting.
- Versioned SQLite schema.
- Core and data unit tests runnable without Revit.

See [M0 architecture](docs/architecture/M0.md) for boundaries and design decisions.

## Requirements

- Windows
- Autodesk Revit 2026
- .NET 8 SDK

The add-in targets the Windows x64 runtime used by Revit 2026.

Set the Revit installation directory without committing a machine-specific path:

```powershell
$env:REVIT_2026_INSTALL_DIR = 'C:\Program Files\Autodesk\Revit 2026'
```

## Build and test

Core and data tests do not require Revit:

```powershell
dotnet test tests/RevitDesignReview.Core.Tests/RevitDesignReview.Core.Tests.csproj
dotnet test tests/RevitDesignReview.Data.Tests/RevitDesignReview.Data.Tests.csproj
```

Build the Revit adapter:

```powershell
dotnet build src/RevitDesignReview.Revit2026/RevitDesignReview.Revit2026.csproj
```

## Local installation

Close Revit, then run:

```powershell
.\scripts\Install-Local.ps1
```

The script builds the add-in, copies its output to the current user's Revit 2026 add-ins directory, and creates the `.addin` manifest. Restart Revit after installation.
If `dotnet` is not on `PATH`, pass its full path with `-DotnetPath`.

## Current limitations

M0 is deliberately local and single-user. It does not yet include the Review Browser, comments, status editing, clash detection, markup, BCF, central synchronization, or linked-model element selection.
