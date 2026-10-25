param(
    [ValidateRange(1, 20)]
    [int]$ExpectedReplicas = 3,

    [ValidatePattern('^[a-z0-9]([-a-z0-9]*[a-z0-9])?$')]
    [string]$Namespace = 'default'
)

$ErrorActionPreference = 'Stop'

$context = & kubectl config current-context 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($context)) {
    throw 'Nenhum contexto Kubernetes ativo foi encontrado.'
}
if ($context -notin @('minikube', 'docker-desktop')) {
    throw "Contexto Kubernetes '$context' recusado: o smoke test aceita apenas minikube ou docker-desktop."
}

& kubectl cluster-info | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'O cluster Kubernetes local nao esta acessivel.' }

$deploymentJson = & kubectl -n $Namespace get deployment order -o json
if ($LASTEXITCODE -ne 0) { throw 'Deployment/order nao foi encontrado.' }
$deployment = $deploymentJson | ConvertFrom-Json
$desiredReplicas = [int]$deployment.spec.replicas
$readyReplicas = [int]$deployment.status.readyReplicas
if ($desiredReplicas -ne $ExpectedReplicas -or $readyReplicas -ne $ExpectedReplicas) {
    throw "Deployment/order possui $readyReplicas/$desiredReplicas replicas prontas; o esperado para a demonstracao e $ExpectedReplicas/$ExpectedReplicas."
}

$statefulSetJson = & kubectl -n $Namespace get statefulset order-sqlserver -o json
if ($LASTEXITCODE -ne 0) { throw 'StatefulSet/order-sqlserver nao foi encontrado.' }
$statefulSet = $statefulSetJson | ConvertFrom-Json
$databaseDesired = [int]$statefulSet.spec.replicas
$databaseReady = [int]$statefulSet.status.readyReplicas
if ($databaseDesired -ne 1 -or $databaseReady -ne 1) {
    throw "StatefulSet/order-sqlserver possui $databaseReady/$databaseDesired replicas prontas; o esperado e 1/1."
}

$endpointSlicesJson = & kubectl -n $Namespace get endpointslices `
    --selector kubernetes.io/service-name=order -o json
if ($LASTEXITCODE -ne 0) { throw 'Falha ao consultar os endpoints do Service/order.' }
$endpointSlices = $endpointSlicesJson | ConvertFrom-Json
$readyEndpoints = 0
foreach ($slice in $endpointSlices.items) {
    foreach ($endpoint in $slice.endpoints) {
        if ($endpoint.conditions.ready -ne $false) {
            $readyEndpoints++
        }
    }
}
if ($readyEndpoints -ne $ExpectedReplicas) {
    throw "Service/order possui $readyEndpoints endpoints prontos; o esperado e $ExpectedReplicas."
}

$smokePod = "order-smoke-$PID"
$serviceUrl = "http://order.$Namespace.svc.cluster.local:8080"
$smokeCommand = "curl -fsS --max-time 15 $serviceUrl/health/ready >/dev/null && curl -fsS --max-time 15 $serviceUrl/metrics | grep -q '^# HELP' && curl -fsS --max-time 15 $serviceUrl/instance"

try {
    $smokeOutput = & kubectl -n $Namespace run $smokePod `
        --image=curlimages/curl:8.11.1 `
        --restart=Never `
        --rm `
        --attach `
        --quiet `
        --command -- sh -ec $smokeCommand 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "O smoke HTTP interno falhou: $($smokeOutput -join [Environment]::NewLine)"
    }
} finally {
    & kubectl -n $Namespace delete pod $smokePod --ignore-not-found --wait=false 2>$null | Out-Null
}

Write-Host "Smoke Kubernetes aprovado: $ExpectedReplicas replicas da API, SQL Server 1/1, DNS interno, readiness, metricas e /instance."
if (-not [string]::IsNullOrWhiteSpace($smokeOutput)) {
    Write-Host "Instancia atendente: $($smokeOutput -join ' ')"
}
