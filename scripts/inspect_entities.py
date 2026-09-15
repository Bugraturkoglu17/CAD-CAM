import sys
from collections import Counter

path = sys.argv[1]
with open(path, encoding="utf-8", errors="replace") as f:
    lines = [l.rstrip("\n").rstrip("\r") for l in f]
pairs = [(lines[i].strip(), lines[i + 1].strip()) for i in range(0, len(lines) - 1, 2)]

in_ent = False
cur_type = None
counts = Counter()

for code, val in pairs:
    if code == "2" and val == "ENTITIES":
        in_ent = True
        continue
    if code == "0" and val == "ENDSEC":
        in_ent = False
    if not in_ent:
        continue
    if code == "0":
        cur_type = val
        continue
    if code == "8":
        counts[(cur_type, val)] += 1

for k, v in sorted(counts.items()):
    print(k, v)
