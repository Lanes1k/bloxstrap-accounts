Add-Type -AssemblyName System.Drawing

foreach ($theme in @('dark', 'light')) {
    $source = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot "../Images/Bloxstrap-full-$theme.png"))
    $bitmap = New-Object System.Drawing.Bitmap($source.Width, ($source.Height + 130))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($source, 0, 0, $source.Width, $source.Height)
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $color = if ($theme -eq 'dark') { [System.Drawing.Color]::White } else { [System.Drawing.Color]::Black }
    $brush = New-Object System.Drawing.SolidBrush($color)
    $font = New-Object System.Drawing.Font('Segoe UI', 100, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $rectangle = New-Object System.Drawing.RectangleF(450, ($source.Height - 55), ($source.Width - 450), 140)
    $graphics.DrawString('accounts', $font, $brush, $rectangle, $format)
    $bitmap.Save((Join-Path $PSScriptRoot "../Images/Bloxstrap-accounts-$theme.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $format.Dispose()
    $font.Dispose()
    $brush.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    $source.Dispose()
}
