<#
.SYNOPSIS
    Exchange Log Middleware â€” Phase 8 E2E Validation Script
.DESCRIPTION
    docker-compose up yapildiktan sonra calistirin.
    Output dosyalari, KVKK maskeleme, format ve metrics ciktisini dogrular.
.EXAMPLE
    .\scripts\validate-e2e.ps1
    .\scripts\validate-e2e.ps1 -OutputDir ".\output" -WaitSeconds 30
#>
param(
    [string]$OutputDir   = "$PSScriptRoot\..\output",
    [int]   $WaitSeconds = 15
)

$ErrorActionPreference = "Continue"
$passCount = 0
$failCount = 0

function Write-Pass([string]$msg) {
    Write-Host "  [PASS] $msg" -ForegroundColor Green
    $script:passCount++
}
function Write-Fail([string]$msg) {
    Write-Host "  [FAIL] $msg" -ForegroundColor Red
    $script:failCount++
}
function Write-Section([string]$title) {
    Write-Host ""
    Write-Host "--- $title ---" -ForegroundColor Cyan
}

# ============================================================
# 1. Docker Container Durumu
# ============================================================
Write-Section "1. Docker Container Status"

$containers = @("exchange-rabbitmq", "exchange-producer", "exchange-middleware")
foreach ($c in $containers) {
    $state = (docker inspect --format '{{.State.Status}}' $c 2>$null)
    if ($state -eq "running") {
        Write-Pass "$c is running"
    } else {
        Write-Fail "$c is NOT running (state: $state)"
    }
}

# ============================================================
# 2. Output Dosyalari Mevcutluk Kontrolu
# ============================================================
Write-Section "2. Output Files Existence (waiting $WaitSeconds s for producer...)"
Start-Sleep -Seconds $WaitSeconds

$expectedFiles = @{
    "developer.jsonl" = "JSON Lines"
    "security.csv"    = "CSV"
    "sysadmin.md"     = "Markdown"
}

foreach ($entry in $expectedFiles.GetEnumerator()) {
    $path = Join-Path $OutputDir $entry.Key
    if (Test-Path $path) {
        $size = (Get-Item $path).Length
        if ($size -gt 0) {
            Write-Pass "$($entry.Key) exists ($size bytes)"
        } else {
            Write-Fail "$($entry.Key) exists but is EMPTY"
        }
    } else {
        Write-Fail "$($entry.Key) NOT FOUND (expected: $path)"
    }
}

# HTML opsiyonel
$htmlPath = Join-Path $OutputDir "sysadmin.html"
if (Test-Path $htmlPath) {
    Write-Pass "sysadmin.html exists (optional formatter)"
}

# ============================================================
# 3. JSON Format Dogrulama
# ============================================================
Write-Section "3. JSON Format Validation (developer.jsonl)"
$jsonPath = Join-Path $OutputDir "developer.jsonl"
if (Test-Path $jsonPath) {
    $lines = Get-Content $jsonPath | Where-Object { $_.Trim() -ne "" }
    $validCount = 0
    $invalidCount = 0
    foreach ($line in $lines) {
        try {
            $null = $line | ConvertFrom-Json -ErrorAction Stop
            $validCount++
        } catch {
            $invalidCount++
        }
    }
    if ($invalidCount -eq 0 -and $validCount -gt 0) {
        Write-Pass "All $validCount JSON lines are valid"
    } elseif ($invalidCount -gt 0) {
        Write-Fail "$invalidCount invalid JSON lines found"
    } else {
        Write-Fail "No JSON lines found"
    }
} else {
    Write-Fail "developer.jsonl not found - skipping JSON validation"
}

# ============================================================
# 4. CSV Format Dogrulama
# ============================================================
Write-Section "4. CSV Format Validation (security.csv)"
$csvPath = Join-Path $OutputDir "security.csv"
if (Test-Path $csvPath) {
    $lines = Get-Content $csvPath | Where-Object { $_.Trim() -ne "" }
    if ($lines.Count -ge 2) {
        $headerCols = ($lines[0] -split ",").Count
        $dataLine   = ($lines[1] -split ",").Count
        if ($headerCols -eq $dataLine) {
            Write-Pass "CSV header ($($headerCols) cols) matches data row"
        } else {
            Write-Fail "CSV column count mismatch: header=$headerCols, data=$dataLine"
        }
        Write-Pass "$($lines.Count - 1) data row(s) found"
    } else {
        Write-Fail "CSV has fewer than 2 lines (header + at least 1 row expected)"
    }
} else {
    Write-Fail "security.csv not found - skipping CSV validation"
}

# ============================================================
# 5. Markdown Format Dogrulama
# ============================================================
Write-Section "5. Markdown Format Validation (sysadmin.md)"
$mdPath = Join-Path $OutputDir "sysadmin.md"
if (Test-Path $mdPath) {
    $content = Get-Content $mdPath -Raw
    if ($content -match "\*\*\w+:\*\*") {
        Write-Pass "Markdown bold keys found"
    } else {
        Write-Fail "No Markdown bold keys found"
    }
    if ($content -match "\|") {
        Write-Pass "Markdown table separator '|' found"
    }
} else {
    Write-Fail "sysadmin.md not found - skipping Markdown validation"
}

# ============================================================
# 6. KVKK Maskeleme Dogrulama
# ============================================================
Write-Section "6. KVKK Masking - No Raw PII in Output Files"

$allOutputFiles = Get-ChildItem -Path $OutputDir -File -Recurse |
    Where-Object { $_.Name -ne ".gitkeep" }

$piiPatterns = @{
    "TCKN (11-digit)"        = "\b[1-9][0-9]{10}\b"
    "Credit Card (16-digit)" = "\b\d{4}\s\d{4}\s\d{4}\s\d{4}\b"
    "Email"                  = "[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}"
    "Phone (raw)"            = "\+90\s5\d{9}\b"
}

$piiFound = $false
foreach ($file in $allOutputFiles) {
    $fileContent = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $fileContent) { continue }
    foreach ($entry in $piiPatterns.GetEnumerator()) {
        if ($fileContent -match $entry.Value) {
            Write-Fail "Potential raw PII '$($entry.Key)' found in $($file.Name)"
            $piiFound = $true
        }
    }
}
if (-not $piiFound) {
    Write-Pass "No raw PII patterns detected in output files"
}

# ============================================================
# 7. Performance Metrics Konsol Ciktisi
# ============================================================
Write-Section "7. Performance Metrics in Middleware Logs"
$middlewareLogs = docker logs exchange-middleware 2>&1
$metricsLines = $middlewareLogs | Where-Object { $_ -match "\[METRICS\]" }
if ($metricsLines.Count -gt 0) {
    Write-Pass "[METRICS] log found ($($metricsLines.Count) line(s))"
    Write-Host "    Sample: $($metricsLines[0])" -ForegroundColor DarkGray
} else {
    Write-Fail "No [METRICS] lines found in middleware logs (service may not have run long enough)"
}

# ============================================================
# 8. Pipeline Processing Akisi Dogrulama
# ============================================================
Write-Section "8. Pipeline Flow in Middleware Logs"
$flowChecks = @{
    "BrokerListenerService baslatildi" = "BrokerListenerService"
    "PipelineWorkerService baslatildi" = "PipelineWorkerService"
    "RouterAndFormatterHandler"        = "RouterAndFormatterHandler"
}
foreach ($check in $flowChecks.GetEnumerator()) {
    if ($middlewareLogs | Where-Object { $_ -match $check.Value }) {
        Write-Pass "$($check.Key) found in logs"
    } else {
        Write-Fail "$($check.Key) NOT found in logs"
    }
}

# ============================================================
# Sonuc
# ============================================================
Write-Host ""
Write-Host "============================================" -ForegroundColor White
Write-Host " RESULT: $passCount PASS / $failCount FAIL" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })
Write-Host "============================================" -ForegroundColor White

if ($failCount -gt 0) {
    Write-Host ""
    Write-Host "Tip: Make sure 'docker-compose up -d' ran successfully and waited enough time." -ForegroundColor DarkYellow
    exit 1
}

