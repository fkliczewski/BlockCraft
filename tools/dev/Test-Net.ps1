param([string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$netRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$netRunId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $netRepo ('.modus\net-e2e-' + $netRunId)
}
$netOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $netOutput) {
    throw 'Wybierz nowy katalog wyników, aby nie użyć sygnałów poprzedniego testu.'
}
New-Item -ItemType Directory -Path $netOutput | Out-Null
dotnet build (Join-Path $netRepo 'src\BlockCraft') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja nie powiodła się.' }
$netExe = Join-Path $netRepo 'src\BlockCraft\bin\Release\net10.0\BlockCraft.exe'
$netProcesses = @()
try {
    foreach ($netRole in @('host', 'join')) {
        $netProcesses += Start-Process -FilePath $netExe -ArgumentList @(
            ('--autotest-' + $netRole), '--net-profile', ('test-' + $netRunId + '-' + $netRole),
            '--net-test-dir', ('"' + $netOutput + '"')
        ) -WindowStyle Hidden -PassThru
    }
    $netDeadline = [DateTime]::UtcNow.AddSeconds(270)
    while (@($netProcesses | Where-Object { -not $_.HasExited }).Count -gt 0) {
        if ([DateTime]::UtcNow -gt $netDeadline) { throw 'Przekroczono limit czasu procesów testowych.' }
        Start-Sleep -Milliseconds 500
    }
    foreach ($netRole in @('host', 'join')) {
        $netResult = Get-Content -Encoding UTF8 -LiteralPath (Join-Path $netOutput ($netRole + '.result')) -Raw
        Write-Output ($netRole + ': ' + $netResult)
        if (-not $netResult.StartsWith('OK ')) { throw ('Test ' + $netRole + ' nie powiódł się.') }
    }
    Write-Output ('Logi i zrzuty: ' + $netOutput)
}
finally {
    # Tylko procesy uruchomione przez ten skrypt.
    foreach ($netProcess in $netProcesses) {
        if (-not $netProcess.HasExited) { Stop-Process -Id $netProcess.Id -Force }
    }
}
