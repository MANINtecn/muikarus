@echo off
color 0A
title Ikarus MU - Servidor Principal
echo ====================================================
echo INICIANDO O SERVIDOR IKARUS MU...
echo ====================================================
echo.
dotnet run --project src\Startup -- -resolveIP:192.99.110.164
pause
