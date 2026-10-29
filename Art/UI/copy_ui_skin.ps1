# Chai Dhaba Tycoon: copies the UI slices from Assets/UI/slicing into Resources/UISkin under short names the
# game code uses (UI/Skin.cs), and makes the few derived pieces the mockups need:
#   panel_blank   the recipe book frame with its title painted out (panels without a mockup)
#   bubble_body / bubble_tail   the order bubble split so it can stretch for 2-3 items
#   pip_empty     a grey copy of the yellow level pip
#   icon_*        item icons cropped from the perk and shop cards (their names and pips are drawn live)
# Run from anywhere:  powershell -File Art/UI/copy_ui_skin.ps1
Add-Type -AssemblyName System.Drawing
$src = 'D:\ChaiTycoon\Assets\UI\slicing'
$dst = 'D:\ChaiTycoon\Assets\ChaiTycoon\Resources\UISkin'
New-Item -ItemType Directory -Force $dst | Out-Null

$map = [ordered]@{
  'coin_bar' = 'main menu\coin bar.png'; 'leaf_bar' = 'main menu\leave bar.png'; 'star_bar' = 'main menu\star bar.png'
  'star_fill' = 'main menu\star fill.png'; 'btn_pause' = 'main menu\pause.png'; 'btn_settings' = 'main menu\setting.png'
  'level_bar' = 'main menu\levels bar 2.png'; 'level_bar_first' = 'main menu\levels bar.png'; 'level_banner' = 'main menu\levels titile.png'
  'hint_bar' = 'main menu\task bar.png'; 'bar_fill' = 'main menu\fill bar.png'; 'bar_back' = 'receipt book\unfill bar.png'
  'inv_bg' = 'main menu\staking\bg.png'; 'inv_slot' = 'main menu\staking\Rectangle 3.png'; 'inv_stack' = 'main menu\staking\stack.png'
  'inv_empty' = 'main menu\staking\empty hands.png'; 'inv_full' = 'main menu\staking\full hand.png'; 'cup_empty' = 'main menu\cup icon for empty hands.png'
  'ring_bg' = 'main menu\item bar.png'; 'ring_fill' = 'main menu\item fill bar.png'; 'tag_full' = 'main menu\full.png'
  'key_w' = 'main menu\w.png'; 'key_a' = 'main menu\a.png'; 'key_s' = 'main menu\s.png'; 'key_d' = 'main menu\d.png'
  'arrow' = 'main menu\arrow.png'; 'icon_chai' = 'main menu\cup.png'; 'icon_paratha' = 'main menu\paratha icon.png'
  'icon_samosa' = 'main menu\somosa icon.png'; 'icon_leaf' = 'main menu\leave.png'; 'icon_lane' = 'main menu\lane icon.png'
  'icon_rickshaw' = 'main menu\hotspot\rickshaw icon.png'; 'offer_bg' = 'main menu\watch ad\bg.png'; 'offer_2x' = 'main menu\watch ad\2x icon.png'
  'offer_coins' = 'main menu\watch ad\coins.png'; 'offer_clock' = 'main menu\watch ad\clock.png'; 'btn_watchad' = 'main menu\watch ad\watch ad.png'
  'tile_bg' = 'main menu\hotspot\bg.png'; 'tile_glow' = 'main menu\hotspot\selection.png'; 'coin_small' = 'main menu\hotspot\coin.png'
  'panel_settings' = 'setting\panel.png'; 'btn_close' = 'receipt book\close button  copy.png'; 'btn_reset' = 'setting\reset save.png'
  'icon_sound' = 'setting\sound icon.png'; 'icon_sound_off' = 'setting\sound off icon.png'; 'icon_music' = 'setting\music icon.png'
  'icon_music_off' = 'setting\music off icon.png'; 'panel_staff' = 'staff\staff panel.png'; 'staff_row' = 'staff\bg.png'
  'staff_portrait' = 'staff\character display bg.png'; 'staff_waiter1' = 'staff\waiter 1.png'; 'staff_waiter2' = 'staff\waiter 2.png'
  'label_speed' = 'staff\speed.png'; 'label_carry' = 'staff\carry.png'; 'btn_price' = 'staff\price button.png'; 'coin' = 'staff\coin.png'
  'btn_assign' = 'staff\assign.png'; 'panel_recipe' = 'receipt book\recipe book.png'; 'tab_perks' = 'receipt book\Perks.png'
  'tab_shop' = 'receipt book\shop.png'; 'card_perk' = 'receipt book\perks\bg.png'; 'card_shop' = 'receipt book\shop\bg.png'
  'pip_fill' = 'receipt book\perks\fill.png'; 'btn_grey' = 'receipt book\leves button.png'; 'btn_max' = 'receipt book\max.png'
  'icon_jar' = 'receipt book\jar icon.png'; 'icon_smile' = 'receipt book\smile icon.png'; 'icon_up_arrow' = 'receipt book\arrow.png'
}
foreach ($k in $map.Keys) { Copy-Item (Join-Path $src $map[$k]) (Join-Path $dst "$k.png") -Force }

# second round of UI (Assets/UI/UIv2): replaces the pieces above that it redraws, and adds new ones
$v2 = 'D:\ChaiTycoon\Assets\UI\UIv2'
$map2 = [ordered]@{
  'star_bar' = 'star bar.png'; 'star_fill' = 'star fill.png'; 'star_empty' = 'unfill star.png'
  'btn_staff' = 'staff.png'; 'btn_upgrade' = 'upgarde.png'; 'dot' = 'notification.png'; 'hand_point' = 'pointing hand.png'
  'icon_rickshaw' = 'rickshaw-icon.png'
  'cook_panel' = 'hire a cook\panel.png'; 'cook_icon_bg' = 'hire a cook\icon bg.png'; 'cook_face' = 'hire a cook\cook.png'
  'cook_price' = 'hire a cook\price button.png'
  'panel_staff' = 'staff\staff panel.png'; 'staff_row' = 'staff\bg.png'; 'staff_portrait' = 'staff\character display bg.png'
  'staff_waiter1' = 'staff\waiter 1.png'; 'staff_waiter2' = 'staff\waiter 2.png'; 'label_speed' = 'staff\speed.png'
  'label_carry' = 'staff\carry.png'; 'btn_price' = 'staff\price button.png'; 'coin' = 'staff\coin.png'
  'btn_assign' = 'staff\assign.png'; 'btn_close' = 'staff\close button .png'
  'panel_pause' = 'pause\panel.png'; 'btn_resume' = 'pause\resume.png'; 'btn_setting' = 'pause\setting.png'
}
foreach ($k in $map2.Keys) { Copy-Item (Join-Path $v2 $map2[$k]) (Join-Path $dst "$k.png") -Force }
# the two 512 px face icons, at 256 px (they show at most ~70 px on screen)
function Shrink($from, $to, $size) {
  $b = New-Object System.Drawing.Bitmap $from
  $o = New-Object System.Drawing.Bitmap $size, $size
  $g = [System.Drawing.Graphics]::FromImage($o); $g.InterpolationMode = 'HighQualityBicubic'; $g.Clear([System.Drawing.Color]::Transparent)
  $g.DrawImage($b, 0, 0, $size, $size); $g.Dispose(); $b.Dispose()
  $o.Save((Join-Path $dst $to), [System.Drawing.Imaging.ImageFormat]::Png); $o.Dispose()
}
Shrink (Join-Path $v2 'waiter-1-icon.png') 'face_girl.png' 256
Shrink (Join-Path $v2 'waiter-2-icon.png') 'face_man.png' 256
Shrink (Join-Path $v2 'rickshaw-icon.png') 'icon_rickshaw.png' 160

function Crop($from, $to, $x, $y, $w, $h) {
  $b = New-Object System.Drawing.Bitmap (Join-Path $src $from)
  $c = $b.Clone((New-Object System.Drawing.Rectangle $x, $y, $w, $h), $b.PixelFormat)
  $c.Save((Join-Path $dst $to), [System.Drawing.Imaging.ImageFormat]::Png); $c.Dispose(); $b.Dispose()
}
# order bubble: body above the tail, and the tail on its own
Crop 'main menu\items bg.png' 'bubble_body.png' 0 0 130 116
Crop 'main menu\items bg.png' 'bubble_tail.png' 44 108 42 25

# blank panel: the recipe book frame with the title filled with the cream beside it
$b = New-Object System.Drawing.Bitmap (Join-Path $src 'receipt book\recipe book.png')
for ($y = 62; $y -lt 240; $y++) {
  $c = $b.GetPixel(1000, $y)
  for ($x = 46; $x -lt 900; $x++) {
    if ($x -lt 112 -and $y -lt 122 -and (($x - 112) * ($x - 112) + ($y - 122) * ($y - 122)) -gt 62 * 62) { continue }   # rounded frame corner
    $p = $b.GetPixel($x, $y)
    $orange = $p.R -gt 200 -and $p.B -lt 150 -and $p.G -lt 210
    if (-not $orange -and ([math]::Abs($c.R - $p.R) + [math]::Abs($c.G - $p.G) + [math]::Abs($c.B - $p.B)) -gt 6) { $b.SetPixel($x, $y, $c) }
  }
}
$b.Save((Join-Path $dst 'panel_blank.png'), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()

# grey level pip
$b = New-Object System.Drawing.Bitmap (Join-Path $src 'receipt book\perks\fill.png')
for ($y = 0; $y -lt $b.Height; $y++) { for ($x = 0; $x -lt $b.Width; $x++) { $p = $b.GetPixel($x, $y); $l = [int](($p.R * 0.3 + $p.G * 0.59 + $p.B * 0.11) * 0.85); $b.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, $l, $l, [math]::Min(255, $l + 6))) } }
$b.Save((Join-Path $dst 'pip_empty.png'), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()

# item icons from the perk and shop cards (left part of each card header)
Crop 'receipt book\perks\faimily recipe.png' 'icon_family_recipe.png' 0 0 120 120
Crop 'receipt book\shop\Embroidered cap.png' 'icon_cap.png' 0 0 98 98
Crop 'receipt book\shop\Marigold kurta.png' 'icon_kurta.png' 0 0 103 103
Crop 'receipt book\shop\peacock apron.png' 'icon_apron.png' 0 0 108 108
Crop 'receipt book\shop\Marigold garlands.png' 'icon_garlands.png' 0 0 105 105
Crop 'receipt book\shop\Painted menu board.png' 'icon_menuboard.png' 0 0 107 107
Crop 'receipt book\shop\Fairy lights.png' 'icon_lights.png' 0 0 108 108
# unlock tile card: the mockup draws it as a skewed trapezoid (screen perspective). The game lays it flat on
# the ground, where the camera adds the perspective, so map each row back onto a square.
function Unwarp($name, $size) {
  $p = Join-Path $dst "$name.png"
  $b = New-Object System.Drawing.Bitmap $p
  $W = $b.Width; $H = $b.Height
  function Edges($y) { $l = -1; $r = -1; for ($x = 0; $x -lt $W; $x++) { if ($b.GetPixel($x, $y).A -gt 128) { if ($l -lt 0) { $l = $x }; $r = $x } }; return @($l, $r) }
  $y0 = 0; while ($y0 -lt $H) { $e = Edges $y0; if ($e[0] -ge 0) { break }; $y0++ }
  $y1 = $H - 1; while ($y1 -gt 0) { $e = Edges $y1; if ($e[0] -ge 0) { break }; $y1-- }
  $ya = [int]($y0 + ($y1 - $y0) * 0.25); $yb = [int]($y0 + ($y1 - $y0) * 0.75)
  $ea = Edges $ya; $eb = Edges $yb
  $ls = ($eb[0] - $ea[0]) / ($yb - $ya); $rs = ($eb[1] - $ea[1]) / ($yb - $ya)
  $o = New-Object System.Drawing.Bitmap $size, $size
  for ($v = 0; $v -lt $size; $v++) {
    $y = [math]::Min($H - 1, [int]($y0 + ($v + 0.5) * ($y1 - $y0 + 1) / $size))
    $l = $ea[0] + $ls * ($y - $ya); $r = $ea[1] + $rs * ($y - $ya)
    for ($u = 0; $u -lt $size; $u++) {
      $x = [int]($l + ($u + 0.5) * ($r - $l + 1) / $size)
      if ($x -ge 0 -and $x -lt $W) { $o.SetPixel($u, $v, $b.GetPixel($x, $y)) }
    }
  }
  $b.Dispose(); $o.Save($p, [System.Drawing.Imaging.ImageFormat]::Png); $o.Dispose()
}
# blank buttons and bars: the label area is painted with a clean column of the same button
function Blank($from, $to, $refX, $x0, $x1) {
  $b = New-Object System.Drawing.Bitmap (Join-Path $dst "$from.png")
  for ($y = 0; $y -lt $b.Height; $y++) { $c = $b.GetPixel($refX, $y); for ($x = $x0; $x -le $x1; $x++) { $b.SetPixel($x, $y, $c) } }
  $b.Save((Join-Path $dst "$to.png"), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}
Blank 'tab_perks' 'btn_yellow' 300 60 299
Blank 'tab_shop' 'btn_cyan' 300 60 299
Blank 'btn_reset' 'btn_red' 280 40 279
Blank 'inv_empty' 'inv_bar_grey' 30 31 346
Blank 'inv_full' 'inv_bar_green' 30 31 346

# grey progress track: the orange fill bar in grey (the "unfill bar" slice is four separate pips)
$b = New-Object System.Drawing.Bitmap (Join-Path $dst 'bar_fill.png')
for ($y = 0; $y -lt $b.Height; $y++) { for ($x = 0; $x -lt $b.Width; $x++) { $p = $b.GetPixel($x, $y); $l = [int](($p.R * 0.3 + $p.G * 0.59 + $p.B * 0.11) * 0.62 + 70); $l = [math]::Min(255, $l); $b.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, $l, $l - 4, $l - 8)) } }
$b.Save((Join-Path $dst 'bar_grey.png'), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()

# white patience ring so it can be tinted green / yellow / red
$b = New-Object System.Drawing.Bitmap (Join-Path $dst 'ring_fill.png')
for ($y = 0; $y -lt $b.Height; $y++) { for ($x = 0; $x -lt $b.Width; $x++) { $a = $b.GetPixel($x, $y).A; $b.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, 255, 255, 255)) } }
$b.Save((Join-Path $dst 'ring_white.png'), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()

Unwarp 'tile_bg' 220
Unwarp 'tile_glow' 236
# pad to multiples of 4 px (transparent on the right / bottom) so the textures can be block-compressed on WebGL
foreach ($f in Get-ChildItem $dst -Filter *.png) {
  $b = New-Object System.Drawing.Bitmap $f.FullName
  $w = [int]([math]::Ceiling($b.Width / 4) * 4); $h = [int]([math]::Ceiling($b.Height / 4) * 4)
  if ($w -eq $b.Width -and $h -eq $b.Height) { $b.Dispose(); continue }
  $o = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($o); $g.Clear([System.Drawing.Color]::Transparent)
  $g.DrawImage($b, 0, 0, $b.Width, $b.Height); $g.Dispose(); $b.Dispose()
  $o.Save($f.FullName, [System.Drawing.Imaging.ImageFormat]::Png); $o.Dispose()
}
"copied $((Get-ChildItem $dst -Filter *.png).Count) pieces"
