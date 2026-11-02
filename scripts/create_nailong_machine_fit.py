from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "nailong_simple_r12.dxf"
OUTPUT = ROOT / "nailong_machine_fit.dxf"

# Matches the machine drawing area shown in the operator panel.
X_MIN, X_MAX = -120.0, 280.0
Y_MIN, Y_MAX = -48.0, 187.0


def parse_pairs(lines):
    if len(lines) % 2:
        raise ValueError("DXF contains an incomplete group-code/value pair")
    return list(zip(lines[0::2], lines[1::2]))


def format_value(value):
    return f"{value:.3f}".rstrip("0").rstrip(".")


def main():
    pairs = parse_pairs(SOURCE.read_text(encoding="ascii").splitlines())
    vertices = []
    entity = None
    for index, (code, value) in enumerate(pairs):
        if code == "0":
            entity = value
        elif entity == "VERTEX" and code in {"10", "20"}:
            vertices.append((index, code, float(value)))

    if not vertices:
        raise ValueError("No POLYLINE VERTEX coordinates found")

    old_x = [value for _, code, value in vertices if code == "10"]
    old_y = [value for _, code, value in vertices if code == "20"]
    old_width = max(old_x) - min(old_x)
    old_height = max(old_y) - min(old_y)

    # Keep a symmetric safety margin and preserve the original aspect ratio.
    margin = 8.0
    scale = min(
        (X_MAX - X_MIN - 2 * margin) / old_width,
        (Y_MAX - Y_MIN - 2 * margin) / old_height,
    )
    old_center_x = (min(old_x) + max(old_x)) / 2
    old_center_y = (min(old_y) + max(old_y)) / 2
    target_center_x = (X_MIN + X_MAX) / 2
    target_center_y = (Y_MIN + Y_MAX) / 2
    offset_x = target_center_x - scale * old_center_x
    offset_y = target_center_y - scale * old_center_y

    current_entity = None
    transformed = []
    for code, value in pairs:
        if code == "0":
            current_entity = value
        if current_entity == "VERTEX" and code == "10":
            value = format_value(float(value) * scale + offset_x)
        elif current_entity == "VERTEX" and code == "20":
            value = format_value(float(value) * scale + offset_y)
        transformed.append((code, value))

    output_lines = []
    for code, value in transformed:
        output_lines.extend((code, value))
    OUTPUT.write_text("\n".join(output_lines) + "\n", encoding="ascii", newline="\n")

    new_x = [float(value) for code, value in transformed if code == "10"]
    new_y = [float(value) for code, value in transformed if code == "20"]
    print(f"output={OUTPUT}")
    print(f"scale={scale:.6f} offset_x={offset_x:.6f} offset_y={offset_y:.6f}")
    print(f"bbox_x={min(new_x):.3f}..{max(new_x):.3f}")
    print(f"bbox_y={min(new_y):.3f}..{max(new_y):.3f}")


if __name__ == "__main__":
    main()
