# Run from the repository root with PowerShell 7 on a GitHub Actions runner.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

function Read-PluginPackage($PackagePath, $ExpectedVersion) {
    $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
    try {
        $manifestEntry = $archive.GetEntry('AnomechJobs.json')
        if (-not $manifestEntry -or -not $archive.GetEntry('AnomechJobs.dll')) {
            throw 'The release ZIP must contain AnomechJobs.dll and AnomechJobs.json at its root.'
        }

        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try {
            $manifest = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }

    if ($manifest.InternalName -cne 'AnomechJobs' -or $manifest.AssemblyVersion -ne $ExpectedVersion -or
        $manifest.DalamudApiLevel -le 0) {
        throw "Unexpected plugin identity, version, or API level in $PackagePath."
    }

    return $manifest
}

$project = [xml](Get-Content -Raw AnoMech/AnoMech.csproj)
$version = [version]($project.Project.PropertyGroup.Version)
if ($version.Revision -lt 0) {
    throw 'Set Version in AnoMech/AnoMech.csproj to a four-part assembly version.'
}

$tag = "v$version"
$repo = $env:GITHUB_REPOSITORY
$branch = $env:RELEASE_BRANCH
$sourceCommit = $env:GITHUB_SHA
$repoUrl = "$env:GITHUB_SERVER_URL/$repo"
$feedUrl = "https://raw.githubusercontent.com/$repo/$branch/repo.json"
$downloadUrl = "$repoUrl/releases/download/$tag/latest.zip"
$package = 'AnoMech/bin/x64/Release/AnomechJobs/latest.zip'

$visibility = gh repo view $repo --json visibility --jq .visibility
if ($visibility -ne 'PUBLIC') {
    throw 'The release repository must be public for Dalamud to fetch its feed and package.'
}

$null = Read-PluginPackage $package $version.ToString()

git fetch origin "refs/heads/$branch"
git merge-base --is-ancestor $sourceCommit FETCH_HEAD
$feedWorktree = Join-Path $env:RUNNER_TEMP "anomechjobs-feed-$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT"
git worktree add --detach $feedWorktree FETCH_HEAD
$feedPath = Join-Path $feedWorktree repo.json
$feed = @()

if (Test-Path -LiteralPath $feedPath) {
    $feed = Get-Content -LiteralPath $feedPath -Raw | ConvertFrom-Json
    $previous = $feed | Where-Object InternalName -CEQ 'AnomechJobs'
    if ($previous) {
        $previousVersion = [version]$previous.AssemblyVersion
        if ($previousVersion -eq $version -and
            $previous.DownloadLinkInstall -eq $downloadUrl -and $previous.DownloadLinkUpdate -eq $downloadUrl) {
            Write-Output "$tag is already in the Dalamud feed."
            return
        }
        if ($previousVersion -ge $version) {
            throw "Increase the project version above $previousVersion before publishing."
        }
    }
}

# An existing tag is reusable only when it identifies the exact source being released.
$existingTag = git ls-remote --refs origin "refs/tags/$tag"
if ($existingTag) {
    git fetch --no-tags origin "refs/tags/$tag"
    $tagCommit = git rev-parse 'FETCH_HEAD^{commit}'
    if ($tagCommit -ne $sourceCommit) {
        throw "$tag already identifies another commit. Increase the project version or retry the original run."
    }
}

# If a previous run published the release before its feed push failed, reuse the published artifact.
$PSNativeCommandUseErrorActionPreference = $false
$releaseResult = gh release view $tag --repo $repo --json isDraft,isPrerelease,publishedAt 2>$null
$releaseExists = $LASTEXITCODE -eq 0
$PSNativeCommandUseErrorActionPreference = $true

if (-not $releaseExists) {
    gh release create $tag $package --repo $repo --target $sourceCommit --title "AnomechJobs $version" --notes "[Dalamud repository feed]($feedUrl)" --generate-notes
    $releaseResult = gh release view $tag --repo $repo --json isDraft,isPrerelease,publishedAt
}
$release = $releaseResult | ConvertFrom-Json
if ($release.isDraft -or $release.isPrerelease -or -not $release.publishedAt) {
    throw "$tag must be a published stable release before it can enter the Dalamud feed."
}

$publishedPackage = Join-Path $env:RUNNER_TEMP "anomechjobs-$tag.zip"
Invoke-WebRequest -Uri $downloadUrl -OutFile $publishedPackage -MaximumRetryCount 3
$manifest = Read-PluginPackage $publishedPackage $version.ToString()
$manifest.RepoUrl = $repoUrl
$manifest.DownloadLinkInstall = $downloadUrl
$manifest.DownloadLinkUpdate = $downloadUrl
$manifest.LastUpdate = ([DateTimeOffset]$release.publishedAt).ToUnixTimeSeconds()

$feed = @($feed | Where-Object { $_ -and $_.InternalName -cne 'AnomechJobs' }) + @($manifest)
ConvertTo-Json -InputObject $feed -Depth 10 | Set-Content -LiteralPath $feedPath -Encoding utf8NoBOM

git -C $feedWorktree add -- repo.json
git -C $feedWorktree -c 'user.name=github-actions[bot]' -c 'user.email=41898282+github-actions[bot]@users.noreply.github.com' commit -m "Publish AnomechJobs $version"
git -C $feedWorktree push origin "HEAD:refs/heads/$branch"
