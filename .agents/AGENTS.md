# Custom Rules for BIMTOOL Project

## Build Procedure
Whenever source code is modified and a build is required, ALWAYS build all versions of the plugin by running the `.\build_all.ps1` script instead of using `dotnet build` manually. Before running `.\build_all.ps1`, you should run `dotnet clean` to ensure all versions (2020 to 2026) are freshly rebuilt.

Example command:
```powershell
dotnet clean ; .\build_all.ps1
```
This ensures the `RincoNhan.bundle` folder is correctly updated with the latest DLLs for every Revit version.
