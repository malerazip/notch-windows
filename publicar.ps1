param(
    # Também compila e instala o plugin de exemplo (Cotação)
    [switch]$ComExemplo
)
$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$dist = Join-Path $raiz 'dist'

Write-Host 'Compilando o Projeto Notch...' -ForegroundColor Cyan
dotnet publish (Join-Path $raiz 'src\Notch.App\Notch.App.csproj') -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $dist
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação' }

if ($ComExemplo) {
    Write-Host 'Compilando o plugin de exemplo...' -ForegroundColor Cyan
    $saida = Join-Path $env:APPDATA 'ProjetoNotch\plugins\Cotacao'
    dotnet build (Join-Path $raiz 'plugins\Exemplo.Cotacao\Exemplo.Cotacao.csproj') -c Release -o $saida
    if ($LASTEXITCODE -ne 0) { throw 'Falha no plugin' }
}

Write-Host "Pronto: $dist\ProjetoNotch.exe" -ForegroundColor Green
