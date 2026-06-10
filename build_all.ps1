# Build all release versions for RincoNhan BIMTOOL
$configs = "Release R20","Release R21","Release R22","Release R23","Release R24","Release R25","Release R26"

foreach($c in $configs) {
    Write-Host "Building Configuration: $c" -ForegroundColor Cyan
    dotnet build RincoNhan.csproj -c $c
}

Write-Host "All versions built successfully!" -ForegroundColor Green
