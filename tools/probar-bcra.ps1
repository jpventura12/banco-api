# Prueba de la API Central de Deudores del BCRA (Windows PowerShell 5.1 / PowerShell 7).
# Uso:
#   .\probar-bcra.ps1
#   .\probar-bcra.ps1 -Cuit 30500010912 -Proxy http://10.2.248.246:8080 -ProxyDefaultCredentials
# Genera la carpeta bcra-salida\ con headers y bodies de cada caso (sirven como fixtures para los tests).

param(
    [string]$Cuit = "30500010912",
    [string]$Proxy = "",
    [switch]$ProxyDefaultCredentials
)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$Base = "https://api.bcra.gob.ar"
$Out  = Join-Path $PWD "bcra-salida"
New-Item -ItemType Directory -Force -Path $Out | Out-Null

function Probar([string]$Nombre, [string]$Path, [hashtable]$Headers = @{}) {
    $url = "$Base$Path"
    Write-Host "=================================================================="
    Write-Host "== $Nombre"
    Write-Host "== GET $url"

    $params = @{ Uri = $url; Method = 'Get'; UseBasicParsing = $true; TimeoutSec = 30; Headers = $Headers }
    if ($Proxy) { $params.Proxy = $Proxy; if ($ProxyDefaultCredentials) { $params.ProxyUseDefaultCredentials = $true } }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $status = $null; $body = ""; $hdrs = $null
    try {
        $r = Invoke-WebRequest @params
        $status = [int]$r.StatusCode; $body = $r.Content; $hdrs = $r.Headers
    }
    catch {
        $resp = $_.Exception.Response
        if ($resp) {
            $status = [int]$resp.StatusCode
            $hdrs = $resp.Headers
            try {
                $reader = New-Object IO.StreamReader($resp.GetResponseStream())
                $body = $reader.ReadToEnd()
            } catch {
                if ($_.ErrorDetails) { $body = $_.ErrorDetails.Message }
            }
            if (-not $body -and $_.ErrorDetails) { $body = $_.ErrorDetails.Message }
        }
        else {
            Write-Host "   ERROR sin respuesta: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    $sw.Stop()

    Write-Host ("   HTTP {0} en {1:N3}s" -f $status, $sw.Elapsed.TotalSeconds) -ForegroundColor Cyan
    $body | Out-File -Encoding utf8 (Join-Path $Out "$Nombre.json")

    $hdrTxt = ""
    if ($hdrs) {
        foreach ($k in $hdrs.Keys) { $hdrTxt += "${k}: $($hdrs[$k])`r`n" }
        $hdrTxt | Out-File -Encoding utf8 (Join-Path $Out "$Nombre.headers.txt")
        Write-Host "-- headers interesantes:"
        $hdrTxt -split "`r`n" | Where-Object { $_ -match '^(Content-Type|Cache-Control|Retry-After|X-RateLimit|RateLimit|X-Correlation|Server|Date)' } |
            ForEach-Object { Write-Host "   $_" }
    }
    Write-Host "-- body (primeros 800 caracteres):"
    if ($body) { Write-Host $body.Substring(0, [Math]::Min(800, $body.Length)) }
}

Probar "01-deudas-ok"          "/CentralDeDeudores/v1.0/Deudas/$Cuit"
Probar "02-historicas-ok"      "/CentralDeDeudores/v1.0/Deudas/Historicas/$Cuit"
Probar "03-cheques-rechazados" "/CentralDeDeudores/v1.0/Deudas/ChequesRechazados/$Cuit"
Probar "04-deudas-404"         "/CentralDeDeudores/v1.0/Deudas/12345678912"
Probar "05-deudas-400"         "/CentralDeDeudores/v1.0/Deudas/123456789"
Probar "06-path-minuscula"     "/centraldedeudores/v1.0/deudas/$Cuit"
Probar "07-con-correlation-id" "/CentralDeDeudores/v1.0/Deudas/$Cuit" @{ "X-Correlation-ID" = "prueba-$([guid]::NewGuid())" }

Write-Host "=================================================================="
Write-Host "== 08 latencia: 5 llamadas seguidas a Deudas"
1..5 | ForEach-Object {
    $p = @{ Uri = "$Base/CentralDeDeudores/v1.0/Deudas/$Cuit"; UseBasicParsing = $true; TimeoutSec = 30 }
    if ($Proxy) { $p.Proxy = $Proxy; if ($ProxyDefaultCredentials) { $p.ProxyUseDefaultCredentials = $true } }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try { $r = Invoke-WebRequest @p; $s = [int]$r.StatusCode } catch { $s = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { "ERR" } }
    $sw.Stop()
    Write-Host ("   intento {0}: HTTP {1} en {2:N3}s" -f $_, $s, $sw.Elapsed.TotalSeconds)
}

Write-Host ""
Write-Host "Listo. Archivos en $Out"
