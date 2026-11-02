import math
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
OUTPUTS = [ROOT / "nailong_example.dxf", ROOT / "nailong_simple_r12.dxf"]


def pair(code, value):
    return f"{code}\n{value}\n"


def polyline(points, closed=True):
    """Write an old-style POLYLINE made only from LINEAR VERTEX records."""
    text = pair(0, "POLYLINE")
    text += pair(8, "0") + pair(66, 1) + pair(70, 1 if closed else 0)
    text += pair(10, 0) + pair(20, 0) + pair(30, 0)
    for x, y in points:
        text += pair(0, "VERTEX") + pair(8, "0")
        text += pair(10, f"{x:.2f}") + pair(20, f"{y:.2f}") + pair(30, 0)
    text += pair(0, "SEQEND") + pair(8, "0")
    return text


def ellipse_polygon(cx, cy, rx, ry, count=16):
    return [
        (cx + rx * math.cos(2 * math.pi * i / count),
         cy + ry * math.sin(2 * math.pi * i / count))
        for i in range(count)
    ]


def body_polygon():
    # Clockwise, low point count, with broad shoulders and a round belly.
    return [
        (-82, 286), (-55, 309), (-20, 320), (20, 320), (55, 309), (82, 286),
        (98, 250), (100, 208), (112, 177), (130, 151), (139, 126),
        (137, 101), (125, 82), (111, 70), (108, 35), (102, 8),
        (88, -17), (62, -32), (30, -40), (0, -43), (-30, -40),
        (-62, -32), (-88, -17), (-102, 8), (-108, 35), (-111, 70),
        (-125, 82), (-137, 101), (-139, 126), (-130, 151), (-112, 177),
        (-100, 208), (-98, 250),
    ]


def limb_polygon(cx, cy, rx, ry):
    return ellipse_polygon(cx, cy, rx, ry, 12)


def main():
    body = body_polygon()
    left_arm = [
        (-97, 205), (-118, 192), (-129, 169), (-131, 142),
        (-126, 118), (-114, 104), (-104, 112), (-101, 137),
        (-105, 163), (-96, 181),
    ]
    right_arm = [(-x, y) for x, y in reversed(left_arm)]
    left_leg = limb_polygon(-50, -27, 30, 48)
    right_leg = limb_polygon(50, -27, 30, 48)

    # Eye rings, pupils, highlights are polygons so no CIRCLE entities are needed.
    left_eye = ellipse_polygon(-45, 232, 27, 32, 16)
    right_eye = ellipse_polygon(45, 232, 27, 32, 16)
    left_pupil = ellipse_polygon(-45, 232, 17, 22, 14)
    right_pupil = ellipse_polygon(45, 232, 17, 22, 14)
    left_highlight = ellipse_polygon(-39, 243, 4, 5, 8)
    right_highlight = ellipse_polygon(51, 243, 4, 5, 8)

    mouth = []
    for i in range(13):
        t = i / 12
        mouth.append((-31 + 62 * t, 193 - 7 * math.sin(math.pi * t)))

    header = (
        pair(0, "SECTION") + pair(2, "HEADER") +
        pair(9, "$ACADVER") + pair(1, "AC1009") +
        pair(9, "$INSUNITS") + pair(70, 4) +
        pair(0, "ENDSEC")
    )
    entities = pair(0, "SECTION") + pair(2, "ENTITIES")
    for shape in (
        body, left_arm, right_arm, left_leg, right_leg,
        left_eye, right_eye, left_pupil, right_pupil,
        left_highlight, right_highlight,
    ):
        entities += polyline(shape, closed=True)
    entities += polyline(mouth, closed=False)
    entities += pair(0, "ENDSEC") + pair(0, "EOF")

    data = header + entities
    for output in OUTPUTS:
        output.write_text(data, encoding="ascii", newline="\n")
        print(output)


if __name__ == "__main__":
    main()
