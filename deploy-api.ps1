# deploy-api.ps1
# Deploy PedagangPulsa.Api ke GCP Cloud Run (Free Tier)
# Region: Jakarta (asia-southeast2)
#
# Setup environment variables sebelum menjalankan:
#   $env:DB_CONNECTION_STRING = "Host=...;Username=...;Password=...;Database=...;SSL Mode=Require"
#   $env:JWT_KEY = "your-jwt-key"
#
# Usage: .\deploy-api.ps1

$ProjectId = "pedagangpulsa27"
$Region = "asia-southeast2"
$ServiceName = "pedagangpulsa-api"
$RepoName = "docker-repo"
$Image = "$Region-docker.pkg.dev/$ProjectId/$RepoName/$ServiceName"

Write-Host "=== Deploy PedagangPulsa.Api to Cloud Run ===" -ForegroundColor Cyan
Write-Host "Project : $ProjectId"
Write-Host "Region  : $Region (Jakarta)"
Write-Host "Image   : $Image"
Write-Host ""

# 1. Set project
Write-Host "[1/7] Setting project..." -ForegroundColor Yellow
gcloud config set project $ProjectId

# 2. Enable APIs (hanya perlu sekali)
Write-Host "[2/7] Enabling APIs..." -ForegroundColor Yellow
gcloud services enable run.googleapis.com artifactregistry.googleapis.com cloudbuild.googleapis.com

# 3. Buat Artifact Registry repo (jika belum ada)
Write-Host "[3/7] Setting up Artifact Registry..." -ForegroundColor Yellow
gcloud artifacts repositories create $RepoName --repository-format=docker --location=$Region --quiet 2>$null

# 4. Auth Docker
Write-Host "[4/7] Configuring Docker auth..." -ForegroundColor Yellow
gcloud auth configure-docker $Region-docker.pkg.dev --quiet

# 5. Build & push image
Write-Host "[5/7] Building & pushing image (Alpine, trimmed)..." -ForegroundColor Yellow
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
Write-Host "[6/7] Deploying to Cloud Run..." -ForegroundColor Yellow

$envVars = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "Jwt__Key=$env:JWT_KEY",
    "Jwt__Issuer=PedagangPulsa.Api",
    "Jwt__Audience=PedagangPulsa.Client"
) -join ","

$secrets = "ConnectionStrings__DefaultConnection=$env:DB_CONNECTION_STRING:latest"

gcloud run deploy $ServiceName `
    --image "$Image" `
    --platform managed `
    --region $Region `
    --port 8080 `
    --memory 256Mi `
    --cpu 1 `
    --min-instances 0 `
    --max-instances 1 `
    --allow-unauthenticated `
    --set-env-vars $envVars `
    --set-secrets $secrets `
    --no-cpu-throttling `
    --project $ProjectId

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deploy failed!" -ForegroundColor Red
    exit 1
}

# 7. Cleanup: hapus image lama, biarkan hanya versi terbaru
Write-Host "[7/7] Cleaning up old images..." -ForegroundColor Yellow
# Set retention policy: keep only 1 image
gcloud artifacts repositories update $RepoName --location=$Region --cleanup-policies --cleanup-policy-config="name=keep-latest,action=DELETE,condition={newerThan='30d',olderThan='1d',tagState=UNTAGGED}" --quiet 2>$null

Write-Host ""
Write-Host "=== Deployment Complete! ===" -ForegroundColor Green
$Url = gcloud run services describe $ServiceName --platform managed --region $Region --format="value(status.url)" --project=$ProjectId
Write-Host "API URL: $Url" -ForegroundColor Green
