"""Find small-radius ARC entities near BEND_UP/BEND_DOWN line endpoints - candidates for
the unwanted rounded-cap artifact the user flagged (EK2 screenshot)."""
import sys


def parse_pairs(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = [l.rstrip("\n").rstrip("\r") for l in f]
    return [(lines[i].strip(), lines[i + 1].strip()) for i in range(0, len(lines) - 1, 2)]


def main():
    path = sys.argv[1]
    pairs = parse_pairs(path)

    in_ent = False
    cur_type = None
    cur_layer = None
    fields = {}
    bend_endpoints = []
    arcs = []

    def flush():
        if cur_type == "LINE" and cur_layer in ("BEND_UP", "BEND_DOWN"):
            if all(k in fields for k in ("10", "20", "11", "21")):
                bend_endpoints.append((float(fields["10"]), float(fields["20"])))
                bend_endpoints.append((float(fields["11"]), float(fields["21"])))
        elif cur_type == "ARC":
            if all(k in fields for k in ("10", "20", "40")):
                arcs.append((cur_layer, float(fields["10"]), float(fields["20"]), float(fields["40"])))

    for code, val in pairs:
        if code == "2" and val == "ENTITIES":
            in_ent = True
            continue
        if code == "0" and val == "ENDSEC":
            in_ent = False
        if not in_ent:
            continue
        if code == "0":
            flush()
            cur_type = val
            cur_layer = None
            fields = {}
            continue
        if code == "8":
            cur_layer = val
        elif code in ("10", "20", "11", "21", "40"):
            fields[code] = val
    flush()

    print(f"BEND_UP/BEND_DOWN endpoint count: {len(bend_endpoints)}")
    print(f"ARC count: {len(arcs)}")
    print()

    for layer, cx, cy, r in arcs:
        for ex, ey in bend_endpoints:
            dist = ((cx - ex) ** 2 + (cy - ey) ** 2) ** 0.5
            if dist < max(r * 3, 5.0):
                print(f"ARC layer={layer} center=({cx:.2f},{cy:.2f}) r={r:.3f} -- near bend endpoint ({ex:.2f},{ey:.2f}) dist={dist:.2f}")


if __name__ == "__main__":
    main()
