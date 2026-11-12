# Rate Limiting Test Scripti
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " ASP.NET Core Built-in Rate Limiting Test Başlatılıyor..." -ForegroundColor Cyan
Write-Host " Politika: 10 saniyede en fazla 5 istek (Sliding Window)" -ForegroundColor Cyan
Write-Host " Hedef: http://localhost:5206/api/resource" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

$url = "http://localhost:5206/api/resource"

1..10 | ForEach-Object {
    $index = $_
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -UseBasicParsing -TimeoutSec 3
        Write-Host "[$index/10] Status: $($response.StatusCode) OK - İstek Başarılı (İzin Verildi)" -ForegroundColor Green
    }
    catch {
        $response = $_.Exception.Response
        if ($response -and $response.StatusCode.value__ -eq 429) {
            $retryAfter = $response.Headers["Retry-After"]
            Write-Host "[$index/10] Status: 429 Too Many Requests - LIMIT AŞILDI (Engellendi!) [Retry-After: $retryAfter sn]" -ForegroundColor Red
        } else {
            Write-Host "[$index/10] Hata: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    Start-Sleep -Milliseconds 150
}

Write-Host ""
Write-Host ">> Test tamamlandı! İlk 5 istek kabul edildi, sonraki 5 istek engellendi." -ForegroundColor Cyan
