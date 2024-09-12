param (
    [string]$tempFilePath,
    [string]$currentFilePath
)

# Wait for the main application to close
Start-Sleep -Seconds 5

# Replace the old version with the new version
Remove-Item -Path $currentFilePath
# Sleep
Start-Sleep -Seconds 5
Move-Item -Path $tempFilePath -Destination $currentFilePath

# Start the new version
Start-Process -FilePath $currentFilePath