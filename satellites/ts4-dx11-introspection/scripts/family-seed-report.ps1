$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$catalogPath = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\docs\raw\shader-catalog.json'
$catalog = Get-Content -Raw $catalogPath | ConvertFrom-Json

$families = @(
    @{
        Name = 'PS_SCREENSPACE_BARE'
        Stage = 'ps'
        Input = 'SV_POSITION0'
        Output = 'SV_Target0'
    },
    @{
        Name = 'PS_SCREENSPACE_UV'
        Stage = 'ps'
        Input = 'SV_POSITION0, TEXCOORD0'
        Output = 'SV_Target0'
    },
    @{
        Name = 'PS_TEXCOORD1'
        Stage = 'ps'
        Input = 'TEXCOORD0'
        Output = 'SV_Target0'
    },
    @{
        Name = 'PS_TEXCOORD3'
        Stage = 'ps'
        Input = 'TEXCOORD0, TEXCOORD1, TEXCOORD2'
        Output = 'SV_Target0'
    },
    @{
        Name = 'PS_COLOR_TEX4'
        Stage = 'ps'
        Input = 'COLOR0, TEXCOORD0, TEXCOORD1, TEXCOORD2, TEXCOORD3'
        Output = 'SV_Target0'
    },
    @{
        Name = 'VS_SIMPLE_POSITION'
        Stage = 'vs'
        Input = 'POSITION0'
        Output = 'SV_POSITION0'
    },
    @{
        Name = 'VS_MESH12_10'
        Stage = 'vs'
        Input = 'POSITION0, NORMAL0, POSITION1, POSITION2, POSITION3, POSITION4, POSITION5, POSITION6, POSITION7, TEXCOORD0, COLOR0, TANGENT0'
        Output = 'COLOR0, COLOR1, TEXCOORD0, TEXCOORD1, TEXCOORD2, TEXCOORD3, TEXCOORD5, TEXCOORD6, TEXCOORD7, SV_POSITION0'
    },
    @{
        Name = 'VS_MESH12_11'
        Stage = 'vs'
        Input = 'POSITION0, NORMAL0, POSITION1, POSITION2, POSITION3, POSITION4, POSITION5, POSITION6, POSITION7, TEXCOORD0, COLOR0, TANGENT0'
        Output = 'COLOR0, TEXCOORD0, TEXCOORD1, COLOR1, TEXCOORD4, TEXCOORD2, TEXCOORD3, TEXCOORD5, TEXCOORD6, TEXCOORD7, SV_POSITION0'
    },
    @{
        Name = 'VS_MESH10_8'
        Stage = 'vs'
        Input = 'POSITION0, NORMAL0, POSITION1, POSITION2, POSITION3, POSITION4, POSITION5, POSITION6, POSITION7, TEXCOORD0'
        Output = 'COLOR0, COLOR1, TEXCOORD0, TEXCOORD1, TEXCOORD2, TEXCOORD3, TEXCOORD5, SV_POSITION0'
    }
)

foreach ($family in $families) {
    $rows = @($catalog.Entries | Where-Object {
            $_.Stage -eq $family.Stage -and
            $_.InputSemanticsSignature -eq $family.Input -and
            $_.OutputSemanticsSignature -eq $family.Output
        })

    $support3 = @($rows | Where-Object { $_.CaptureSupport -eq 3 }).Count
    $support1 = @($rows | Where-Object { $_.CaptureSupport -eq 1 }).Count
    $topSignature = @($rows | Group-Object ReflectionSignature | Sort-Object Count -Descending | Select-Object -First 1)
    $signatureName = if ($topSignature.Count -gt 0) { $topSignature[0].Name } else { '-' }

    Write-Output ("FAMILY`t{0}`tcount={1}`tsupport3={2}`tsupport1={3}`ttopSig={4}" -f $family.Name, $rows.Count, $support3, $support1, $signatureName)

    foreach ($row in ($rows | Sort-Object -Property @{Expression = 'CaptureSupport'; Descending = $true }, @{Expression = 'Hash'; Descending = $false } | Select-Object -First 5)) {
        Write-Output ("HASH`t{0}`t{1}`t{2}`t{3}" -f $row.Hash, $row.CaptureSupport, $row.BytecodeSize, $row.ReflectionSignature)
    }
}

Write-Output "RARE_SUPPORT"
$catalog.Entries |
    Where-Object { $_.CaptureSupport -eq 1 } |
    Group-Object Stage |
    Sort-Object Name |
    ForEach-Object {
        Write-Output ("{0}`t{1}" -f $_.Name, $_.Count)
    }
