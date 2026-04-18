# deploy-api.ps1
# Deploy PedagangPulsa.Api ke GCP Cloud Run (Free Tier)
# Region: Jakarta (asia-southeast2)
# Secrets via environment variables (bukan Secret Manager / GCS)
#
# Prerequisites:
#   - gcloud CLI sudah login: gcloud auth login
#   - Docker sudah berjalan
#   - Set environment variables di bawah atau isi manual saat prompt
#
# Usage: .\deploy-api.ps1

$ProjectId = "pedagangpulsa27"
$Region = "asia-southeast2"
$ServiceName = "pedagangpulsa-api"
$RepoName = "docker-repo"
$Image = "$Region-docker.pkg.dev/$ProjectId/$RepoName/$ServiceName"

# --- Secrets (baca dari env lokal, atau prompt jika kosong) ---
$DbConnection = if ($env:DEPLOY_DB_CONNECTION) { $env:DEPLOY_DB_CONNECTION } else {
    Read-Host "PostgreSQL ConnectionString"
}
$RedisConnection = "redis://default:MTzdXJYhwwc7d5hc5jERcESAT8rZpuCE@redis-12099.crce289.asia-seast2-2.gcp.cloud.redislabs.com:12099"
$JwtKey = "PedagangPulsaDevelopmentJwtKey_ChangeThisBeforeProduction_12345"
$JwtIssuer = "PedagangPulsa.Api"
$JwtAudience = "PedagangPulsa.Client"

Write-Host "=== Deploy PedagangPulsa.Api to Cloud Run ===" -ForegroundColor Cyan
Write-Host "Project : $ProjectId"
Write-Host "Region  : $Region (Jakarta)"
Write-Host "Image   : $Image"
Write-Host "Secrets : environment variables" -ForegroundColor DarkGray
Write-Host ""

# 1. Set project
Write-Host "[1/6] Setting project..." -ForegroundColor Yellow
gcloud config set project $ProjectId

# 2. Enable APIs (hanya perlu sekali)
Write-Host "[2/6] Enabling APIs..." -ForegroundColor Yellow
gcloud services enable run.googleapis.com artifactregistry.googleapis.com cloudbuild.googleapis.com

# 3. Buat Artifact Registry repo (jika belum ada)
Write-Host "[3/6] Setting up Artifact Registry..." -ForegroundColor Yellow
gcloud artifacts repositories create $RepoName --repository-format=docker --location=$Region --quiet 2>$null

# 4. Auth Docker
Write-Host "[4/6] Configuring Docker auth..." -ForegroundColor Yellow
gcloud auth configure-docker $Region-docker.pkg.dev --quiet

# 5. Build & push image
Write-Host "[5/6] Building & pushing image (Alpine, trimmed)..." -ForegroundColor Yellow
docker build -t "$Image" -f PedagangPulsa.Api/Dockerfile .
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
docker push "$Image"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Push failed!" -ForegroundColor Red
    exit 1
}

# 6. Deploy ke Cloud Run
Write-Host "[6/6] Deploying to Cloud Run..." -ForegroundColor Yellow

$envVars = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ConnectionStrings__DefaultConnection=$DbConnection",
    "ConnectionStrings__Redis=$RedisConnection",
    "Jwt__Key=$JwtKey",
    "Jwt__Issuer=$JwtIssuer",
    "Jwt__Audience=$JwtAudience",
    "EnableSwagger=true",
    "EnableScalar=true"
) -join ","

gcloud run deploy $ServiceName `
    --image "$Image" `
    --platform managed `
    --region $Region `
    --port 8080 `
    --memory 512Mi `
    --cpu 1 `
    --min-instances 0 `
    --max-instances 1 `
    --allow-unauthenticated `
    --set-env-vars $envVars `
    --no-cpu-throttling `
    --project $ProjectId

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deploy failed!" -ForegroundColor Red
    exit 1
}

# Cleanup: hapus image lama, biarkan hanya versi terbaru
Write-Host "[Cleanup] Setting image retention policy..." -ForegroundColor Yellow
gcloud artifacts repositories update $RepoName --location=$Region --cleanup-policies --cleanup-policy-config="name=keep-latest,action=DELETE,condition={newerThan='30d',olderThan='1d',tagState=UNTAGGED}" --quiet 2>$null

Write-Host ""
Write-Host "=== Deployment Complete! ===" -ForegroundColor Green
$Url = gcloud run services describe $ServiceName --platform managed --region $Region --format="value(status.url)" --project=$ProjectId
Write-Host "API URL    : $Url" -ForegroundColor Green
Write-Host "Logs       : Cloud Logging (stdout)" -ForegroundColor Cyan
Write-Host "Secrets    : Environment Variables" -ForegroundColor Cyan
