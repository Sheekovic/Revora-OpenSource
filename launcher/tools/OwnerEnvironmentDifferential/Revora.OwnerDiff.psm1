Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-RevoraRedactedText {
    param([AllowNull()][string]$Text, [string]$ClientRoot)
    if ($null -eq $Text) { return $null }
    $value = $Text
    if ($ClientRoot) { $value = [regex]::Replace($value, [regex]::Escape(([IO.Path]::GetFullPath($ClientRoot)).TrimEnd('\')), '<CLIENT_ROOT>', 'IgnoreCase') }
    if ($env:USERPROFILE) { $value = [regex]::Replace($value, [regex]::Escape($env:USERPROFILE), '<USER_PROFILE>', 'IgnoreCase') }
    if ($env:USERNAME) { $value = [regex]::Replace($value, [regex]::Escape($env:USERNAME), '<USER>', 'IgnoreCase') }
    $value = [regex]::Replace($value, '(?i)(blacknull\s+)[0-9a-f]{8}\b', '$1<REDACTED_SESSION>')
    $value = [regex]::Replace($value, '(?im)^\s*([^=]*(?:password|passwd|credential|token|grant|private.?key|secret)[^=]*)=.*$', '$1=<REDACTED>')
    return $value
}

function Get-RevoraBoundedTextEvidence {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$ClientRoot,[int]$MaximumBytes=262144,[switch]$RelevantLinesOnly)
    $file=Get-RevoraFileEvidence $Path $ClientRoot
    if(-not$file.exists){return $file}
    if($file.size -gt $MaximumBytes){$file.contentStatus='OmittedOversize';return $file}
    try{$text=ConvertTo-RevoraRedactedText ([IO.File]::ReadAllText([IO.Path]::GetFullPath($Path))) $ClientRoot;if($RelevantLinesOnly){$text=@($text -split "`r?`n"|Where-Object{$_ -match '(?i)error|fail|exception|crash|TQSafe|license|DirectX|DX8|DX9|pipe|Conquer'}|Select-Object -Last 200)-join"`n"};$file.content=$text;$file.contentStatus=if($RelevantLinesOnly){'CapturedRelevantRedactedLines'}else{'CapturedRedacted'}}catch{$file.contentStatus='UnreadableText'}
    return $file
}

function Get-RevoraFileEvidence {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$ClientRoot)
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        return [ordered]@{ path = ConvertTo-RevoraRedactedText $full $ClientRoot; exists = $false }
    }
    $item = Get-Item -LiteralPath $full -Force
    $signature = Get-AuthenticodeSignature -LiteralPath $full
    $zone = Get-Item -LiteralPath $full -Stream Zone.Identifier -ErrorAction SilentlyContinue
    $acl = Get-Acl -LiteralPath $full
    [ordered]@{
        path = ConvertTo-RevoraRedactedText $full $ClientRoot
        relativePath = if ($full.StartsWith(([IO.Path]::GetFullPath($ClientRoot)).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { $full.Substring(([IO.Path]::GetFullPath($ClientRoot)).TrimEnd('\').Length).TrimStart('\').Replace('\','/') } else { $null }
        exists = $true; size = $item.Length; sha256 = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash
        authenticode = [string]$signature.Status
        signerThumbprint = if ($signature.SignerCertificate) { $signature.SignerCertificate.Thumbprint } else { $null }
        zoneIdentifier = [bool]$zone; lastWriteUtc=$item.LastWriteTimeUtc.ToString('O')
        owner = ConvertTo-RevoraRedactedText $acl.Owner $ClientRoot
        aclFingerprintSha256 = (([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($acl.Sddl)) | ForEach-Object { $_.ToString('X2') }) -join '')
        attributes = [string]$item.Attributes
    }
}

function Get-RevoraProcessRecord {
    param([Parameter(Mandatory)][int]$ProcessId, [Parameter(Mandatory)][string]$ClientRoot)
    $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction SilentlyContinue
    if (-not $cim) { return $null }
    [ordered]@{
        processId = [int]$cim.ProcessId; parentProcessId = [int]$cim.ParentProcessId; name = [string]$cim.Name
        executablePath = ConvertTo-RevoraRedactedText ([string]$cim.ExecutablePath) $ClientRoot
        commandLine = ConvertTo-RevoraRedactedText ([string]$cim.CommandLine) $ClientRoot
        creationUtc = if ($cim.CreationDate -is [DateTime]) { ([DateTime]$cim.CreationDate).ToUniversalTime().ToString('O') }
            elseif ($cim.CreationDate) { ([Management.ManagementDateTimeConverter]::ToDateTime([string]$cim.CreationDate)).ToUniversalTime().ToString('O') }
            else { $null }
    }
}

function Get-RevoraLoadedModules {
    param([Parameter(Mandatory)][int]$ProcessId, [Parameter(Mandatory)][string]$ClientRoot)
    $records = [Collections.Generic.List[object]]::new()
    try {
        $process = Get-Process -Id $ProcessId -ErrorAction Stop
        foreach ($module in $process.Modules) {
            try { $records.Add((Get-RevoraFileEvidence -Path $module.FileName -ClientRoot $ClientRoot)) }
            catch { $records.Add([ordered]@{ path = ConvertTo-RevoraRedactedText $module.FileName $ClientRoot; error = $_.Exception.GetType().Name }) }
        }
    } catch { $records.Add([ordered]@{ error = 'ModuleEnumerationUnavailable'; detail = $_.Exception.GetType().Name }) }
    return @($records | Sort-Object { $_.path } -Unique)
}

function Get-RevoraTcpMetadata {
    param([Parameter(Mandatory)][int]$ProcessId, [Parameter(Mandatory)][string]$TimestampUtc)
    @(Get-NetTCPConnection -OwningProcess $ProcessId -ErrorAction SilentlyContinue | ForEach-Object {
        [ordered]@{ processId=$ProcessId; timestampUtc=$TimestampUtc; destinationIp=$_.RemoteAddress; destinationPort=$_.RemotePort; state=[string]$_.State }
    })
}

function Test-RevoraEventCorrelation {
    param(
        [Parameter(Mandatory)]$Event,
        [Parameter(Mandatory)][string[]]$ApprovedExecutablePaths,
        [Parameter(Mandatory)][int[]]$ObservedProcessIds,
        [Parameter(Mandatory)][DateTime]$ObservationStart,
        [Parameter(Mandatory)][DateTime]$ObservationEnd)
    $timeProperty=$Event.PSObject.Properties['TimeCreated']
    if(-not$timeProperty-or$null-eq$timeProperty.Value){return $false}
    $time=[DateTime]$timeProperty.Value
    if($time-lt$ObservationStart-or$time-gt$ObservationEnd){return $false}
    $values=[Collections.Generic.List[string]]::new()
    $messageProperty=$Event.PSObject.Properties['Message'];if($messageProperty-and$messageProperty.Value){$values.Add([string]$messageProperty.Value)}
    $evidenceProperty=$Event.PSObject.Properties['EvidenceValues'];if($evidenceProperty){foreach($value in @($evidenceProperty.Value)){if($null-ne$value){$values.Add([string]$value)}}}
    $propertiesProperty=$Event.PSObject.Properties['Properties'];if($propertiesProperty){foreach($property in @($propertiesProperty.Value)){if($property-and$property.Value){$values.Add([string]$property.Value)}}}
    $pathMatched=$false
    foreach($approved in @($ApprovedExecutablePaths)){
        $exact=[IO.Path]::GetFullPath($approved)
        foreach($value in @($values)){
            if($value.Equals($exact,[StringComparison]::OrdinalIgnoreCase)-or
               [regex]::IsMatch($value,[regex]::Escape($exact)+'(?![\\\w])',[Text.RegularExpressions.RegexOptions]::IgnoreCase)){$pathMatched=$true;break}
        }
        if($pathMatched){break}
    }
    if(-not$pathMatched){return $false}
    $targetPids=[Collections.Generic.List[int]]::new()
    $targetProperty=$Event.PSObject.Properties['TargetProcessId'];if($targetProperty-and$null-ne$targetProperty.Value){$targetPids.Add([int]$targetProperty.Value)}
    try{
        [xml]$xml=$Event.ToXml()
        foreach($data in @($xml.Event.EventData.Data)){
            if($data.Name-match'^(?:ProcessId|FaultingProcessId|AppProcessId)$'){
                $raw=[string]$data.'#text';$parsed=0
                if($raw-match'^0x[0-9a-f]+$'){$parsed=[Convert]::ToInt32($raw.Substring(2),16)}elseif([int]::TryParse($raw,[ref]$parsed)){}
                if($parsed-gt 0){$targetPids.Add($parsed)}
            }
        }
    }catch{}
    if($targetPids.Count -gt 0 -and -not @($targetPids|Where-Object{$ObservedProcessIds -contains $_}).Count){return $false}
    return $true
}

function Get-RevoraRankedDifferences {
    param([Parameter(Mandatory)]$Owner, [Parameter(Mandatory)]$Server)
    $differences = [Collections.Generic.List[object]]::new()
    function Add-Difference([int]$Score,[string]$Category,[string]$Evidence) { $differences.Add([ordered]@{ score=$Score; category=$Category; evidence=$Evidence }) }
    $ownerEvents = @($Owner.run.events | ForEach-Object { "$( $_.providerName ):$( $_.eventId ):$( $_.message )" })
    $serverEvents = @($Server.run.events | ForEach-Object { "$( $_.providerName ):$( $_.eventId ):$( $_.message )" })
    foreach ($event in $ownerEvents) { if ($serverEvents -notcontains $event) { Add-Difference 100 'Owner-only application/security event' $event } }
    if (($Owner.system.remoteSession -ne $Server.system.remoteSession)) { Add-Difference 75 'Remote-session state differs' "owner=$($Owner.system.remoteSession), server=$($Server.system.remoteSession)" }
    foreach ($file in @($Owner.files)) {
        $peer = @($Server.files | Where-Object relativePath -eq $file.relativePath)[0]
        if (-not $peer) { Add-Difference 95 'Critical/startup file differs' "$($file.relativePath): absent from server report" }
        elseif ($file.exists -ne $peer.exists -or $file.sha256 -ne $peer.sha256) { Add-Difference 95 'Critical/startup file differs' "$($file.relativePath): owner=$($file.sha256), server=$($peer.sha256)" }
        elseif ($file.zoneIdentifier -ne $peer.zoneIdentifier -or $file.aclFingerprintSha256 -ne $peer.aclFingerprintSha256) { Add-Difference 80 'File trust/ACL differs' "$($file.relativePath): zone or ACL differs" }
    }
    $ownerModules = @($Owner.run.modules | ForEach-Object { "$($_.path)|$($_.sha256)" })
    $serverModules = @($Server.run.modules | ForEach-Object { "$($_.path)|$($_.sha256)" })
    foreach ($module in $ownerModules) { if ($serverModules -notcontains $module) { Add-Difference 90 'Owner-only loaded module' $module } }
    if (($Owner.compatibility | ConvertTo-Json -Depth 8 -Compress) -ne ($Server.compatibility | ConvertTo-Json -Depth 8 -Compress)) { Add-Difference 88 'Compatibility or mitigation policy differs' 'AppCompat/mitigation evidence differs between machines.' }
    if (($Owner.securityProducts | ConvertTo-Json -Depth 6 -Compress) -ne ($Server.securityProducts | ConvertTo-Json -Depth 6 -Compress)) { Add-Difference 85 'Security-product environment differs' 'Installed security products differ; correlate with recorded block events.' }
    if (($Owner.runtimes | ConvertTo-Json -Depth 8 -Compress) -ne ($Server.runtimes | ConvertTo-Json -Depth 8 -Compress)) { Add-Difference 70 'Legacy DirectX/VC++ runtime differs' 'Legacy runtime inventory differs.' }
    if (($Owner.display | ConvertTo-Json -Depth 8 -Compress) -ne ($Server.display | ConvertTo-Json -Depth 8 -Compress)) { Add-Difference 45 'Display environment differs' 'Adapter, driver, monitor, or DPI evidence differs.' }
    if (($Owner.system.osBuild -ne $Server.system.osBuild) -or ($Owner.system.edition -ne $Server.system.edition)) { Add-Difference 40 'Windows version differs' "owner=$($Owner.system.edition) $($Owner.system.osBuild); server=$($Server.system.edition) $($Server.system.osBuild)" }
    $ranked = @($differences | Sort-Object @{Expression='score';Descending=$true}, category)
    if (@($ranked).Count -eq 0) { return [ordered]@{ mostLikelyCause='No evidenced environmental difference in collected scope'; confidence='insufficient'; rankedDifferences=@() } }
    [ordered]@{ mostLikelyCause=$ranked[0].category; confidence=if($ranked[0].score -ge 90){'high'}elseif($ranked[0].score -ge 70){'medium'}else{'low'}; rankedDifferences=$ranked }
}

Export-ModuleMember -Function ConvertTo-RevoraRedactedText,Get-RevoraFileEvidence,Get-RevoraBoundedTextEvidence,Get-RevoraProcessRecord,Get-RevoraLoadedModules,Get-RevoraTcpMetadata,Test-RevoraEventCorrelation,Get-RevoraRankedDifferences
