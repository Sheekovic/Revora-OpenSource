"""Prepare Revora's 16 bidirectional DeityLand crossings; never edit live data."""
from pathlib import Path
import collections
import hashlib
import json
import struct

ROOT = Path(__file__).resolve().parents[1]
DB = ROOT / 'bin/Debug/database'


def prepare():
    dmap = (DB / 'map/map/island-snail01.DMap').read_bytes()
    floor = (DB / 'maps/10250.map').read_bytes()
    w, h = struct.unpack_from('<ii', floor)
    assert (w, h) == struct.unpack_from('<ii', dmap, 268)
    cells = floor[8:]
    assert len(cells) == w*h and set(cells) == {0, 1}
    offset = 276 + h*(w*6+4)
    count = struct.unpack_from('<I', dmap, offset)[0]
    gates = {}
    for i in range(count):
        x, y, gate = struct.unpack_from('<iii', dmap, offset+4+i*12)
        assert gate not in gates and 0 <= x < w and 0 <= y < h
        gates[gate] = (x, y)
    assert set(gates) == set(range(32))

    # Map's cache loader treats byte 0 as walkable; byte 1 is blocked.
    def walk(x, y):
        return 0 <= x < w and 0 <= y < h and cells[y*w+x] == 0

    labels = {}
    sizes = {}
    def component(start):
        if start in labels:
            return labels[start]
        key = len(sizes)
        queue = collections.deque([start])
        labels[start] = key
        size = 0
        while queue:
            x, y = queue.popleft()
            size += 1
            for p in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if p not in labels and walk(*p):
                    labels[p] = key
                    queue.append(p)
        sizes[key] = size
        return key

    gate_components = {}
    landings = {}
    for gate, (gx, gy) in sorted(gates.items()):
        near = [(x,y) for y in range(gy-6,gy+7) for x in range(gx-6,gx+7) if walk(x,y)]
        regions = {component(p) for p in near}
        region = max(regions, key=lambda k:sizes[k])
        assert sizes[region] >= 50
        gate_components[gate] = region
        choices = []
        for y in range(gy-18,gy+19):
            for x in range(gx-18,gx+19):
                distance = max(abs(x-gx),abs(y-gy))
                if not 5 <= distance <= 18 or labels.get((x,y)) != region:
                    continue
                if any(max(abs(x-px),abs(y-py)) <= 4 for px,py in gates.values()):
                    continue
                if all(walk(x+dx,y+dy) for dx in range(-1,2) for dy in range(-1,2)):
                    choices.append((distance, (x-gx)**2+(y-gy)**2, x, y))
        assert choices, ('No safe landing', gate)
        _, _, x, y = min(choices)
        assert struct.unpack_from('<H', dmap, 276+y*(w*6+4)+x*6)[0] == 0
        landings[gate] = (x,y)

    # Adjacent numbered endpoints are Revora's documented crossing pairs.
    # These pairings are inferred from geometry, not claimed official rules.
    graph = collections.defaultdict(set)
    lines = ['[10250]', 'Count=32']
    for gate in range(32):
        source = gate_components[gate]
        target = gate_components[gate ^ 1]
        assert source != target
        graph[source].add(target)
        x,y = gates[gate]
        tx,ty = landings[gate ^ 1]
        lines += [f'PortalEnter{gate}=10250 {x} {y}', f'PortalExit{gate}=10250 {tx} {ty}']
    visited = set()
    queue = [gate_components[27]]  # Southern arrival island.
    while queue:
        region = queue.pop()
        if region in visited:
            continue
        visited.add(region)
        queue.extend(graph[region]-visited)
    assert visited == set(gate_components.values()), 'Crossings leave an island unreachable'
    original = (DB / 'Portals.ini').read_bytes()
    assert b'[10250]' not in original, 'Inspect existing routes before replacing them'
    output = ROOT / 'obj/deityland-gates'
    output.mkdir(exist_ok=True)
    (output / 'Portals.ini').write_bytes(original+b'\r\n'+'\r\n'.join(lines).encode('ascii')+b'\r\n')
    evidence = dict(dmap_sha256=hashlib.sha256(dmap).hexdigest(),
                    floor_sha256=hashlib.sha256(floor).hexdigest(),
                    original_portals_sha256=hashlib.sha256(original).hexdigest(),
                    regions=len(visited), gates=gates, landings=landings)
    (output / 'review.json').write_text(json.dumps(evidence,indent=2))
    print(f'Validated 32 routes across {len(visited)} connected regions; safe landings and original routes preserved.')


if __name__ == '__main__':
    prepare()
