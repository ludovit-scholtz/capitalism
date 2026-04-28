[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$Environment = 'Development',

    [Parameter()]
    [switch]$Force,

    [Parameter()]
    [string[]]$AdditionalArguments = @()
)

$ErrorActionPreference = 'Stop'

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDirectory = Split-Path -Parent $scriptDirectory
$projectFile = Join-Path $projectDirectory 'Api.csproj'

if (-not (Test-Path $projectFile)) {
    throw "Could not find Api.csproj at '$projectFile'."
}

$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:ASPNETCORE_ENVIRONMENT = $Environment

try {
    Push-Location $projectDirectory

    $arguments = @(
        'ef',
        'migrations',
        'remove',
        '--context', 'Api.Data.AppDbContext',
        '--project', $projectFile,
        '--startup-project', $projectFile
    )

    if ($Force) {
        $arguments += '--force'
    }

    $arguments += @('--') + $AdditionalArguments

    Write-Host 'Removing the last AppDbContext migration...' -ForegroundColor Yellow
    Write-Host ("dotnet " + ($arguments -join ' ')) -ForegroundColor DarkGray

    & dotnet @arguments
    $exitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } elseif ($?) { 0 } else { 1 }
    if ($exitCode -ne 0) {
        throw "dotnet ef migrations remove failed with exit code $exitCode."
    }
}
finally {
    Pop-Location

    if ($null -eq $previousEnvironment) {
        Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    }
    else {
        $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    }
}
