from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parent


def remove_exterior_background(image):
    image = image.convert("RGB")
    r, g, b = image.split()
    darkest = ImageChops.darker(ImageChops.darker(r, g), b)
    regions = darkest.point(lambda value: 0 if value > 235 else 255)
    # Remove the connected white exterior; keep enclosed white facial details.
    ImageDraw.floodfill(regions, (0, 0), 128, thresh=0)
    alpha = regions.point(lambda value: 0 if value == 128 else 255)
    result = image.convert("RGBA")
    result.putalpha(alpha)
    # Recover coverage at the outer edge instead of retaining the white matte.
    edge = ImageChops.subtract(alpha, alpha.filter(ImageFilter.MinFilter(3)))
    pixels = result.load()
    for index, is_edge in enumerate(edge.getdata()):
        if not is_edge:
            continue
        x, y = index % result.width, index // result.width
        color = pixels[x, y][:3]
        coverage = min(1.0, (255 - min(color)) / 223.0)
        if 0 < coverage < 0.995:
            recovered = tuple(max(0, min(255, round((c - 255 * (1 - coverage)) / coverage))) for c in color)
            pixels[x, y] = (*recovered, round(255 * coverage))
    return result


def crop_with_padding(image, padding=32):
    bounds = image.getbbox()
    if bounds is None:
        raise ValueError("No logo foreground found")
    content = image.crop(bounds)
    output = Image.new("RGBA", (content.width + 2 * padding, content.height + 2 * padding))
    output.alpha_composite(content, (padding, padding))
    return output


variants = (
    ("moon-gap", (85, 290, 485, 720)),
    ("night-cat", (400, 45, 1135, 651)),
)

for name, icon_bounds in variants:
    with Image.open(ROOT / f"{name}-source.png") as source:
        transparent = remove_exterior_background(source)
        lockup = crop_with_padding(transparent)
        lockup.save(ROOT / f"logo-{name}-transparent.png")

        icon = crop_with_padding(transparent.crop(icon_bounds), padding=20)
        icon.thumbnail((448, 448), Image.Resampling.LANCZOS)
        square = Image.new("RGBA", (512, 512))
        square.alpha_composite(icon, ((512 - icon.width) // 2, (512 - icon.height) // 2))
        square.save(ROOT / f"icon-{name}-transparent.png")

        preview = Image.new("RGBA", lockup.size, (240, 240, 240, 255))
        preview.alpha_composite(lockup)
        preview.convert("RGB").save(ROOT / f"preview-{name}-on-interface-gray.png")

        print(f"{name}: source={source.size}, lockup={lockup.size}, icon={square.size}, alpha={lockup.getchannel('A').getextrema()}")
