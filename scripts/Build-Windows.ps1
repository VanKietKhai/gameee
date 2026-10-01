param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
dotnet restore "$PSScriptRoot\..\ConanServerControl.sln"
dotnet build "$PSScriptRoot\..\ConanServerControl.sln" -c $Configuration --no-restore
dotnet test "$PSScriptRoot\..\ConanServerControl.sln" -c $Configuration --no-build
