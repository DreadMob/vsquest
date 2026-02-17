# Script to fix empty catch blocks in vsquest codebase
# This script will find all empty catch blocks and replace them with proper exception logging

Get-ChildItem -Path "src" -Recurse -Filter "*.cs" | ForEach-Object {
    $file = $_.FullName
    $content = Get-Content -Path $file -Raw
    
    # Pattern to match empty catch blocks with various whitespace patterns
    $pattern = 'catch\s*\(\s*\)\s*\{\s*\}'
    $replacement = 'catch (Exception e)'
    
    # We need to be more careful and context-aware, so let's use a more sophisticated approach
    Write-Host "Processing file: $file"
    
    # Find lines with empty catch blocks
    $lines = Get-Content -Path $file
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match 'catch\s*\(\s*\)\s*\{\s*\}') {
            Write-Host "Found empty catch at line $($i+1) in $file"
        }
        elseif ($line -match 'catch\s*\{') {
            # Check if next line is just closing brace
            if ($i + 1 -lt $lines.Count -and $lines[$i+1].Trim() -eq '}') {
                Write-Host "Found multi-line empty catch at line $($i+1) in $file"
            }
        }
    }
}

Write-Host "Scan complete. Please use manual editing for precise fixes."
