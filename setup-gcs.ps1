# setup-gcs.ps1
# Initial setup: Create GCS bucket, upload config files, set IAM permissions
# Run this ONCE before first deployment
#
# Usage: .\setup-gcs.ps1

$ProjectId = "pedagangpulsa27"
$Region = "asia-southeast2"
$BucketName = "pedagangpulsa27-storage"

Write-Host "=== Setup GCS Bucket for PedagangPulsa ===" -ForegroundColor Cyan
Write-Host "Project : $ProjectId"
Write-Host "Region  : $Region (Jakarta)"
Write-Host "Bucket  : $BucketName"
Write-Host ""

# 1. Set project
Write-Host "[1/6] Setting project..." -ForegroundColor Yellow
gcloud config set project $ProjectId

# 2. Enable Storage API
Write-Host "[2/6] Enabling Storage API..." -ForegroundColor Yellow
gcloud services enable storage.googleapis.com

# 3. Create bucket (if not exists)
Write-Host "[3/6] Creating GCS bucket..." -ForegroundColor Yellow
$bucketExists = gsutil ls gs://$BucketName 2>$null
if ($LASTEXITCODE -ne 0) {
    gsutil mb -p $ProjectId -l $Region -c STANDARD gs://$BucketName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to create bucket!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  Bucket created: gs://$BucketName" -ForegroundColor Green
} else {
    Write-Host "  Bucket already exists: gs://$BucketName" -ForegroundColor Green
}

# 4. Block public access
Write-Host "[4/6] Blocking public access..." -ForegroundColor Yellow
gsutil uniformbucketlevelaccess set on gs://$BucketName
gsutil iam ch allUsers: none gs://$BucketName 2>$null

# 5. Set lifecycle rule: auto-delete logs after 30 days
Write-Host "[5/6] Setting lifecycle rule (logs auto-delete after 30 days)..." -ForegroundColor Yellow
$lifecycleJson = @"
{
  "lifecycle": {
    "rule": [
      {
        "action": {"type": "Delete"},
        "condition": {
          "age": 30,
          "matchesPrefix": ["logs/"]
        }
      }
    ]
  }
}
"@

$tempFile = New-TemporaryFile
$lifecycleJson | Out-File -FilePath $tempFile.FullName -Encoding utf8
gsutil lifecycle set $tempFile.FullName gs://$BucketName
Remove-Item $tempFile.FullName

# 6. Grant Cloud Run service account access
Write-Host "[6/6] Setting IAM permissions..." -ForegroundColor Yellow
$serviceAccount = "$ProjectId-compute@developer.gserviceaccount.com"
gsutil iam ch serviceAccount:$serviceAccount:objectCreator,objectViewer,objectAdmin gs://$BucketName

Write-Host ""
Write-Host "=== GCS Setup Complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Create config/secrets.json with your production config:" -ForegroundColor White
Write-Host "     { " -ForegroundColor Gray
Write-host '       "ConnectionStrings": { "DefaultConnection": "...", "Redis": "..." },' -ForegroundColor Gray
Write-host '       "Jwt": { "Key": "...", "Issuer": "PedagangPulsa.Api", "Audience": "PedagangPulsa.Client" }' -ForegroundColor Gray
Write-Host "     }" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Upload config files:" -ForegroundColor White
Write-Host "     gsutil cp config/secrets.json gs://$BucketName/config/secrets.json" -ForegroundColor Gray
Write-Host "     gsutil cp config/appsettings.Production.json gs://$BucketName/config/appsettings.Production.json" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Run deploy-api.ps1 to deploy" -ForegroundColor White
