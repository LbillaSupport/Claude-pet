# Assembles numbered PNG frames (frame_0000.png …) into a looping animated GIF.
#
# Why a script and not the app: the app emits clean PNG frames (Skia), and we stitch them here.
# Windows has no ffmpeg/ImageMagick by default, so this drives the GDI+ (System.Drawing) native
# GIF encoder via Save + SaveAdd (the documented multi-frame path), then patches the per-frame
# delay and the NETSCAPE loop block into the bytes (GDI+ writes neither). All built-in, no tools.
#
# Usage: .\tools\GifAssembler.ps1 -FramesDir _frames -Out docs\assets\demo.gif -DelayCs 5

param(
    [Parameter(Mandatory = $true)][string]$FramesDir,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$DelayCs = 5   # per-frame delay in centiseconds (5 = 20 fps)
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$files = Get-ChildItem $FramesDir -Filter *.png | Sort-Object Name
if ($files.Count -eq 0) { throw "No PNG frames in $FramesDir" }

$gifCodec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
    Where-Object { $_.FormatID -eq [System.Drawing.Imaging.ImageFormat]::Gif.Guid } | Select-Object -First 1

$epMulti = New-Object System.Drawing.Imaging.EncoderParameters 1
$epMulti.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
    [System.Drawing.Imaging.Encoder]::SaveFlag, [long][System.Drawing.Imaging.EncoderValue]::MultiFrame)
$epNext = New-Object System.Drawing.Imaging.EncoderParameters 1
$epNext.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
    [System.Drawing.Imaging.Encoder]::SaveFlag, [long][System.Drawing.Imaging.EncoderValue]::FrameDimensionTime)
$epFlush = New-Object System.Drawing.Imaging.EncoderParameters 1
$epFlush.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
    [System.Drawing.Imaging.Encoder]::SaveFlag, [long][System.Drawing.Imaging.EncoderValue]::Flush)

$tmp = [System.IO.Path]::GetTempFileName() + ".gif"
$first = [System.Drawing.Bitmap]::FromFile($files[0].FullName)
$first.Save($tmp, $gifCodec, $epMulti)
for ($i = 1; $i -lt $files.Count; $i++) {
    $b = [System.Drawing.Bitmap]::FromFile($files[$i].FullName)
    $first.SaveAdd($b, $epNext)
    $b.Dispose()
}
$first.SaveAdd($epFlush)
$first.Dispose()

# --- Patch in per-frame delays + a NETSCAPE loop block (GDI+ omits both) ---
$bytes = [System.Collections.Generic.List[byte]]::new()
$bytes.AddRange([System.IO.File]::ReadAllBytes($tmp))
Remove-Item $tmp -Force

# Insert the NETSCAPE2.0 loop extension right after the Logical Screen Descriptor + Global
# Colour Table (so it precedes the first frame).
$gctFlags = $bytes[10]
$gctSize = 0
if (($gctFlags -band 0x80) -ne 0) { $gctSize = 3 * [math]::Pow(2, ($gctFlags -band 0x07) + 1) }
$insertAt = 13 + [int]$gctSize
$loop = New-Object System.Collections.Generic.List[byte]
$loop.AddRange([byte[]]@(0x21, 0xFF, 0x0B))
$loop.AddRange([System.Text.Encoding]::ASCII.GetBytes("NETSCAPE2.0"))
$loop.AddRange([byte[]]@(0x03, 0x01, 0x00, 0x00, 0x00))
$bytes.InsertRange($insertAt, $loop)

# Patch every Graphic Control Extension's delay field (bytes 4-5 of each GCE) to $DelayCs.
for ($i = 0; $i -lt $bytes.Count - 8; $i++) {
    if ($bytes[$i] -eq 0x21 -and $bytes[$i + 1] -eq 0xF9 -and $bytes[$i + 2] -eq 0x04) {
        $bytes[$i + 4] = [byte]($DelayCs -band 0xFF)
        $bytes[$i + 5] = [byte](($DelayCs -shr 8) -band 0xFF)
    }
}

[System.IO.File]::WriteAllBytes($Out, $bytes.ToArray())
$sizeKb = [math]::Round((Get-Item $Out).Length / 1KB)
Write-Host "GIF written: $Out  ($($files.Count) frames, ${sizeKb}KB, delay ${DelayCs}cs)"
