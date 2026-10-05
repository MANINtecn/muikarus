# Gera um keystore NOVO para assinar o APK do Ikarus MU e copia para a area de transferencia
# os valores dos 3 Secrets do GitHub. O keystore e salvo FORA do repositorio (guarde backup!).
# Se voce perder esse arquivo, nao consegue mais atualizar o app sem desinstalar (e perder o Data).
#
# Uso (PowerShell, na raiz do projeto):  .\tools\gerar_keystore.ps1

$ErrorActionPreference = "Stop"
$dir = Join-Path $env:USERPROFILE "IkarusKeys"
New-Item -ItemType Directory -Force $dir | Out-Null
$ks = Join-Path $dir "ikarus-release.keystore"
if (Test-Path $ks) { throw "Ja existe $ks - nao vou sobrescrever. Apague ou renomeie se quiser gerar outro." }

$alias = "ikarus"
$chars = (48..57) + (65..90) + (97..122)
$pass = -join ($chars | Get-Random -Count 24 | ForEach-Object { [char]$_ })

$keytool = (Get-Command keytool -ErrorAction SilentlyContinue).Source
if (-not $keytool -and $env:JAVA_HOME) { $keytool = Join-Path $env:JAVA_HOME "bin\keytool.exe" }
if (-not $keytool -or -not (Test-Path $keytool)) { throw "keytool nao encontrado (instale um JDK ou ajuste o PATH)." }

& $keytool -genkeypair -v -keystore $ks -alias $alias -keyalg RSA -keysize 2048 -validity 10000 `
  -storepass $pass -keypass $pass -dname "CN=Ikarus MU, O=TECX Softhouse, C=BR"
if ($LASTEXITCODE -ne 0) { throw "keytool falhou" }

$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($ks))
Set-Content -Path (Join-Path $dir "SECRETS-LEIA-ME.txt") -Encoding utf8 -Value @"
Crie 3 Secrets em GitHub > repo muikarus > Settings > Secrets and variables > Actions:
  ANDROID_KEY_ALIAS          = $alias
  ANDROID_KEYSTORE_PASSWORD  = $pass
  ANDROID_KEYSTORE_B64       = (conteudo do arquivo ikarus-release.keystore.b64.txt)
Guarde esta pasta em local seguro (backup). NAO coloque nada disso no git.
"@
Set-Content -Path (Join-Path $dir "ikarus-release.keystore.b64.txt") -Encoding ascii -Value $b64

Write-Host ""
Write-Host "Keystore criado em: $ks" -ForegroundColor Green
Write-Host "Instrucoes e valores em: $dir\SECRETS-LEIA-ME.txt" -ForegroundColor Green
Write-Host "Abra a pasta e crie os 3 Secrets. Faca BACKUP dessa pasta." -ForegroundColor Yellow
Invoke-Item $dir
