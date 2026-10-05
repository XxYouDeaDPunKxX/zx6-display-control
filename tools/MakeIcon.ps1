$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$asset=Join-Path (Split-Path $PSScriptRoot -Parent) 'src\Assets\holder.ico'
New-Item -ItemType Directory -Path (Split-Path $asset -Parent) -Force | Out-Null
$images=@()
foreach($size in @(16,32,48,64,256)) {
 $bitmap=New-Object Drawing.Bitmap($size,$size)
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 $graphics.ScaleTransform($size/32.0,$size/32.0)
 $graphics.Clear([Drawing.Color]::Transparent)
 $background=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(27,67,93))
 $accent=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(74,205,196))
 $graphics.FillRectangle($background,1,4,30,24)
 foreach($column in 0..2) {
  $height=5+$column*4
  $graphics.FillRectangle([Drawing.Brushes]::White,5+$column*4,24-$height,3,$height)
  $graphics.FillRectangle($accent,18+$column*4,24-(17-$column*4),3,17-$column*4)
 }
 $stream=New-Object IO.MemoryStream
 $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
 $images+=,[byte[]]$stream.ToArray()
 $stream.Dispose();$graphics.Dispose();$bitmap.Dispose();$background.Dispose();$accent.Dispose()
}
$file=[IO.File]::Create($asset)
$writer=New-Object IO.BinaryWriter($file)
try {
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$images.Count)
 $offset=6+16*$images.Count;$sizes=@(16,32,48,64,256)
 for($i=0;$i -lt $images.Count;$i++) {
  $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
  $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
  $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$images[$i].Length);$writer.Write([uint32]$offset)
  $offset+=$images[$i].Length
 }
 foreach($bytes in $images){$writer.Write([byte[]]$bytes)}
} finally {$writer.Dispose();$file.Dispose()}
