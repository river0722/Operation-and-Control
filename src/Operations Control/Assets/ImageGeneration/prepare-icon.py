"""Prepare Windows icon assets from the preserved Image2 output (requires Pillow)."""
from pathlib import Path
from collections import deque
from PIL import Image, ImageFilter

directory = Path(__file__).resolve().parent
assets = directory.parent
source = Image.open(directory / "pmac-icon-source.png").convert("RGB")
width, height = source.size
pixels = source.load()
exterior = Image.new("L", source.size)
mask = exterior.load()
queue = deque([(0, 0)])
mask[0, 0] = 255
while queue:
    x, y = queue.popleft()
    for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if 0 <= nx < width and 0 <= ny < height and mask[nx, ny] == 0:
            if min(pixels[nx, ny]) >= 235:
                mask[nx, ny] = 255
                queue.append((nx, ny))

# Remove only the connected exterior; the white motion symbol stays opaque.
image = source.convert("RGBA")
rgba = image.load()
boundary = exterior.filter(ImageFilter.MaxFilter(3)).load()
blue = (31, 76, 200)
for y in range(height):
    for x in range(width):
        if mask[x, y]:
            rgba[x, y] = (0, 0, 0, 0)
        elif boundary[x, y]:
            red, green, cyan = pixels[x, y]
            alpha = min(1.0, max((255 - channel) / (255 - base)
                                 for channel, base in zip((red, green, cyan), blue)))
            recovered = tuple(max(0, min(255, round((channel - 255 * (1 - alpha)) / alpha)))
                              for channel in (red, green, cyan))
            rgba[x, y] = recovered + (round(255 * alpha),)

bounds = image.getbbox()
cropped = image.crop(bounds)
side = max(cropped.size)
padding = round(side * 0.025)
canvas = Image.new("RGBA", (side + 2 * padding, side + 2 * padding))
canvas.paste(cropped, (padding + (side - cropped.width) // 2,
                      padding + (side - cropped.height) // 2))
icon = canvas.resize((1024, 1024), Image.Resampling.LANCZOS)
icon.save(assets / "pmac-console.png")
sizes = [16, 24, 32, 48, 64, 128, 256]
icon.save(assets / "pmac-console.ico", format="ICO", bitmap_format="bmp",
          sizes=[(size, size) for size in sizes])
icon.resize((256, 256), Image.Resampling.LANCZOS).save(directory / "pmac-icon-preview.png")
print("Source: {}x{}; final PNG: 1024x1024; ICO sizes: {}".format(width, height, sizes))
