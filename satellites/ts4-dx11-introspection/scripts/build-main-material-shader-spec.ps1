param(
    [string[]]$Inputs = @(
        '20260421-212139',
        '20260421-212533',
        '20260421-220041'
    ),

    [string]$Output = 'C:\Users\stani\PROJECTS\Sims4Browser\satellites\ts4-dx11-introspection\docs\ts4-material-shader-spec.md',

    [string]$MatdCensusPath = 'C:\Users\stani\PROJECTS\Sims4Browser\tmp\matd_shader_census_fullscan.json',

    [string]$ResourceTypeCensusPath = 'C:\Users\stani\PROJECTS\Sims4Browser\tmp\resource_type_census_fullscan.json'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$captureRoot = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\captures\live'
$docsRoot = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\docs'
$rawDocsRoot = Join-Path $docsRoot 'raw'

function Resolve-CapturePath {
    param([string]$Value)

    if ([System.IO.Path]::IsPathRooted($Value)) {
        return (Resolve-Path $Value).Path
    }

    return (Resolve-Path (Join-Path $captureRoot $Value)).Path
}

function Expand-SessionArgs {
    param([string[]]$Values)

    foreach ($value in $Values) {
        foreach ($part in ($value -split ',')) {
            $trimmed = $part.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $trimmed
            }
        }
    }
}

function Get-SemanticSignature {
    param($Parameters)

    if ($null -eq $Parameters -or $Parameters.Count -eq 0) {
        return '-'
    }

    return (($Parameters | ForEach-Object { '{0}{1}' -f $_.semantic_name, $_.semantic_index }) -join ', ')
}

function Get-BinaryLinkageMap {
    param([string]$Path)

    $map = @{}
    if (-not (Test-Path $Path)) {
        return $map
    }

    $rows = Get-Content -Raw $Path | ConvertFrom-Json
    foreach ($row in $rows) {
        if ($row.MatchKind -ne 'runtime_catalog') {
            continue
        }

        if (-not $map.ContainsKey($row.Sha256)) {
            $map[$row.Sha256] = New-Object System.Collections.Generic.List[string]
        }

        [void]$map[$row.Sha256].Add($row.Offset)
    }

    return $map
}

function Get-DisassemblyMap {
    param([string]$Directory)

    $map = @{}
    if (-not (Test-Path -LiteralPath $Directory)) {
        return $map
    }

    foreach ($file in (Get-ChildItem -LiteralPath $Directory -Filter *.asm -File)) {
        if ($file.BaseName -notmatch '^[a-z]{2}-([0-9a-f]{64})-offset-') {
            continue
        }

        $hash = $Matches[1]
        $content = Get-Content -LiteralPath $file.FullName
        $joined = $content -join "`n"
        $sampleCount = ([regex]::Matches($joined, '(?m):\s+sample(?:_[a-z]+)?(?:_indexable)?\(')).Count
        $sampleLCount = ([regex]::Matches($joined, '(?m):\s+sample_l(?:_[a-z]+)?(?:_indexable)?\(')).Count
        $loopCount = ([regex]::Matches($joined, '(?m):\s+loop\s*$')).Count
        $discardCount = ([regex]::Matches($joined, '(?m):\s+discard')).Count
        $usesDynamicCb = $joined -match 'dcl_constantbuffer\s+CB\d+\[\d+\],\s+dynamicIndexed'
        $approxSlots = 0
        $slotMatch = [regex]::Match($joined, 'Approximately\s+(\d+)\s+instruction slots used')
        if ($slotMatch.Success) {
            $approxSlots = [int]$slotMatch.Groups[1].Value
        }

        $map[$hash] = [pscustomobject]@{
            Path = $file.FullName
            RelativePath = if ($file.FullName.StartsWith($docsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                $file.FullName.Substring($docsRoot.Length).TrimStart('\')
            }
            else {
                $file.FullName
            }
            SampleCount = $sampleCount
            SampleLCount = $sampleLCount
            LoopCount = $loopCount
            DiscardCount = $discardCount
            UsesDynamicConstantBufferIndexing = $usesDynamicCb
            ApproxInstructionSlots = $approxSlots
        }
    }

    return $map
}

function New-StringSet {
    return @{}
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Add-String {
    param(
        [Parameter(Mandatory = $true)]
        $Set,

        [string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        $Set[$Value] = $true
    }
}

$resolvedInputs = Expand-SessionArgs $Inputs | ForEach-Object { Resolve-CapturePath $_ }
$outputPath = [System.IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($outputPath)) -Force | Out-Null

$binaryLinksX64 = Get-BinaryLinkageMap (Join-Path $rawDocsRoot 'reports\embedded-dxbc-TS4_x64.json')
$binaryLinksFpb = Get-BinaryLinkageMap (Join-Path $rawDocsRoot 'reports\embedded-dxbc-TS4_x64_fpb.json')
$disassemblyMap = Get-DisassemblyMap (Join-Path $rawDocsRoot 'disassembly\embedded-x64\asm')
$matdCensus = Read-JsonFile $MatdCensusPath
$resourceTypeCensus = Read-JsonFile $ResourceTypeCensusPath

$shaderSpecs = @{}
$captureSeen = @{}

foreach ($capturePath in $resolvedInputs) {
    $captureName = Split-Path $capturePath -Leaf
    $shaderPath = Join-Path $capturePath 'shaders.jsonl'
    if (-not (Test-Path $shaderPath)) {
        continue
    }

    foreach ($line in Get-Content $shaderPath) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $row = $line | ConvertFrom-Json
        $hash = $row.shader_hash

        if (-not $shaderSpecs.ContainsKey($hash)) {
            $reflection = $row.reflection
            $shaderSpecs[$hash] = [pscustomobject]@{
                Hash = $hash
                Stage = $row.stage
                BytecodeSize = [int]$row.bytecode_size
                TimestampUtc = $row.timestamp_utc
                InstructionCount = if ($null -ne $reflection) { [int]$reflection.instruction_count } else { 0 }
                TempRegisterCount = if ($null -ne $reflection) { [int]$reflection.temp_register_count } else { 0 }
                BoundResources = @($reflection.bound_resources)
                ConstantBuffers = @($reflection.constant_buffers)
                InputParameters = @($reflection.input_parameters)
                OutputParameters = @($reflection.output_parameters)
                ReflectionSignature = '{0} br={1} cb={2} in={3} out={4}' -f `
                    $row.stage,
                    @($reflection.bound_resources).Count,
                    @($reflection.constant_buffers).Count,
                    @($reflection.input_parameters).Count,
                    @($reflection.output_parameters).Count
                InputSemanticsSignature = Get-SemanticSignature $reflection.input_parameters
                OutputSemanticsSignature = Get-SemanticSignature $reflection.output_parameters
            }
        }

        if (-not $captureSeen.ContainsKey($hash)) {
            $captureSeen[$hash] = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        }

        [void]$captureSeen[$hash].Add($captureName)
    }
}

$entries = foreach ($hash in ($shaderSpecs.Keys | Sort-Object)) {
    $spec = $shaderSpecs[$hash]
    $seenIn = @($captureSeen[$hash] | Sort-Object)
    $x64Offsets = if ($binaryLinksX64.ContainsKey($hash)) { @($binaryLinksX64[$hash] | Sort-Object -Unique) } else { @() }
    $fpbOffsets = if ($binaryLinksFpb.ContainsKey($hash)) { @($binaryLinksFpb[$hash] | Sort-Object -Unique) } else { @() }

    [pscustomobject]@{
        Hash = $spec.Hash
        Stage = $spec.Stage
        BytecodeSize = $spec.BytecodeSize
        ReflectionSignature = $spec.ReflectionSignature
        InputSemanticsSignature = $spec.InputSemanticsSignature
        OutputSemanticsSignature = $spec.OutputSemanticsSignature
        CaptureSupport = $seenIn.Count
        SeenInCaptures = $seenIn
        TimestampUtc = $spec.TimestampUtc
        InstructionCount = $spec.InstructionCount
        TempRegisterCount = $spec.TempRegisterCount
        BoundResources = $spec.BoundResources
        ConstantBuffers = $spec.ConstantBuffers
        InputParameters = $spec.InputParameters
        OutputParameters = $spec.OutputParameters
        X64Offsets = $x64Offsets
        FpbOffsets = $fpbOffsets
        BinaryLinked = ($x64Offsets.Count -gt 0 -or $fpbOffsets.Count -gt 0)
        Disassembly = if ($disassemblyMap.ContainsKey($hash)) { $disassemblyMap[$hash] } else { $null }
    }
}

$total = @($entries).Count
$psCount = @($entries | Where-Object Stage -eq 'ps').Count
$vsCount = @($entries | Where-Object Stage -eq 'vs').Count
$binaryLinked = @($entries | Where-Object BinaryLinked).Count
$disassemblyCount = @($entries | Where-Object { $null -ne $_.Disassembly }).Count
$disassemblyWithSampling = @($entries | Where-Object { $null -ne $_.Disassembly -and $_.Disassembly.SampleCount -gt 0 }).Count
$disassemblyWithSampleL = @($entries | Where-Object { $null -ne $_.Disassembly -and $_.Disassembly.SampleLCount -gt 0 }).Count
$disassemblyWithLoops = @($entries | Where-Object { $null -ne $_.Disassembly -and $_.Disassembly.LoopCount -gt 0 }).Count
$disassemblyWithDynamicCb = @($entries | Where-Object { $null -ne $_.Disassembly -and $_.Disassembly.UsesDynamicConstantBufferIndexing }).Count
$materialRelevantTypeNames = @(
    'MaterialDefinition',
    'MaterialSet',
    'Geometry',
    'BlendGeometry',
    'Model',
    'ModelLOD',
    'Rig',
    'DSTImage',
    'LRLEImage',
    'RLE2Image',
    'RLESImage',
    'PNGImage',
    'PNGImage2',
    'RegionMap',
    'CASPart',
    'ObjectCatalog',
    'ObjectDefinition'
)
$materialRelevantTypeRows = @()
if ($null -ne $resourceTypeCensus) {
    $byName = @{}
    foreach ($row in @($resourceTypeCensus.TopTypes)) {
        $byName[$row.Name] = $row
    }

    foreach ($typeName in $materialRelevantTypeNames) {
        if ($byName.ContainsKey($typeName)) {
            $materialRelevantTypeRows += $byName[$typeName]
        }
    }
}

$resourceVocabulary = @{}
$cbufferVocabulary = @{}
$variableVocabulary = @{}

foreach ($entry in $entries) {
    $isSampled = ($null -ne $entry.Disassembly -and $entry.Disassembly.SampleCount -gt 0)
    $isLooped = ($null -ne $entry.Disassembly -and $entry.Disassembly.LoopCount -gt 0)
    $isDynamicCb = ($null -ne $entry.Disassembly -and $entry.Disassembly.UsesDynamicConstantBufferIndexing)

    foreach ($resource in $entry.BoundResources) {
        if ($resource.type -eq 'cbuffer') {
            continue
        }

        $resourceName = if ([string]::IsNullOrWhiteSpace($resource.name)) { '<unnamed>' } else { $resource.name }
        if (-not $resourceVocabulary.ContainsKey($resourceName)) {
            $resourceVocabulary[$resourceName] = [pscustomobject]@{
                Name = $resourceName
                Hashes = (New-StringSet)
                PsHashes = (New-StringSet)
                VsHashes = (New-StringSet)
                SampledHashes = (New-StringSet)
                LoopedHashes = (New-StringSet)
                DynamicCbHashes = (New-StringSet)
                Types = (New-StringSet)
                Dimensions = (New-StringSet)
            }
        }

        $bucket = $resourceVocabulary[$resourceName]
        Add-String $bucket.Hashes $entry.Hash
        if ($entry.Stage -eq 'ps') { Add-String $bucket.PsHashes $entry.Hash }
        if ($entry.Stage -eq 'vs') { Add-String $bucket.VsHashes $entry.Hash }
        if ($isSampled) { Add-String $bucket.SampledHashes $entry.Hash }
        if ($isLooped) { Add-String $bucket.LoopedHashes $entry.Hash }
        if ($isDynamicCb) { Add-String $bucket.DynamicCbHashes $entry.Hash }
        Add-String $bucket.Types $resource.type
        Add-String $bucket.Dimensions ([string]$resource.dimension)
    }

    foreach ($cbuffer in $entry.ConstantBuffers) {
        $cbufferName = if ([string]::IsNullOrWhiteSpace($cbuffer.name)) { '<unnamed>' } else { $cbuffer.name }
        if (-not $cbufferVocabulary.ContainsKey($cbufferName)) {
            $cbufferVocabulary[$cbufferName] = [pscustomobject]@{
                Name = $cbufferName
                Hashes = (New-StringSet)
                PsHashes = (New-StringSet)
                VsHashes = (New-StringSet)
                SampledHashes = (New-StringSet)
                LoopedHashes = (New-StringSet)
                DynamicCbHashes = (New-StringSet)
                Sizes = (New-StringSet)
                BufferTypes = (New-StringSet)
            }
        }

        $cbucket = $cbufferVocabulary[$cbufferName]
        Add-String $cbucket.Hashes $entry.Hash
        if ($entry.Stage -eq 'ps') { Add-String $cbucket.PsHashes $entry.Hash }
        if ($entry.Stage -eq 'vs') { Add-String $cbucket.VsHashes $entry.Hash }
        if ($isSampled) { Add-String $cbucket.SampledHashes $entry.Hash }
        if ($isLooped) { Add-String $cbucket.LoopedHashes $entry.Hash }
        if ($isDynamicCb) { Add-String $cbucket.DynamicCbHashes $entry.Hash }
        Add-String $cbucket.Sizes ([string]$cbuffer.size)
        Add-String $cbucket.BufferTypes $cbuffer.buffer_type

        foreach ($variable in @($cbuffer.variables)) {
            $variableKey = '{0}::{1}' -f $cbufferName, $variable.name
            if (-not $variableVocabulary.ContainsKey($variableKey)) {
                $variableVocabulary[$variableKey] = [pscustomobject]@{
                    BufferName = $cbufferName
                    VariableName = $variable.name
                    Hashes = (New-StringSet)
                    PsHashes = (New-StringSet)
                    VsHashes = (New-StringSet)
                    SampledHashes = (New-StringSet)
                    LoopedHashes = (New-StringSet)
                    DynamicCbHashes = (New-StringSet)
                    TypeNames = (New-StringSet)
                    Offsets = (New-StringSet)
                    Sizes = (New-StringSet)
                }
            }

            $vbucket = $variableVocabulary[$variableKey]
            Add-String $vbucket.Hashes $entry.Hash
            if ($entry.Stage -eq 'ps') { Add-String $vbucket.PsHashes $entry.Hash }
            if ($entry.Stage -eq 'vs') { Add-String $vbucket.VsHashes $entry.Hash }
            if ($isSampled) { Add-String $vbucket.SampledHashes $entry.Hash }
            if ($isLooped) { Add-String $vbucket.LoopedHashes $entry.Hash }
            if ($isDynamicCb) { Add-String $vbucket.DynamicCbHashes $entry.Hash }
            Add-String $vbucket.TypeNames $variable.type_name
            Add-String $vbucket.Offsets ([string]$variable.start_offset)
            Add-String $vbucket.Sizes ([string]$variable.size)
        }
    }
}

$resourceVocabularyRows = foreach ($bucket in $resourceVocabulary.Values) {
    [pscustomobject]@{
        Name = $bucket.Name
        ShaderCount = $bucket.Hashes.Count
        PsShaderCount = $bucket.PsHashes.Count
        VsShaderCount = $bucket.VsHashes.Count
        SampledShaderCount = $bucket.SampledHashes.Count
        LoopedShaderCount = $bucket.LoopedHashes.Count
        DynamicCbShaderCount = $bucket.DynamicCbHashes.Count
        Types = @($bucket.Types.Keys | Sort-Object) -join ', '
        Dimensions = @($bucket.Dimensions.Keys | Sort-Object) -join ', '
    }
}

$cbufferVocabularyRows = foreach ($bucket in $cbufferVocabulary.Values) {
    [pscustomobject]@{
        Name = $bucket.Name
        ShaderCount = $bucket.Hashes.Count
        PsShaderCount = $bucket.PsHashes.Count
        VsShaderCount = $bucket.VsHashes.Count
        SampledShaderCount = $bucket.SampledHashes.Count
        LoopedShaderCount = $bucket.LoopedHashes.Count
        DynamicCbShaderCount = $bucket.DynamicCbHashes.Count
        BufferTypes = @($bucket.BufferTypes.Keys | Sort-Object) -join ', '
        Sizes = @($bucket.Sizes.Keys | Sort-Object { [int]$_ }) -join ', '
    }
}

$variableVocabularyRows = foreach ($bucket in $variableVocabulary.Values) {
    [pscustomobject]@{
        BufferName = $bucket.BufferName
        VariableName = $bucket.VariableName
        ShaderCount = $bucket.Hashes.Count
        PsShaderCount = $bucket.PsHashes.Count
        VsShaderCount = $bucket.VsHashes.Count
        SampledShaderCount = $bucket.SampledHashes.Count
        LoopedShaderCount = $bucket.LoopedHashes.Count
        DynamicCbShaderCount = $bucket.DynamicCbHashes.Count
        TypeNames = @($bucket.TypeNames.Keys | Sort-Object) -join ', '
        Offsets = @($bucket.Offsets.Keys | Sort-Object { [int]$_ }) -join ', '
        Sizes = @($bucket.Sizes.Keys | Sort-Object { [int]$_ }) -join ', '
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# TS4 Material Shader Spec')
$lines.Add('')
$lines.Add(('Generated: `{0}`' -f [DateTimeOffset]::UtcNow.ToString('O')))
$lines.Add('')
$lines.Add('## Purpose')
$lines.Add('')
$lines.Add('This is the single implementation-oriented shader/material specification document for the main project.')
$lines.Add('')
$lines.Add('It is intended to answer:')
$lines.Add('- which runtime-used shaders exist')
$lines.Add('- what each shader expects in terms of textures, samplers, constant buffers, variables, and geometry semantics')
$lines.Add('- which shaders are directly linked back to embedded executable `DXBC` containers')
$lines.Add('- what the main project should eventually provide when reading package-side material definitions')
$lines.Add('')
$lines.Add('## Branch Goal')
$lines.Add('')
$lines.Add('The local goal of the `ts4-dx11-introspection` branch is not to build a second material system. Its job is to close the evidence gap between package-side authored materials and real runtime rendering.')
$lines.Add('')
$lines.Add('The branch should eventually provide a trustworthy closure across these layers:')
$lines.Add('- `MATD` / `MTST` package-side authored material data')
$lines.Add('- package-side shader/profile identifiers and parameter vocabularies')
$lines.Add('- runtime DX11 shader hashes and shader interfaces')
$lines.Add('- scene ownership and pass/draw context')
$lines.Add('')
$lines.Add('## Current Highest-Priority Gaps')
$lines.Add('')
$lines.Add('- No proven `MATD` / `MTST` -> runtime DX11 shader hash mapping exists yet.')
$lines.Add('- No trustworthy scene-pass or draw-pass ownership exists for runtime shader families.')
$lines.Add('- No stable draw/bind/state capture path exists inside the live game process without destabilizing TS4.')
$lines.Add('- The current spec is strong as an interface reference, but it still lacks final scene-pass closure for the main project.')
$lines.Add('')
$lines.Add('## Immediate Strategy')
$lines.Add('')
$lines.Add('- Keep the current stable runtime inventory and disassembly pipeline as ground truth.')
$lines.Add('- Prefer non-invasive external frame capture before adding any new risky in-process hooks.')
$lines.Add('- Use package-side `MATD` / `MTST` parsing already present in the main project as the authored-material layer of truth.')
$lines.Add('- Use runtime reflection/disassembly vocabulary to score candidate matches, but do not treat vocabulary overlap as final proof.')
$lines.Add('- Treat external GPU capture as the most promising path to establish scene-pass closure.')
$lines.Add('')
$lines.Add('## Scope')
$lines.Add('')
$lines.Add(('Input captures: `{0}`' -f ($resolvedInputs -join '`, `')))
$lines.Add(('Unique shaders: `{0}`' -f $total))
$lines.Add(('Pixel shaders: `{0}`' -f $psCount))
$lines.Add(('Vertex shaders: `{0}`' -f $vsCount))
$lines.Add(('Direct executable-linked shaders: `{0}`' -f $binaryLinked))
$lines.Add(('Executable disassembly available: `{0}`' -f $disassemblyCount))
$lines.Add('')
$lines.Add('## Disassembly Coverage')
$lines.Add('')
$lines.Add('Offline shader disassembly is currently available for executable-linked shaders extracted from embedded `DXBC` containers.')
$lines.Add('')
$lines.Add('- Disassembly source: `docs/raw/disassembly/embedded-x64/asm`')
$lines.Add(('- Unique shaders with assembly available: `{0}`' -f $disassemblyCount))
$lines.Add(('- Shaders with texture sampling instructions: `{0}`' -f $disassemblyWithSampling))
$lines.Add(('- Shaders with explicit `sample_l` instructions: `{0}`' -f $disassemblyWithSampleL))
$lines.Add(('- Shaders with explicit loop blocks: `{0}`' -f $disassemblyWithLoops))
$lines.Add(('- Shaders with dynamic constant-buffer indexing: `{0}`' -f $disassemblyWithDynamicCb))
$lines.Add('- Disassembly is raw evidence, but the important signals are folded into each shader entry below.')
$lines.Add('- Current per-shader disassembly signals: approximate instruction slots, texture sample count, explicit `sample_l` count, explicit loop count, explicit discard count, and whether the shader uses dynamic constant-buffer indexing.')
$lines.Add('')
$lines.Add('## Main-Project Reading Rules')
$lines.Add('')
$lines.Add('- Do not treat materials as opaque blobs; model them as candidates for satisfying known shader interfaces.')
$lines.Add('- A package-side material reader should aim to surface named texture bindings, sampler bindings, numeric/vector parameter bindings, and required geometry channels.')
$lines.Add('- A material path is only plausible if its package-side data can satisfy the runtime shader interface documented for the target shader or shader family.')
$lines.Add('- Vertex semantics matter. If a shader expects `POSITION1..POSITION7`, `NORMAL0`, `TANGENT0`, or extra color channels, the main project must preserve or model those channels explicitly.')
$lines.Add('- Bound resource names and cbuffer variable names are currently the strongest runtime clues for mapping package-side fields to shader-visible expectations.')
$lines.Add('- `MATD.shader` values are package-side material profile identifiers, not DX11 runtime bytecode hashes; they must be treated as a separate mapping layer.')
$lines.Add('')
$lines.Add('## Package-Side Material Layer')
$lines.Add('')
$lines.Add('This section captures what package-side material carriers are already visible in indexed game content. It should be used together with the runtime shader interface data below.')
$lines.Add('')
$lines.Add('### Implementation Priorities')
$lines.Add('')
$lines.Add('- Prioritize `MaterialDefinition` (`MATD`) parsing first. It is the main authored package-side material carrier and already exposes package-side material profile ids.')
$lines.Add('- Treat `MaterialSet` (`MTST`) as the next layer. It is much less frequent than `MATD`, but it likely selects material-state/material-variant combinations on top of referenced `MATD` entries.')
$lines.Add('- Keep `Geometry`, `Model`, and `ModelLOD` in the critical path. They are the main place where required vertex semantics and material references will have to line up with the runtime shader interface.')
$lines.Add('- Texture decoding still matters: `DSTImage`, `RLE2Image`, `RLESImage`, `LRLEImage`, and `PNGImage` form the practical package-side texture surface that authored materials can bind.')
$lines.Add('- For CAS and character work, keep `CASPart`, `RegionMap`, `BlendGeometry`, and `Rig` in scope because they shape which materials/textures/geometry variants are actually assembled.')
$lines.Add('')
if ($null -eq $matdCensus) {
    $lines.Add('- `MATD` census not loaded.')
}
else {
    $lines.Add(('`MATD` census source: `{0}`' -f $MatdCensusPath))
    $lines.Add(('- MaterialDefinition resources scanned: `{0}`' -f $matdCensus.MaterialDefinitionResources))
    $lines.Add(('- Packages containing MaterialDefinition resources: `{0}`' -f $matdCensus.MaterialDefinitionPackages))
    $lines.Add(('- Successfully decoded `MATD` resources: `{0}`' -f $matdCensus.DecodedResources))
    $lines.Add(('- Empty `MATD` payloads: `{0}`' -f $matdCensus.EmptyResources))
    $lines.Add(('- Decode failures: `{0}`' -f $matdCensus.Failures))
    $lines.Add('- Important: these counts describe package-side material profile ids embedded in `MATD`, not runtime DX11 shader blobs.')
    $lines.Add('')
    $lines.Add('### Top `MATD` Material Profiles')
    $lines.Add('')
    $lines.Add('| Profile Name | Shader/Profile Id | Count | Package Coverage |')
    $lines.Add('| --- | --- | ---: | ---: |')
    $topMatdProfiles = @($matdCensus.TopProfiles | Select-Object -First 12)
    $topMatdHashes = @($matdCensus.TopShaderHashes | Select-Object -First 12)
    for ($index = 0; $index -lt [Math]::Min($topMatdProfiles.Count, $topMatdHashes.Count); $index++) {
        $profile = $topMatdProfiles[$index]
        $shaderId = $topMatdHashes[$index]
        $lines.Add(('| `{0}` | `{1}` | {2} | {3} |' -f
                $profile.Name,
                $shaderId.Name,
                $profile.Count,
                $profile.PackageCoverage))
    }
}
$lines.Add('')
if ($null -eq $resourceTypeCensus) {
    $lines.Add('- Resource type census not loaded.')
}
else {
    $lines.Add(('Resource type census source: `{0}`' -f $ResourceTypeCensusPath))
    $lines.Add(('- Indexed resources in cache: `{0}`' -f $resourceTypeCensus.TotalResources))
    $lines.Add(('- Indexed packages in cache: `{0}`' -f $resourceTypeCensus.TotalPackages))
    $lines.Add('')
    $lines.Add('### Material-Carrying Resource Types')
    $lines.Add('')
    if ($materialRelevantTypeRows.Count -eq 0) {
        $lines.Add('- none')
    }
    else {
        $lines.Add('| Type Name | Count | Package Coverage |')
        $lines.Add('| --- | ---: | ---: |')
        foreach ($row in ($materialRelevantTypeRows | Sort-Object @{Expression='Count';Descending=$true}, Name)) {
            $lines.Add(('| `{0}` | {1} | {2} |' -f $row.Name, $row.Count, $row.PackageCoverage))
        }
    }
}
$lines.Add('')
$lines.Add('## Interface Vocabulary')
$lines.Add('')
$lines.Add('This section aggregates the interface names that recur across the current shader population. The `sampled`, `looped`, and `dynamic-CB` columns are restricted to the executable-linked subpopulation with assembly coverage.')
$lines.Add('')
$lines.Add('### Bound Resource Names')
$lines.Add('')
if ($resourceVocabularyRows.Count -eq 0) {
    $lines.Add('- none')
}
else {
    $lines.Add('| Name | Shader Count | PS | VS | Sampled | Looped | Dynamic-CB | Types | Dimensions |')
    $lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |')
    foreach ($row in ($resourceVocabularyRows | Sort-Object @{Expression='ShaderCount';Descending=$true}, Name)) {
        $lines.Add(('| `{0}` | {1} | {2} | {3} | {4} | {5} | {6} | `{7}` | `{8}` |' -f
                $row.Name,
                $row.ShaderCount,
                $row.PsShaderCount,
                $row.VsShaderCount,
                $row.SampledShaderCount,
                $row.LoopedShaderCount,
                $row.DynamicCbShaderCount,
                $(if ($row.Types) { $row.Types } else { '-' }),
                $(if ($row.Dimensions) { $row.Dimensions } else { '-' })))
    }
}
$lines.Add('')
$lines.Add('### Constant Buffer Names')
$lines.Add('')
if ($cbufferVocabularyRows.Count -eq 0) {
    $lines.Add('- none')
}
else {
    $lines.Add('| Name | Shader Count | PS | VS | Sampled | Looped | Dynamic-CB | Buffer Types | Sizes |')
    $lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |')
    foreach ($row in ($cbufferVocabularyRows | Sort-Object @{Expression='ShaderCount';Descending=$true}, Name)) {
        $lines.Add(('| `{0}` | {1} | {2} | {3} | {4} | {5} | {6} | `{7}` | `{8}` |' -f
                $row.Name,
                $row.ShaderCount,
                $row.PsShaderCount,
                $row.VsShaderCount,
                $row.SampledShaderCount,
                $row.LoopedShaderCount,
                $row.DynamicCbShaderCount,
                $(if ($row.BufferTypes) { $row.BufferTypes } else { '-' }),
                $(if ($row.Sizes) { $row.Sizes } else { '-' })))
    }
}
$lines.Add('')
$lines.Add('### Constant Buffer Variables')
$lines.Add('')
if ($variableVocabularyRows.Count -eq 0) {
    $lines.Add('- none')
}
else {
    $lines.Add('| Buffer | Variable | Shader Count | PS | VS | Sampled | Looped | Dynamic-CB | Types | Offsets | Sizes |')
    $lines.Add('| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- |')
    foreach ($row in ($variableVocabularyRows | Sort-Object @{Expression='ShaderCount';Descending=$true}, BufferName, VariableName)) {
        $lines.Add(('| `{0}` | `{1}` | {2} | {3} | {4} | {5} | {6} | {7} | `{8}` | `{9}` | `{10}` |' -f
                $row.BufferName,
                $row.VariableName,
                $row.ShaderCount,
                $row.PsShaderCount,
                $row.VsShaderCount,
                $row.SampledShaderCount,
                $row.LoopedShaderCount,
                $row.DynamicCbShaderCount,
                $(if ($row.TypeNames) { $row.TypeNames } else { '-' }),
                $(if ($row.Offsets) { $row.Offsets } else { '-' }),
                $(if ($row.Sizes) { $row.Sizes } else { '-' })))
    }
}
$lines.Add('')
$lines.Add('## Shader Index')
$lines.Add('')
$lines.Add('| Hash | Stage | Support | Bytecode | Reflection | Inputs | Outputs | Binary Link | Disasm |')
$lines.Add('| --- | --- | ---: | ---: | --- | --- | --- | --- | --- |')
foreach ($entry in ($entries | Sort-Object Stage, @{Expression='CaptureSupport';Descending=$true}, Hash)) {
    $linkState = if ($entry.BinaryLinked) { 'yes' } else { 'no' }
    $disasmState = if ($null -ne $entry.Disassembly) { 'yes' } else { 'no' }
    $lines.Add(('| `{0}` | `{1}` | {2} | {3} | `{4}` | `{5}` | `{6}` | {7} | {8} |' -f
            $entry.Hash,
            $entry.Stage,
            $entry.CaptureSupport,
            $entry.BytecodeSize,
            $entry.ReflectionSignature,
            $entry.InputSemanticsSignature,
            $entry.OutputSemanticsSignature,
            $linkState,
            $disasmState))
}

foreach ($stageGroup in ($entries | Group-Object Stage | Sort-Object Name)) {
    $lines.Add('')
    $lines.Add(('## `{0}` Shader Details' -f $stageGroup.Name))
    foreach ($entry in ($stageGroup.Group | Sort-Object @{Expression='CaptureSupport';Descending=$true}, Hash)) {
        $lines.Add('')
        $lines.Add(('### `{0}`' -f $entry.Hash))
        $lines.Add('')
        $lines.Add(('- Stage: `{0}`' -f $entry.Stage))
        $lines.Add(('- Bytecode size: `{0}`' -f $entry.BytecodeSize))
        $lines.Add(('- Capture support: `{0}`' -f $entry.CaptureSupport))
        $lines.Add(('- Seen in captures: `{0}`' -f ($entry.SeenInCaptures -join '`, `')))
        $lines.Add(('- Reflection signature: `{0}`' -f $entry.ReflectionSignature))
        $lines.Add(('- Instruction count: `{0}`' -f $entry.InstructionCount))
        $lines.Add(('- Temp register count: `{0}`' -f $entry.TempRegisterCount))
        $lines.Add(('- Input semantics: `{0}`' -f $entry.InputSemanticsSignature))
        $lines.Add(('- Output semantics: `{0}`' -f $entry.OutputSemanticsSignature))
        $lines.Add(('- Binary-linked in `TS4_x64.exe`: `{0}`' -f $(if ($entry.X64Offsets.Count -gt 0) { $entry.X64Offsets -join '`, `' } else { 'no' })))
        $lines.Add(('- Binary-linked in `TS4_x64_fpb.exe`: `{0}`' -f $(if ($entry.FpbOffsets.Count -gt 0) { $entry.FpbOffsets -join '`, `' } else { 'no' })))
        if ($null -eq $entry.Disassembly) {
            $lines.Add('- Disassembly available: `no`')
        }
        else {
            $lines.Add('- Disassembly available: `yes`')
            $lines.Add(('- Disassembly path: `{0}`' -f $entry.Disassembly.RelativePath))
            $lines.Add(('- Approximate instruction slots from assembly: `{0}`' -f $entry.Disassembly.ApproxInstructionSlots))
            $lines.Add(('- Texture sample instructions: `{0}`' -f $entry.Disassembly.SampleCount))
            $lines.Add(('- Explicit `sample_l` instructions: `{0}`' -f $entry.Disassembly.SampleLCount))
            $lines.Add(('- Explicit loop blocks: `{0}`' -f $entry.Disassembly.LoopCount))
            $lines.Add(('- Explicit discard instructions: `{0}`' -f $entry.Disassembly.DiscardCount))
            $lines.Add(('- Uses dynamic constant-buffer indexing: `{0}`' -f $(if ($entry.Disassembly.UsesDynamicConstantBufferIndexing) { 'yes' } else { 'no' })))
        }
        $lines.Add('')
        $lines.Add('#### Bound Resources')
        if ($entry.BoundResources.Count -eq 0) {
            $lines.Add('')
            $lines.Add('- none')
        }
        else {
            $lines.Add('')
            $lines.Add('| Name | Type | Bind Point | Bind Count | Dimension |')
            $lines.Add('| --- | --- | ---: | ---: | ---: |')
            foreach ($resource in $entry.BoundResources) {
                $lines.Add(('| `{0}` | `{1}` | {2} | {3} | {4} |' -f
                        $resource.name,
                        $resource.type,
                        $resource.bind_point,
                        $resource.bind_count,
                        $resource.dimension))
            }
        }

        $lines.Add('')
        $lines.Add('#### Constant Buffers')
        if ($entry.ConstantBuffers.Count -eq 0) {
            $lines.Add('')
            $lines.Add('- none')
        }
        else {
            foreach ($cbuffer in $entry.ConstantBuffers) {
                $lines.Add('')
                $lines.Add(('- Buffer `{0}`: type=`{1}`, size=`{2}`, variable_count=`{3}`' -f
                        $cbuffer.name,
                        $cbuffer.buffer_type,
                        $cbuffer.size,
                        $cbuffer.variable_count))
                if (@($cbuffer.variables).Count -gt 0) {
                    $lines.Add('')
                    $lines.Add('| Variable | Type | Start Offset | Size | Rows | Cols | Elements | Members |')
                    $lines.Add('| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |')
                    foreach ($variable in $cbuffer.variables) {
                        $lines.Add(('| `{0}` | `{1}` | {2} | {3} | {4} | {5} | {6} | {7} |' -f
                                $variable.name,
                                $variable.type_name,
                                $variable.start_offset,
                                $variable.size,
                                $variable.rows,
                                $variable.columns,
                                $variable.elements,
                                $variable.members))
                    }
                }
            }
        }

        $lines.Add('')
        $lines.Add('#### Input Parameters')
        if ($entry.InputParameters.Count -eq 0) {
            $lines.Add('')
            $lines.Add('- none')
        }
        else {
            $lines.Add('')
            $lines.Add('| Semantic | Register | Mask | Component Type | System Value |')
            $lines.Add('| --- | ---: | ---: | --- | --- |')
            foreach ($parameter in $entry.InputParameters) {
                $lines.Add(('| `{0}{1}` | {2} | {3} | `{4}` | `{5}` |' -f
                        $parameter.semantic_name,
                        $parameter.semantic_index,
                        $parameter.register_index,
                        $parameter.component_mask,
                        $parameter.component_type,
                        $parameter.system_value_type))
            }
        }

        $lines.Add('')
        $lines.Add('#### Output Parameters')
        if ($entry.OutputParameters.Count -eq 0) {
            $lines.Add('')
            $lines.Add('- none')
        }
        else {
            $lines.Add('')
            $lines.Add('| Semantic | Register | Mask | Component Type | System Value |')
            $lines.Add('| --- | ---: | ---: | --- | --- |')
            foreach ($parameter in $entry.OutputParameters) {
                $lines.Add(('| `{0}{1}` | {2} | {3} | `{4}` | `{5}` |' -f
                        $parameter.semantic_name,
                        $parameter.semantic_index,
                        $parameter.register_index,
                        $parameter.component_mask,
                        $parameter.component_type,
                        $parameter.system_value_type))
            }
        }
    }
}

Set-Content -Path $outputPath -Value $lines -Encoding utf8
Write-Host "Wrote master spec to $outputPath"
