"""
Uretilen DXF'te parca etiketinin (MTEXT) herhangi bir CUT/BEND cizgisiyle kesisip
kesismedigini kontrol eder. Gorsel DWG TrueView dogrulamasi klavye/komut satiri
guvenilirligi dusuk oldugu icin, bunun yerine DXF'i dogrudan matematiksel olarak
analiz ediyoruz.
"""
import sys
import re

def parse_dxf_pairs(path):
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        lines = [l.rstrip("\n").rstrip("\r") for l in f]
    pairs = []
    i = 0
    while i + 1 < len(lines):
        pairs.append((lines[i].strip(), lines[i+1].strip()))
        i += 2
    return pairs

def extract_entities_section(pairs):
    out = []
    in_entities = False
    for code, val in pairs:
        if code == "2" and val == "ENTITIES":
            in_entities = True
            continue
        if code == "0" and val == "ENDSEC":
            in_entities = False
        if in_entities:
            out.append((code, val))
    return out

def collect_segments(entity_pairs, target_layers):
    """CUT/BEND layer'larindaki LINE entity'lerinden (x1,y1,x2,y2) segment listesi."""
    segments = []
    cur_type = None
    cur_layer = None
    x1 = y1 = x2 = y2 = None
    for code, val in entity_pairs:
        if code == "0":
            if cur_type == "LINE" and cur_layer in target_layers and None not in (x1, y1, x2, y2):
                segments.append((x1, y1, x2, y2, cur_layer))
            cur_type = val
            cur_layer = None
            x1 = y1 = x2 = y2 = None
            continue
        if code == "8":
            cur_layer = val
        elif code == "10":
            x1 = float(val)
        elif code == "20":
            y1 = float(val)
        elif code == "11":
            x2 = float(val)
        elif code == "21":
            y2 = float(val)
    if cur_type == "LINE" and cur_layer in target_layers and None not in (x1, y1, x2, y2):
        segments.append((x1, y1, x2, y2, cur_layer))
    return segments

def find_mtext(entity_pairs):
    """MTEXT konumu (10,20), yukseklik (40) ve icerik (1)."""
    cur_type = None
    x = y = height = None
    content = None
    results = []
    for code, val in entity_pairs:
        if code == "0":
            if cur_type == "MTEXT" and x is not None:
                results.append((x, y, height or 1.0, content or ""))
            cur_type = val
            x = y = height = None
            content = None
            continue
        if cur_type == "MTEXT":
            if code == "10":
                x = float(val)
            elif code == "20":
                y = float(val)
            elif code == "40":
                height = float(val)
            elif code == "1":
                content = val
    if cur_type == "MTEXT" and x is not None:
        results.append((x, y, height or 1.0, content or ""))
    return results

def seg_intersects_rect(x1, y1, x2, y2, rx0, ry0, rx1, ry1):
    """Basit segment-dikdortgen kesisim testi (Liang-Barsky benzeri, kaba ama yeterli)."""
    # Hizli red: segmentin bounding box'i dikdortgenle hic ortusmuyor mu?
    if max(x1, x2) < rx0 or min(x1, x2) > rx1:
        return False
    if max(y1, y2) < ry0 or min(y1, y2) > ry1:
        return False

    # Segmentin uc noktalarindan biri dikdortgenin icindeyse kesin kesisim.
    def inside(px, py):
        return rx0 <= px <= rx1 and ry0 <= py <= ry1
    if inside(x1, y1) or inside(x2, y2):
        return True

    # Dikdortgenin 4 kenariyla segment kesisimi kontrol et.
    def seg_seg(ax, ay, bx, by, cx, cy, dx, dy):
        d1 = (dx - cx) * (ay - cy) - (dy - cy) * (ax - cx)
        d2 = (dx - cx) * (by - cy) - (dy - cy) * (bx - cx)
        d3 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax)
        d4 = (bx - ax) * (dy - ay) - (by - ay) * (dx - ax)
        if ((d1 > 0 and d2 < 0) or (d1 < 0 and d2 > 0)) and ((d3 > 0 and d4 < 0) or (d3 < 0 and d4 > 0)):
            return True
        return False

    edges = [
        (rx0, ry0, rx1, ry0),
        (rx1, ry0, rx1, ry1),
        (rx1, ry1, rx0, ry1),
        (rx0, ry1, rx0, ry0),
    ]
    for ex1, ey1, ex2, ey2 in edges:
        if seg_seg(x1, y1, x2, y2, ex1, ey1, ex2, ey2):
            return True
    return False

def main():
    path = sys.argv[1]
    pairs = parse_dxf_pairs(path)
    entity_pairs = extract_entities_section(pairs)

    segments = collect_segments(entity_pairs, {"CUT", "BEND_UP", "BEND_DOWN"})
    mtexts = find_mtext(entity_pairs)

    print(f"Toplam CUT/BEND segment: {len(segments)}")
    print(f"Bulunan MTEXT sayisi: {len(mtexts)}")

    if not mtexts:
        print("UYARI: MTEXT bulunamadi - etiket eklenmemis olabilir.")
        return

    for (tx, ty, theight, content) in mtexts:
        # Metnin yaklasik genisligini icerik uzunlugundan tahmin et (Tahoma icin kaba oran ~0.6*height/karakter).
        char_width = theight * 0.6
        twidth = char_width * len(content)
        # AddFitted genelde ekleme noktasini metnin SOL-ALT kösesi civarinda konumlandirir - kaba varsayim.
        rx0, ry0 = tx, ty
        rx1, ry1 = tx + twidth, ty + theight

        print(f"\nMTEXT '{content}' konum=({tx:.2f},{ty:.2f}) yukseklik={theight:.2f}")
        print(f"  Tahmini kutu: ({rx0:.2f},{ry0:.2f}) -> ({rx1:.2f},{ry1:.2f})")

        overlaps = []
        for (sx1, sy1, sx2, sy2, layer) in segments:
            if seg_intersects_rect(sx1, sy1, sx2, sy2, rx0, ry0, rx1, ry1):
                overlaps.append((sx1, sy1, sx2, sy2, layer))

        if overlaps:
            print(f"  SONUC: CAKISMA VAR! {len(overlaps)} segment ile kesisiyor:")
            for o in overlaps[:5]:
                print(f"    {o}")
        else:
            print("  SONUC: Cakisma yok (bu segment listesine gore).")

if __name__ == "__main__":
    main()
