from pathlib import Path
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent

for name in ("moon-gap", "night-cat"):
    with Image.open(ROOT / f"logo-{name}-transparent.png") as source:
        logo = source.convert("RGBA")

    draw = ImageDraw.Draw(logo)
    if name == "moon-gap":
        draw.rectangle((380, 270, logo.width - 1, logo.height - 1), fill=(0, 0, 0, 0))
        # Center the unchanged team lettering beside the symbol after removing its subtitle.
        lettering = logo.crop((400, 75, logo.width, 250))
        draw.rectangle((400, 75, logo.width - 1, 249), fill=(0, 0, 0, 0))
        logo.alpha_composite(lettering, (400, 119))
    else:
        draw.rectangle((0, 800, logo.width - 1, logo.height - 1), fill=(0, 0, 0, 0))

    content = logo.crop(logo.getbbox())
    final = Image.new("RGBA", (content.width + 64, content.height + 64))
    final.alpha_composite(content, (32, 32))
    final.save(ROOT / f"logo-{name}-no-school-transparent.png")

    preview = Image.new("RGBA", final.size, (240, 240, 240, 255))
    preview.alpha_composite(final)
    preview.convert("RGB").save(ROOT / f"preview-{name}-no-school.png")
    print(f"{name}: {final.size}, alpha={final.getchannel('A').getextrema()}")
