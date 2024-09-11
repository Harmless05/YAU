param (
    [string]$tempFilePath,
    [string]$currentFilePath
)

# Wait for the main application to close
Start-Sleep -Seconds 5

# Replace the old version with the new version
Copy-Item -Path $tempFilePath -Destination $currentFilePath -Force

# Start the new version
Start-Process -FilePath $currentFilePath