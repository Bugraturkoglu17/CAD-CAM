import sys

path = sys.argv[1]
want_type = sys.argv[2] if len(sys.argv) > 2 else "LINE"
which = int(sys.argv[3]) if len(sys.argv) > 3 else 0  # 0 = first occurrence

with open(path, encoding="utf-8", errors="replace") as f:
    lines = [l.rstrip("\n").rstrip("\r") for l in f]

pairs = [(lines[i].strip(), lines[i + 1].strip()) for i in range(0, len(lines) - 1, 2)]

in_ent = False
cur_type = None
current = []
found = []

for code, val in pairs:
    if code == "2" and val == "ENTITIES":
        in_ent = True
        continue
    if code == "0" and val == "ENDSEC":
        in_ent = False
    if not in_ent:
        continue
    if code == "0":
        if cur_type == want_type:
            found.append(current)
        cur_type = val
        current = [(code, val)]
        continue
    current.append((code, val))
if cur_type == want_type:
    found.append(current)

if len(found) <= which:
    print(f"only {len(found)} {want_type} entities found")
else:
    for code, val in found[which]:
        print(code, "=", val)
