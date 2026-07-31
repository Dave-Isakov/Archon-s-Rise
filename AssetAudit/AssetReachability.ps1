$ErrorActionPreference = "Stop"
$root = "c:\Users\Dave's Comp\source\repos\Archon's Rise"
$assets = Join-Path $root "Assets"

$binaryExt = @(".png",".jpg",".jpeg",".psd",".psb",".tga",".ttf",".otf",".dll",".pdf",".mdb",
               ".xcf",".wav",".mp3",".ogg",".fbx",".blend",".exr",".bmp",".gif",".mp4",".pdb",
               ".zip",".aar",".jar",".dylib",".so",".bytes",".cubemap")

Write-Host "Indexing .meta files..."
$metaFiles = Get-ChildItem -Path $assets -Recurse -File -Filter *.meta
$guidToPath = @{}
$pathToGuid = @{}
$guidRe = [regex]'(?m)^guid:\s*([0-9a-fA-F]{32})'
foreach ($m in $metaFiles) {
    $txt = [IO.File]::ReadAllText($m.FullName)
    $mm = $guidRe.Match($txt)
    if ($mm.Success) {
        $g = $mm.Groups[1].Value.ToLower()
        $assetPath = $m.FullName.Substring(0, $m.FullName.Length - 5)
        $guidToPath[$g] = $assetPath
        $pathToGuid[$assetPath.ToLower()] = $g
    }
}
Write-Host ("  guids indexed: {0}" -f $guidToPath.Count)

# ---- Build outgoing edges: guid -> referenced guids ----
Write-Host "Scanning references..."
$refRe = [regex]'guid:\s*([0-9a-fA-F]{32})'
$edges = @{}

function Get-RefsFromFile([string]$file, [bool]$isMeta) {
    $result = New-Object 'System.Collections.Generic.HashSet[string]'
    try { $txt = [IO.File]::ReadAllText($file) } catch { return $result }
    if ($isMeta) {
        # strip the meta's own top-level guid declaration
        $txt = [regex]::Replace($txt, '(?m)^guid:\s*[0-9a-fA-F]{32}', '')
    }
    foreach ($mm in $refRe.Matches($txt)) { [void]$result.Add($mm.Groups[1].Value.ToLower()) }
    return $result
}

foreach ($kv in $guidToPath.GetEnumerator()) {
    $g = $kv.Key; $p = $kv.Value
    $set = New-Object 'System.Collections.Generic.HashSet[string]'
    $ext = [IO.Path]::GetExtension($p).ToLower()
    if (Test-Path -LiteralPath $p -PathType Container) {
        # folder asset: no refs of its own
    } elseif ($binaryExt -notcontains $ext) {
        if (Test-Path -LiteralPath $p) {
            foreach ($r in (Get-RefsFromFile $p $false)) { [void]$set.Add($r) }
        }
    }
    # the asset's .meta can carry refs (importer settings, atlas membership, etc.)
    $metaPath = "$p.meta"
    if (Test-Path -LiteralPath $metaPath) {
        foreach ($r in (Get-RefsFromFile $metaPath $true)) { [void]$set.Add($r) }
    }
    $edges[$g] = $set
}

# ---- Roots ----
Write-Host "Determining roots..."
$roots = New-Object 'System.Collections.Generic.HashSet[string]'
function Add-RootPath([string]$p) {
    $k = $p.ToLower()
    if ($pathToGuid.ContainsKey($k)) { [void]$roots.Add($pathToGuid[$k]) }
    else { Write-Host ("  !! no guid for root: {0}" -f $p) }
}

# 1. Scenes in build settings
$ebs = [IO.File]::ReadAllText((Join-Path $root "ProjectSettings\EditorBuildSettings.asset"))
foreach ($mm in [regex]::Matches($ebs, 'path:\s*(Assets/[^\r\n]+\.unity)')) {
    Add-RootPath (Join-Path $root ($mm.Groups[1].Value -replace '/', '\'))
}

# 2. Everything inside any Resources folder (loadable by path at runtime)
Get-ChildItem -Path $assets -Recurse -Directory -Filter "Resources" | ForEach-Object {
    Get-ChildItem -Path $_.FullName -Recurse -File | Where-Object { $_.Extension -ne ".meta" } | ForEach-Object {
        Add-RootPath $_.FullName
    }
}

# 3. All guids referenced from ProjectSettings (graphics/quality/TMP/input defaults)
Get-ChildItem -Path (Join-Path $root "ProjectSettings") -File | ForEach-Object {
    $t = [IO.File]::ReadAllText($_.FullName)
    foreach ($mm in $refRe.Matches($t)) {
        $g = $mm.Groups[1].Value.ToLower()
        if ($guidToPath.ContainsKey($g)) { [void]$roots.Add($g) }
    }
}

# 4. All C# scripts + asmdefs compile into the build regardless of scene refs.
#    Treat them as roots so assets referenced from ScriptableObject defaults aren't
#    falsely marked dead. (Dead *code* is reported separately.)
foreach ($kv in $guidToPath.GetEnumerator()) {
    $ext = [IO.Path]::GetExtension($kv.Value).ToLower()
    if ($ext -in @(".cs",".asmdef",".asmref")) { [void]$roots.Add($kv.Key) }
}

Write-Host ("  roots: {0}" -f $roots.Count)

# ---- BFS ----
Write-Host "Walking graph..."
$reached = New-Object 'System.Collections.Generic.HashSet[string]'
$queue = New-Object 'System.Collections.Generic.Queue[string]'
foreach ($r in $roots) { if ($reached.Add($r)) { $queue.Enqueue($r) } }
while ($queue.Count -gt 0) {
    $g = $queue.Dequeue()
    if ($edges.ContainsKey($g)) {
        foreach ($n in $edges[$g]) {
            if ($guidToPath.ContainsKey($n) -and $reached.Add($n)) { $queue.Enqueue($n) }
        }
    }
}
Write-Host ("  reachable assets: {0}" -f $reached.Count)

# ---- Report ----
$rows = @()
foreach ($kv in $guidToPath.GetEnumerator()) {
    $g = $kv.Key; $p = $kv.Value
    if (Test-Path -LiteralPath $p -PathType Container) { continue }
    if (-not (Test-Path -LiteralPath $p)) { continue }
    $fi = Get-Item -LiteralPath $p
    $rows += [PSCustomObject]@{
        Path      = $p.Substring($root.Length+1)
        Ext       = $fi.Extension.ToLower()
        Bytes     = $fi.Length
        Reachable = $reached.Contains($g)
        Guid      = $g
    }
}

$rows | Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $PSScriptRoot "asset-usage.csv")

$dead = $rows | Where-Object { -not $_.Reachable }
$live = $rows | Where-Object { $_.Reachable }
""
"=========== SUMMARY ==========="
"Assets total : {0,6}  ({1,8:N1} MB)" -f $rows.Count, (($rows|Measure-Object Bytes -Sum).Sum/1MB)
"Reachable    : {0,6}  ({1,8:N1} MB)" -f $live.Count, (($live|Measure-Object Bytes -Sum).Sum/1MB)
"UNREACHABLE  : {0,6}  ({1,8:N1} MB)" -f $dead.Count, (($dead|Measure-Object Bytes -Sum).Sum/1MB)
""
"--- Unreachable by top-level folder ---"
$dead | ForEach-Object {
    $parts = $_.Path -split '\\'
    $d = if ($parts.Count -ge 3) { ($parts[0..2] -join '\') } elseif ($parts.Count -ge 2) { ($parts[0..1] -join '\') } else { $parts[0] }
    [PSCustomObject]@{ Dir = $d; Bytes = $_.Bytes }
} | Group-Object Dir | ForEach-Object {
    [PSCustomObject]@{ Folder = $_.Name; Files = $_.Count; MB = [math]::Round((($_.Group|Measure-Object Bytes -Sum).Sum)/1MB,2) }
} | Sort-Object MB -Descending | Format-Table -AutoSize
