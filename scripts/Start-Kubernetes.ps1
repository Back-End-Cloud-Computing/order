param(
    [switch]$UseLocalImage,
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publicKeyPath = Join-Path $root 'public.pem'
$chartPath = Join-Path $root 'helm/order'

if (-not (Test-Path -LiteralPath $publicKeyPath -PathType Leaf)) {
    throw 'Copie a chave publica RS256 do Authorization para public.pem na raiz do repositorio (nunca a chave privada).'
}

$publicKey = Get-Content -LiteralPath $publicKeyPath -Raw
if ($publicKey -notmatch '-----BEGIN PUBLIC KEY-----') {
    throw 'public.pem nao contem uma chave publica PEM valida.'
}

$minikubeCommand = Get-Command minikube -ErrorAction SilentlyContinue
if ($null -eq $minikubeCommand) {
    $installedMinikube = 'C:\Program Files\Kubernetes\Minikube\minikube.exe'
    if (Test-Path -LiteralPath $installedMinikube) {
        $minikubeCommand = @{ Source = $installedMinikube }
    }
}

$helmCommand = Get-Command helm -ErrorAction SilentlyContinue
if ($null -eq $helmCommand) {
    throw 'Helm nao encontrado. Instale o Helm 3 ou superior antes de executar este script.'
}

$context = & kubectl config current-context 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($context)) {
    if ($null -eq $minikubeCommand) {
        throw 'Nenhum cluster configurado. Instale o Minikube ou habilite o Kubernetes do Docker Desktop.'
    }
    & $minikubeCommand.Source start --driver=docker --cpus=4 --memory=6144 --disk-size=20g
    if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel iniciar o Minikube.' }
    $context = & kubectl config current-context
}

if ($context -notin @('minikube', 'docker-desktop')) {
    throw "Contexto Kubernetes '$context' recusado: use apenas minikube ou docker-desktop para evitar um deploy remoto acidental."
}

if ($context -eq 'minikube') {
    $status = & $minikubeCommand.Source status --format='{{.Host}}' 2>$null
    if ($LASTEXITCODE -ne 0 -or $status -ne 'Running') {
        & $minikubeCommand.Source start --driver=docker --cpus=4 --memory=6144 --disk-size=20g
        if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel iniciar o Minikube.' }
    }
}

& kubectl cluster-info | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'O cluster Kubernetes local nao esta acessivel.' }

$releaseNames = & $helmCommand.Source list --namespace default --short --filter '^order$'
if ($LASTEXITCODE -ne 0) { throw 'Falha ao consultar os releases Helm no namespace default.' }
if ('order' -notin @($releaseNames)) {
    $legacyResources = @()
    foreach ($resource in @(
        'deployment/order',
        'statefulset/order-sqlserver',
        'service/order',
        'service/order-sqlserver'
    )) {
        $existingResource = & kubectl -n default get $resource -o name --ignore-not-found 2>$null
        if ($LASTEXITCODE -ne 0) { throw "Falha ao consultar o recurso Kubernetes $resource." }
        if (-not [string]::IsNullOrWhiteSpace($existingResource)) {
            $legacyResources += $existingResource
        }
    }

    if ($legacyResources.Count -gt 0) {
        $resourceList = $legacyResources -join ', '
        throw "Existem recursos do Order sem o release Helm 'order': $resourceList. O script nao apaga nem adota recursos automaticamente. Remova ou migre esses recursos legados de forma consciente e execute novamente."
    }
}

$existingSecretJson = & kubectl -n default get secret order-secrets -o json --ignore-not-found
if ($LASTEXITCODE -ne 0) { throw 'Falha ao consultar o Secret order-secrets.' }

if ([string]::IsNullOrWhiteSpace($existingSecretJson)) {
    $sqlPassword = if ([string]::IsNullOrWhiteSpace($env:SQL_SERVER_PASSWORD)) {
        'Aa1!' + [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)).TrimEnd('=')
    } else {
        $env:SQL_SERVER_PASSWORD
    }
    if ($sqlPassword.Length -lt 8 -or $sqlPassword -notmatch '[A-Z]' -or
        $sqlPassword -notmatch '[a-z]' -or $sqlPassword -notmatch '[0-9]' -or
        $sqlPassword -notmatch '[^a-zA-Z0-9]' -or $sqlPassword.Contains(';')) {
        throw 'SQL_SERVER_PASSWORD precisa ter ao menos 8 caracteres, maiuscula, minuscula, numero e simbolo; nao use ponto e virgula.'
    }

    $internalSecret = if ([string]::IsNullOrWhiteSpace($env:INTERNAL_SERVICE_SECRET)) {
        [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).TrimEnd('=')
    } else {
        $env:INTERNAL_SERVICE_SECRET
    }
    $connectionString = "Server=order-sqlserver,1433;Database=GanjjOrder;User Id=sa;Password=$sqlPassword;TrustServerCertificate=True"
    $encode = [Text.Encoding]::UTF8
    $secret = @{
        apiVersion = 'v1'
        kind = 'Secret'
        metadata = @{ name = 'order-secrets'; namespace = 'default' }
        type = 'Opaque'
        data = @{
            'sqlserver-sa-password' = [Convert]::ToBase64String($encode.GetBytes($sqlPassword))
            'connection-string' = [Convert]::ToBase64String($encode.GetBytes($connectionString))
            'internal-secret' = [Convert]::ToBase64String($encode.GetBytes($internalSecret))
        }
    }
    $secret | ConvertTo-Json -Depth 8 -Compress | & kubectl apply --server-side --field-manager=order-local-bootstrap -f -
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o Secret order-secrets.' }
    Write-Host 'Secret local criado sem gravar credenciais no repositorio.'
} else {
    $existingSecret = $existingSecretJson | ConvertFrom-Json
    $currentSqlPassword = [Text.Encoding]::UTF8.GetString(
        [Convert]::FromBase64String($existingSecret.data.'sqlserver-sa-password'))
    if (-not [string]::IsNullOrWhiteSpace($env:SQL_SERVER_PASSWORD) -and
        $env:SQL_SERVER_PASSWORD -ne $currentSqlPassword) {
        throw 'SQL_SERVER_PASSWORD difere do Secret existente. A senha do banco persistente nao sera alterada automaticamente.'
    }
    if (-not [string]::IsNullOrWhiteSpace($env:INTERNAL_SERVICE_SECRET)) {
        $newInternalSecret = [Convert]::ToBase64String(
            [Text.Encoding]::UTF8.GetBytes($env:INTERNAL_SERVICE_SECRET))
        if ($newInternalSecret -ne $existingSecret.data.'internal-secret') {
            $secretUpdate = @{
                apiVersion = 'v1'
                kind = 'Secret'
                metadata = @{ name = 'order-secrets'; namespace = 'default' }
                type = 'Opaque'
                data = @{
                    'sqlserver-sa-password' = $existingSecret.data.'sqlserver-sa-password'
                    'connection-string' = $existingSecret.data.'connection-string'
                    'internal-secret' = $newInternalSecret
                }
            }
            $secretUpdate | ConvertTo-Json -Depth 8 -Compress | & kubectl apply --server-side --field-manager=order-local-bootstrap -f -
            if ($LASTEXITCODE -ne 0) { throw 'Falha ao atualizar o segredo interno compartilhado.' }
            Write-Host 'Segredo interno atualizado; a senha persistente do SQL Server foi preservada.'
        }
    }
}

$configMap = @{
    apiVersion = 'v1'
    kind = 'ConfigMap'
    metadata = @{ name = 'order-jwt-public-key'; namespace = 'default' }
    data = @{ 'public.pem' = $publicKey }
}
$configMap | ConvertTo-Json -Depth 8 -Compress | & kubectl apply --server-side --field-manager=order-local-bootstrap -f -
if ($LASTEXITCODE -ne 0) { throw 'Falha ao configurar a chave publica JWT.' }

if ($UseLocalImage) {
    & docker build -t ganjj-order:local $root
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao construir a imagem ganjj-order:local.' }

    if ($context -eq 'minikube') {
        & $minikubeCommand.Source image load ganjj-order:local
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao carregar a imagem no Minikube.' }
    }
}

$helmArguments = @(
    'upgrade', '--install', 'order', $chartPath,
    '--namespace', 'default',
    '--wait',
    '--timeout', '10m'
)
if ($UseLocalImage) {
    $helmArguments += @(
        '--set', 'image.repository=ganjj-order',
        '--set', 'image.tag=local',
        '--set', 'image.pullPolicy=Never'
    )
}

& $helmCommand.Source @helmArguments
if ($LASTEXITCODE -ne 0) { throw 'Falha ao instalar o chart Helm do Order.' }

& kubectl -n default rollout status statefulset/order-sqlserver --timeout=600s
if ($LASTEXITCODE -ne 0) { throw 'SQL Server nao ficou pronto; consulte kubectl describe pod order-sqlserver-0.' }
& kubectl -n default rollout status deployment/order --timeout=360s
if ($LASTEXITCODE -ne 0) { throw 'Order nao ficou pronto; consulte kubectl logs deployment/order.' }

& kubectl -n default get deployment/order statefulset/order-sqlserver service/order service/order-sqlserver
if ($LASTEXITCODE -ne 0) { throw 'Falha ao consultar os recursos Kubernetes criados.' }

if (-not $SkipSmokeTest) {
    $smokeScript = Join-Path $PSScriptRoot 'Test-Kubernetes.ps1'
    & $smokeScript
}

Write-Host 'Order pronto. Em outro terminal: kubectl -n default port-forward service/order 3004:8080'
