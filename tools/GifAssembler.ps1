# Assembles numbered PNG frames (frame_0000.png …) into a looping animated GIF.
#
# Why a script and not the app: the app emits clean PNG frames (Skia), and we stitch them here.
# Windows has no ffmpeg/ImageMagick by default, so this drives the GDI+ (System.Drawing) native
# GIF encoder via Save + SaveAdd (the documented multi-frame path). The per-frame delay and the
# loop-forever flag are set through GDI+ PropertyItems (0x5100 FrameDelay, 0x5101 LoopCount) on
# the FIRST bitmap before encoding — the supported way, so GDI+ writes correct Graphic-Control
# blocks itself. (An earlier version byte-patched the delays by scanning for 0x21F904, which
# also matched that sequence INSIDE a frame's LZW data and corrupted a frame mid-GIF — that made
# the GIF stop partway and not loop. PropertyItems avoid touching the byte stream entirely.)
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

# A PropertyItem can't be `new`'d directly; clone one off any existing image, then overwrite it.
function New-PropItem([int]$id, [int]$type, [byte[]]$value) {
    $pi = $template.PropertyItems[0]
    $pi.Id = $id
    $pi.Type = $type      # 3 = SHORT (16-bit), 4 = LONG (32-bit)
    $pi.Len = $value.Length
    $pi.Value = $value
    return $pi
}

$first = [System.Drawing.Bitmap]::FromFile($files[0].FullName)
$template = $first  # PropertyItems[0] exists on a loaded PNG; we reuse its shape

# 0x5100 FrameDelay: one 32-bit LE value per frame, in centiseconds.
$delayBytes = New-Object System.Collections.Generic.List[byte]
for ($f = 0; $f -lt $files.Count; $f++) {
    $delayBytes.Add([byte]($DelayCs -band 0xFF))
    $delayBytes.Add([byte](($DelayCs -shr 8) -band 0xFF))
    $delayBytes.Add([byte](($DelayCs -shr 16) -band 0xFF))
    $delayBytes.Add([byte](($DelayCs -shr 24) -band 0xFF))
}
$first.SetPropertyItem((New-PropItem 0x5100 4 $delayBytes.ToArray()))

# 0x5101 LoopCount: 16-bit, 0 = loop forever.
$first.SetPropertyItem((New-PropItem 0x5101 3 ([byte[]]@(0x00, 0x00))))

$first.Save($Out, $gifCodec, $epMulti)
for ($i = 1; $i -lt $files.Count; $i++) {
    $b = [System.Drawing.Bitmap]::FromFile($files[$i].FullName)
    $first.SaveAdd($b, $epNext)
    $b.Dispose()
}
$first.SaveAdd($epFlush)
$first.Dispose()

$sizeKb = [math]::Round((Get-Item $Out).Length / 1KB)
Write-Host "GIF written: $Out  ($($files.Count) frames, ${sizeKb}KB, delay ${DelayCs}cs, loop forever)"
