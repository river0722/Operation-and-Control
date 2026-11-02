from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parent
target = ROOT.parents[2] / "online" / "Operations Control" / "Assets" / "team-logo-background.png"
with Image.open(ROOT / "logo-moon-gap-no-school-transparent.png") as source:
    logo = source.convert("RGBA")
    logo = logo.crop(logo.getbbox())
    logo.putalpha(logo.getchannel("A").point(lambda alpha: round(alpha * 0.65)))
    logo.save(target)
    print(f"{target}: {logo.size}, alpha={logo.getchannel('A').getextrema()}")
