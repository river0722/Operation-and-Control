"""Create a simplified Golden Arches silhouette as one closed R12 polyline."""
from pathlib import Path
import math

import ezdxf
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "mcdonalds_logo_machine_fit.dxf"


def cubic(start, a, b, end, steps=40):
    points = []
    for i in range(1, steps + 1):
        t = i / steps
        s = 1 - t
        points.append(tuple(s**3 * start[j] + 3*s*s*t*a[j]
                            + 3*s*t*t*b[j] + t**3*end[j] for j in (0, 1)))
    return points


def main():
    points = [(-108.0, 0.0)]

    def curve(a, b, end):
        points.extend(cubic(points[-1], a, b, end))

    # Outer edges of two tall arches; a single joined silhouette.
    curve((-106, 91), (-90, 160), (-54, 160))
    curve((-23, 160), (-6, 105), (0, 38))
    curve((6, 105), (23, 160), (54, 160))
    curve((90, 160), (106, 91), (108, 0))
    points.append((80, 0))
    # Inner returns make the arches slim at their crowns and wide at the feet.
    curve((78, 78), (71, 148), (54, 148))
    curve((37, 148), (21, 58), (17, 0))
    points.append((-17, 0))
    curve((-21, 58), (-37, 148), (-54, 148))
    curve((-71, 148), (-78, 78), (-80, 0))

    # 270 x 200 mm, centered in X=-120..280 and Y=-48..187.
    points = [(round(x * 1.25 + 80, 3), round(y * 1.25 - 30.5, 3))
              for x, y in points]
    doc = ezdxf.new("R12")
    doc.modelspace().add_polyline2d(points, close=True)
    doc.header["$EXTMIN"] = (-55, -30.5, 0)
    doc.header["$EXTMAX"] = (215, 169.5, 0)
    doc.saveas(OUTPUT)
    text = OUTPUT.read_text(encoding="cp1252")
    # The local host accepts this millimetre declaration with R12 geometry.
    text = text.replace("  2\nHEADER\n",
                        "  2\nHEADER\n  9\n$INSUNITS\n 70\n4\n", 1)
    OUTPUT.write_text(text, encoding="ascii", newline="\n")

    check = ezdxf.readfile(OUTPUT)
    audit = check.audit()
    assert not audit.has_errors, audit.errors
    entities = list(check.modelspace())
    assert len(entities) == 1 and entities[0].dxftype() == "POLYLINE"
    assert entities[0].is_closed
    vertices = [tuple(vertex.dxf.location) for vertex in entities[0].vertices]
    assert all(math.isfinite(c) for vertex in vertices for c in vertex)
    assert all(-120 <= x <= 280 and -48 <= y <= 187 and z == 0
               for x, y, z in vertices)
    assert all(math.dist(a, b) > 0 for a, b in zip(vertices, vertices[1:]))

    image = Image.new("RGB", (1440, 700), "white")
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 25)
    small = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
    for center_x, filled in ((365, True), (1080, False)):
        screen = [(center_x + (x - 80) * 2.15, 560 - (y + 30.5) * 2.15)
                  for x, y in points]
        if filled:
            draw.polygon(screen, fill="#FFC72C")
        else:
            draw.line(screen + [screen[0]], fill="#20262D", width=3)
        draw.text((center_x - 160, 44),
                  "GOLDEN ARCHES" if filled else "DXF CLOSED OUTLINE",
                  font=font, fill="#303840")
    draw.line((720, 30, 720, 625), fill="#E4E7EB", width=2)
    draw.text((58, 638), "Simplified example | 270 x 200 mm | One closed polyline | R12 ASCII",
              font=small, fill="#586574")
    image.save(ROOT / "mcdonalds_logo_preview.png")
    print(f"DXF: {OUTPUT.name}; {len(vertices)} vertices; audit passed")
    print("Bounds: X=-55..215 mm; Y=-30.5..169.5 mm")


if __name__ == "__main__":
    main()
