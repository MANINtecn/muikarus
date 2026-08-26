@echo off
title Iniciar MU Online

echo Iniciando o Servidor OpenMU...
cd "C:\TECX SOFTHOUSE\L2 IKARUS INTERCROW\MU_ONLINE\OpenMU\src\Startup\bin\Debug"
start "Servidor MU" "C:\Users\icaro\AppData\Local\Microsoft\dotnet\dotnet.exe" exec MUnique.OpenMU.Startup.dll -demo -autostart -resolveIP:loopback

echo Aguardando 5 segundos para o servidor ligar...
timeout /t 5 /nobreak >nul

echo Iniciando o Cliente do Jogo...
cd "C:\TECX SOFTHOUSE\L2 IKARUS INTERCROW\MU_ONLINE\Client_Desktop\MuWinDX\bin\Debug\net10.0-windows\win-x64"
start "Cliente MU" "C:\Users\icaro\AppData\Local\Microsoft\dotnet\dotnet.exe" exec MuMono.dll
