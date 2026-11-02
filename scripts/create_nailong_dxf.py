import math
from pathlib import Path


OUT = Path(__file__).resolve().parent.parent / "nailong_example.dxf"


def pair(code, value):
    return f"{code}\n{value}\n"


def lwpolyline(points, layer, color=None, closed=True):
    text = pair(0, "LWPOLYLINE") + pair(8, layer)
    text += pair(90, len(points)) + pair(70, 1 if closed else 0)
    if color is not None:
        text += pair(420, color)
    for x, y in points:
        text += pair(10, f"{x:.3f}") + pair(20, f"{y:.3f}")
    return text


def circle(x, y, radius, layer, color=None):
    text = pair(0, "CIRCLE") + pair(8, layer)
    if color is not None:
        text += pair(420, color)
    return text + pair(10, f"{x:.3f}") + pair(20, f"{y:.3f}") + pair(30, 0) + pair(40, f"{radius:.3f}")


def hatch(points, layer, color):
    # Solid hatch with a polyline boundary.
    text = pair(0, "HATCH") + pair(8, layer) + pair(420, color) + pair(2, "SOLID")
    text += pair(70, 1) + pair(71, 0) + pair(91, 1)
    text += pair(92, 2) + pair(72, 0) + pair(73, 1) + pair(93, len(points))
    for x, y in points:
        text += pair(10, f"{x:.3f}") + pair(20, f"{y:.3f}")
    text += pair(97, 0)
    text += pair(75, 0) + pair(76, 1) + pair(98, 1) + pair(10, 0) + pair(20, 0)
    return text


def sample_body():
    points = []
    for i in range(96):
        t = 2 * math.pi * i / 96
        s = math.sin(t)
        c = math.cos(t)
        # A soft, slightly wider lower half gives the rounded mascot silhouette.
        width = 104 + 10 * max(0, -s) + 4 * max(0, s)
        x = width * c
        y = 150 + 150 * s
        points.append((x, y))
    return points


def rounded_limb(cx, cy, rx, ry, start_angle=0, count=32):
    return [
        (cx + rx * math.cos(start_angle + 2 * math.pi * i / count),
         cy + ry * math.sin(start_angle + 2 * math.pi * i / count))
        for i in range(count)
    ]


def main():
    body = sample_body()
    left_arm = [
        (-96, 205), (-118, 195), (-133, 173), (-137, 143),
        (-132, 119), (-119, 104), (-107, 111), (-101, 131),
        (-102, 155), (-94, 177),
    ]
    right_arm = [(-x, y) for x, y in reversed(left_arm)]
    left_leg = rounded_limb(-52, 2, 31, 48, count=28)
    right_leg = rounded_limb(52, 2, 31, 48, count=28)

    header = ""
    header += pair(0, "SECTION") + pair(2, "HEADER")
    header += pair(9, "$ACADVER") + pair(1, "AC1015")
    header += pair(9, "$INSUNITS") + pair(70, 4)
    header += pair(0, "ENDSEC")

    layers = [
        ("0", 7), ("BODY_FILL", 2), ("LIMB_FILL", 2),
        ("OUTLINE", 7), ("EYES", 7), ("PUPILS", 7),
        ("MOUTH", 1), ("HIGHLIGHT", 7),
    ]
    tables = pair(0, "SECTION") + pair(2, "TABLES")
    tables += pair(0, "TABLE") + pair(2, "LAYER") + pair(70, len(layers))
    for name, color in layers:
        tables += pair(0, "LAYER") + pair(2, name) + pair(70, 0) + pair(62, color) + pair(6, "CONTINUOUS")
    tables += pair(0, "ENDTAB") + pair(0, "ENDSEC")

    entities = pair(0, "SECTION") + pair(2, "ENTITIES")
    entities += hatch(body, "BODY_FILL", 0xFFF06A)
    entities += lwpolyline(body, "OUTLINE", color=0x5A3A00)
    for limb in (left_arm, right_arm):
        entities += hatch(limb, "LIMB_FILL", 0xFFF06A)
        entities += lwpolyline(limb, "OUTLINE", color=0x5A3A00)
    for leg in (left_leg, right_leg):
        entities += hatch(leg, "LIMB_FILL", 0xF6C842)
        entities += lwpolyline(leg, "OUTLINE", color=0x5A3A00)

    for x in (-46, 46):
        entities += circle(x, 235, 27, "EYES", color=0xB9B84A)
        entities += circle(x, 235, 17, "PUPILS", color=0x101010)
        entities += circle(x + 6, 243, 4, "HIGHLIGHT", color=0xFFFFFF)

    mouth = []
    for i in range(25):
        t = i / 24
        x = -31 + 62 * t
        y = 193 - 7 * math.sin(math.pi * t)
        mouth.append((x, y))
    entities += lwpolyline(mouth, "MOUTH", color=0x5A2600, closed=False)

    # A small baseline keeps the drawing easy to locate after zoom-to-extents.
    entities += lwpolyline([(-155, -51), (155, -51)], "OUTLINE", color=0xB0B0B0, closed=False)
    entities += pair(0, "ENDSEC") + pair(0, "EOF")

    OUT.write_text(header + tables + entities, encoding="ascii", newline="\n")
    print(OUT)


if __name__ == "__main__":
    main()
