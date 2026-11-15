# Run this locally. Never paste the API token into chat or commit it to Git.
param([string]$ReceiverAccountId = 'sakhak_loeung@bkrt')
$ErrorActionPreference = 'Stop'
$taskApiFolder = Get-ChildItem -LiteralPath (Split-Path $PSScriptRoot -Parent) -Directory | Where-Object { $_.Name -like '*.API' } | Select-Object -First 1
if (-not $taskApiFolder) { throw 'Run this script from the existing Aura Cafe repository.' }
$taskPreviousEnabled = $env:BakongSandbox__Enabled
$taskPreviousAccount = $env:BakongSandbox__ReceiverAccountId
$taskPreviousToken = $env:BakongSandbox__ApiToken
$taskToken = Read-Host 'Bakong TEST API token (hidden; not saved to disk)' -AsSecureString
$taskPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskToken)
try {
    $env:BakongSandbox__ApiToken = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPointer)
    if ([string]::IsNullOrWhiteSpace($env:BakongSandbox__ApiToken)) { throw 'A test API token is required.' }
    $env:BakongSandbox__Enabled = 'true'
    $env:BakongSandbox__ReceiverAccountId = $ReceiverAccountId
    Write-Host 'Starting API with Bakong SANDBOX enabled. Test confirmations do not enter real sales.'
    Write-Host 'The existing Development startup applies pending additive migrations. Back up the database before running.'
    & dotnet run --project $taskApiFolder.FullName
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPointer)
    $taskToken.Dispose()
    $env:BakongSandbox__Enabled = $taskPreviousEnabled
    $env:BakongSandbox__ReceiverAccountId = $taskPreviousAccount
    $env:BakongSandbox__ApiToken = $taskPreviousToken
    $taskPreviousToken = $null
}
