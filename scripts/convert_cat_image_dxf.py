"""Trace the supplied cat drawing into machine-fitted ASCII DXF polylines.

Run with uv run --with numpy --with opencv-contrib-python-headless
--with ezdxf --with pillow python scripts/convert_cat_image_dxf.py IMAGE
"""
import json
import math
import sys
from pathlib import Path

import cv2
import ezdxf
import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent.parent
AREA = (-120.0, -48.0, 280.0, 187.0)


def graph_from_skeleton(skeleton):
    pixels = set(zip(*np.nonzero(skeleton)))
    graph = {}
    for y, x in pixels:
        neighbors = set()
        for dy, dx in ((-1, -1), (-1, 0), (-1, 1), (0, -1),
                       (0, 1), (1, -1), (1, 0), (1, 1)):
            other = (y + dy, x + dx)
            if other not in pixels:
                continue
            if dy and dx and ((y + dy, x) in pixels or (y, x + dx) in pixels):
                continue
            neighbors.add(other)
        graph[(y, x)] = neighbors
    return graph


def prune_short_spurs(graph):
    for _ in range(5):
        remove = set()
        for start in list(graph):
            if len(graph[start]) != 1:
                continue
            chain = [start]
            previous = None
            current = start
            distance = 0.0
            while True:
                choices = graph[current] - {previous}
                if not choices:
                    break
                following = next(iter(choices))
                distance += math.dist(current, following)
                previous, current = current, following
                if len(graph[current]) != 2:
                    break
                chain.append(current)
            if len(graph[current]) >= 3 and distance < 24:
                remove.update(chain)
        if not remove:
            break
        for point in remove:
            for neighbor in graph[point]:
                graph[neighbor].discard(point)
        for point in remove:
            del graph[point]


def centerline_paths(mask):
    skeleton = cv2.ximgproc.thinning(mask, thinningType=cv2.ximgproc.THINNING_GUOHALL)
    graph = graph_from_skeleton(skeleton)
    prune_short_spurs(graph)
    visited = set()
    paths = []
    starts = sorted(graph, key=lambda point: (len(graph[point]) == 2, point))
    for start in starts:
        for neighbor in sorted(graph[start]):
            edge = frozenset((start, neighbor))
            if edge in visited:
                continue
            chain = [start, neighbor]
            visited.add(edge)
            previous, current = start, neighbor
            while len(graph[current]) == 2 and current != start:
                following = next(iter(graph[current] - {previous}))
                edge = frozenset((current, following))
                if edge in visited:
                    break
                visited.add(edge)
                chain.append(following)
                previous, current = current, following
            if len(chain) < 3:
                continue
            closed = chain[-1] == chain[0]
            # Smooth raster stair steps before reducing the vertex count.
            points = np.array([(x, y) for y, x in chain], dtype=np.float32)
            if len(points) > 9 and not closed:
                padded = np.pad(points, ((3, 3), (0, 0)), mode="edge")
                smoothed = np.array([padded[i:i + 7].mean(axis=0)
                                     for i in range(len(points))])
                smoothed[0], smoothed[-1] = points[0], points[-1]
                points = smoothed
            simple = cv2.approxPolyDP(points, 1.0, closed).reshape(-1, 2)
            if len(simple) >= (3 if closed else 2):
                paths.append((simple, closed))
    return paths


def write_dxf(paths, destination):
    all_points = np.concatenate([points for points, _ in paths])
    lo, hi = all_points.min(axis=0), all_points.max(axis=0)
    scale = min(380 / (hi[0] - lo[0]), 215 / (hi[1] - lo[1]))
    source_center = (lo + hi) / 2
    target_center = np.array([80.0, 69.5])
    doc = ezdxf.new("R12")
    converted = []
    for points, closed in paths:
        points = (points.astype(np.float64) - source_center) * scale
        points[:, 1] *= -1  # Image Y points down; CAD Y points up.
        points += target_center
        points = np.round(points, 3)
        converted.append((points, closed))
        doc.modelspace().add_polyline2d(points.tolist(), close=closed)
    doc.saveas(destination)
    # R12 has no standardized unit declaration. This extension is understood
    # by the local host reader and makes its millimetre conversion explicit.
    text = destination.read_text(encoding="cp1252")
    marker = "  2\nHEADER\n"
    if marker not in text:
        raise ValueError("DXF HEADER marker missing")
    text = text.replace(marker, marker + "  9\n$INSUNITS\n 70\n4\n", 1)
    destination.write_text(text, encoding="ascii", newline="\n")
    check = ezdxf.readfile(destination)
    audit = check.audit()
    if audit.has_errors:
        raise ValueError(str(audit.errors))
    vertices = np.array([(v.dxf.location.x, v.dxf.location.y)
                         for poly in check.modelspace() for v in poly.vertices])
    lower, upper = vertices.min(axis=0), vertices.max(axis=0)
    if not (lower[0] >= AREA[0] and lower[1] >= AREA[1]
            and upper[0] <= AREA[2] and upper[1] <= AREA[3]):
        raise ValueError("Drawing exceeds machine area")
    return converted, {"file": destination.name, "strokes": len(converted),
                       "vertices": len(vertices), "xmin": float(lower[0]),
                       "xmax": float(upper[0]), "ymin": float(lower[1]),
                       "ymax": float(upper[1]), "units": "mm"}


def preview(paths, destination):
    image = Image.new("RGB", (1440, 960), "white")
    draw = ImageDraw.Draw(image)
    factor = 3.2
    def pixel(point):
        x, y = point
        return (80 + (x - AREA[0]) * factor,
                100 + (AREA[3] - y) * factor)
    draw.rectangle([pixel((AREA[0], AREA[3])), pixel((AREA[2], AREA[1]))],
                   outline="#8092a2", width=2)
    draw.text((80, 48), "DXF CENTERLINE | X: -120..280 mm | Y: -48..187 mm", fill="#334155")
    for points, closed in paths:
        points = list(points)
        if closed:
            points.append(points[0])
        draw.line([pixel(point) for point in points], fill="#161616", width=3)
    draw.text((80, 885), "Polyline geometry / preserved aspect ratio / margin >= 10 mm", fill="#334155")
    image.save(destination)


def main():
    source = Path(sys.argv[1])
    gray = cv2.imdecode(np.frombuffer(source.read_bytes(), np.uint8), cv2.IMREAD_GRAYSCALE)
    if gray is None:
        raise ValueError("Cannot read source image")
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    mask = np.where(blurred < 95, 255, 0).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask)
    keep = [i for i in range(1, count) if stats[i, cv2.CC_STAT_AREA] >= 150]
    mask = np.where(np.isin(labels, keep), 255, 0).astype(np.uint8)
    centers = centerline_paths(mask)
    contours, _ = cv2.findContours(mask, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    outlines = [(cv2.approxPolyDP(contour, 1.2, True).reshape(-1, 2).astype(float), True)
                for contour in contours if cv2.contourArea(contour) >= 100]
    converted, center_report = write_dxf(centers, ROOT / "cat_centerline_machine_fit.dxf")
    _, outline_report = write_dxf(outlines, ROOT / "cat_outline_machine_fit.dxf")
    preview(converted, ROOT / "cat_dxf_preview.png")
    (ROOT / "cat_dxf_report.json").write_text(
        json.dumps([center_report, outline_report], indent=2), encoding="utf-8")
    print(json.dumps([center_report, outline_report], indent=2))


if __name__ == "__main__":
    main()
