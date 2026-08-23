Set-Variable -Name "projectSource" -Value "$PSScriptRoot/../"
Push-Location "$projectSource" # Use Push-Location so we can use Pop-Location to go back to the original directory

Remove-Item -Path "./Tests.*/TestResults/*" -Recurse -Force # Remove old test results

dotnet test --collect:"XPlat Code Coverage" --settings:coverage.runsettings
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coveragereport
#Start-Process .\coveragereport\index.html

Pop-Location