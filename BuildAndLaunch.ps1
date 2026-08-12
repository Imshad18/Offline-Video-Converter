$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'VideoConverter.cs'
$exe = Join-Path $root 'Universal Video to MP4.exe'
$log = Join-Path $root 'build_error.txt'

try {
    if (-not (Test-Path $exe) -or ((Get-Item $src).LastWriteTimeUtc -gt (Get-Item $exe).LastWriteTimeUtc)) {
        if (Test-Path $exe) { Remove-Item $exe -Force }
        Add-Type -Path $src `
            -ReferencedAssemblies 'System.Windows.Forms','System.Drawing','System.IO.Compression','System.IO.Compression.FileSystem','System.Net' `
            -OutputAssembly $exe `
            -OutputType WindowsApplication
    }
    if (Test-Path $log) { Remove-Item $log -Force }
    Start-Process -FilePath $exe -WorkingDirectory $root
}
catch {
    $_ | Out-String | Set-Content -Path $log -Encoding UTF8
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        "Could not start the app.`r`n`r`n$($_.Exception.Message)`r`n`r`nDetails were saved to build_error.txt",
        'Universal Video to MP4',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    exit 1
}
