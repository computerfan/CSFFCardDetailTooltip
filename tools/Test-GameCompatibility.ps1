param(
    [Parameter(Mandatory = $true)][string]$GameAssemblyPath,
    [Parameter(Mandatory = $true)][string]$PluginPath,
    [Parameter(Mandatory = $true)][string]$CecilPath
)

# Read metadata only: this does not load Unity, patch the game, or touch saves.
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path -LiteralPath $CecilPath).Path
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
foreach ($directory in $resolver.GetSearchDirectories()) { $resolver.RemoveSearchDirectory($directory) }
$resolver.AddSearchDirectory((Resolve-Path -LiteralPath $GameAssemblyPath).Path)
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly(
    (Join-Path $GameAssemblyPath 'Assembly-CSharp.dll'), $parameters)
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly(
    (Resolve-Path -LiteralPath $PluginPath).Path, $parameters)
$failures = [Collections.Generic.List[string]]::new()
$memberCount = 0
$patchCount = 0

try {
    foreach ($member in $plugin.MainModule.GetMemberReferences()) {
        if ($member.DeclaringType.Scope.Name -ne 'Assembly-CSharp') { continue }
        $memberCount++
        try {
            if ($null -eq $member.Resolve()) { $failures.Add("Missing member: $($member.FullName)") }
        }
        catch { $failures.Add("Cannot resolve: $($member.FullName): $($_.Exception.Message)") }
    }

    foreach ($type in $plugin.MainModule.Types) {
        foreach ($patch in $type.Methods) {
            foreach ($attribute in $patch.CustomAttributes) {
                if ($attribute.AttributeType.FullName -ne 'HarmonyLib.HarmonyPatch') { continue }
                # This mod declares each target using [HarmonyPatch(typeof(T), "Method")].
                if ($attribute.ConstructorArguments.Count -ne 2) {
                    $failures.Add("Unsupported patch declaration: $($patch.FullName)")
                    continue
                }
                $targetTypeName = $attribute.ConstructorArguments[0].Value.FullName
                $targetName = [string]$attribute.ConstructorArguments[1].Value
                $targetType = $game.MainModule.GetType($targetTypeName)
                $targets = @()
                while ($null -ne $targetType) {
                    $targets = @($targetType.Methods | Where-Object Name -eq $targetName)
                    if ($targets.Count -gt 0 -or $null -eq $targetType.BaseType) { break }
                    $targetType = $targetType.BaseType.Resolve()
                }
                $patchCount++
                if ($targets.Count -ne 1) {
                    $failures.Add("Expected one target for ${targetTypeName}::${targetName}, found $($targets.Count)")
                    continue
                }
                foreach ($argument in $patch.Parameters) {
                    if ($argument.Name.StartsWith('__')) { continue }
                    $original = @($targets[0].Parameters | Where-Object Name -eq $argument.Name)
                    if ($original.Count -ne 1 -or
                        $original[0].ParameterType.FullName -ne $argument.ParameterType.FullName) {
                        $failures.Add("Patch argument mismatch: $($patch.Name) / $($argument.Name)")
                    }
                }
            }
        }
    }
    if ($memberCount -eq 0 -or $patchCount -eq 0) { $failures.Add('No game references or patches found.') }
    if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
    "PASS: $memberCount game member references and $patchCount Harmony targets/arguments match."
    "Game DLL SHA256: $((Get-FileHash -LiteralPath (Join-Path $GameAssemblyPath 'Assembly-CSharp.dll')).Hash)"
    'This is a static compatibility check; in-game behavior still requires smoke testing.'
}
finally {
    $plugin.Dispose()
    $game.Dispose()
    $resolver.Dispose()
}
