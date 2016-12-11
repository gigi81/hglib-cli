param(
    [string]
    $Library = "Mercurial.Client",

    [string]
    $Version = $env:APPVEYOR_BUILD_VERSION    
)

$projects = (Get-ChildItem -Path $Path -Filter "Project.json" -Recurse -File).Fullname

function Set-DotnetProjectVersion
{
    param(
        $project,
        $version
    )

    $changed = $false
    $json = Get-Content -Raw -Path $project | ConvertFrom-Json
    if($json.version)
    {
        Write-Host "Updating version on project $project to $version"
        $json.version = $version
        $changed = $true
    }
    
    $property = $json.dependencies.PSobject.Properties | Where-Object { $_.Name -eq $Library }
    
    if($property)
    {
        Write-Host "Updating dependency version on $project to $version"
        $property.Value = $version
        $changed = $true
    }
    
    if($changed)
    {
        $encoding = New-Object System.Text.UTF8Encoding($False)
        $content = $json | ConvertTo-Json -depth 50
        [System.IO.File]::WriteAllText($project, $content, $encoding)
    }
}

if(-not ([string]::IsNullOrEmpty($Version)))
{
    Write-Host "Building version $Version"

    foreach($project in $projects)
    {
        Set-DotnetProjectVersion $project $Version
    }
}
