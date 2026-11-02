@echo off
rem Atualiza o Projeto Notch: fecha, compila, gera o instalador, instala e abre de novo.
rem O registro completo fica em dist\ultima-atualizacao.log
cd /d "%~dp0"
if not exist dist mkdir dist
echo Fechando o notch...
taskkill /F /IM ProjetoNotch.exe >nul 2>&1
echo Compilando e gerando o instalador (pode levar 1 a 3 minutos)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0gerar-instalador.ps1" > "%~dp0dist\ultima-atualizacao.log" 2>&1
if errorlevel 1 (
  echo.
  echo ERRO na compilacao. Veja dist\ultima-atualizacao.log
  echo ERRO >> "%~dp0dist\ultima-atualizacao.log"
  pause
  exit /b 1
)
echo Instalando...
"%~dp0dist\ProjetoNotch-Setup.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
echo INSTALADO >> "%~dp0dist\ultima-atualizacao.log"
start "" "%LOCALAPPDATA%\Programs\Projeto Notch\ProjetoNotch.exe"
echo Pronto! O notch atualizado ja esta aberto.
timeout /t 5 >nul
