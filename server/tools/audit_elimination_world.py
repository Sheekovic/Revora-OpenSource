"""Read-only reachability audit for staged Elimination NPC placements.

Uses source coordinates, installed terrain and conservative 3x3 actor footprints.
This is terrain evidence, not native-client or live-world acceptance.
"""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import re
import struct

MAP_HASH = '7e8f7a8262f8ae93a314699af6d276f0324fff6c32bcc6061bffc2f336f1e817'


def routes(data, actors, origin, destinations, reach=6):
    if len(data) < 8:
        raise ValueError('Truncated map')
    w, h = struct.unpack_from('<ii', data)
    if not (0 < w <= 4096 and 0 < h <= 4096 and len(data) == 8+w*h):
        raise ValueError('Invalid map dimensions')
    grid = data[8:]
    if any(v not in (0, 1) for v in grid):
        raise ValueError('Unknown terrain flags')
    def inside(x, y):
        return 0 <= x < w and 0 <= y < h
    blocked = set()
    for x, y in actors:
        if not inside(x, y) or grid[x+y*w]:
            raise ValueError('Actor center is not walkable')
        blocked.update((a, b) for a in range(x-1, x+2) for b in range(y-1, y+2))
    def walk(x, y):
        return inside(x, y) and not grid[x+y*w] and (x, y) not in blocked
    if not walk(*origin):
        raise ValueError('Blocked origin')
    distance = {origin: 0}
    queue = deque([origin])
    while queue:
        x, y = queue.popleft()
        for dx, dy in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
            p = x+dx, y+dy
            if p in distance or not walk(*p):
                continue
            if dx and dy and (not walk(x+dx, y) or not walk(x, y+dy)):
                continue
            distance[p] = distance[x,y]+1
            queue.append(p)
    result = []
    for x, y in destinations:
        candidates = [(distance[a,b], a, b) for a in range(x-reach,x+reach+1) for b in range(y-reach,y+reach+1)
                      if (a,b) in distance and max(abs(a-x),abs(b-y)) > 1]
        if not candidates:
            raise ValueError('Unreachable interaction site: %d,%d' % (x,y))
        steps, a, b = min(candidates)
        result.append(dict(site=[x,y], approach=[a,b], steps=steps))
    return result


def audit(root, runtime):
    source = (root/'Game/Features/EliminationRules.cs').read_text()
    herbs = [tuple(map(int, p)) for p in re.findall(r'new HerbSite\((\d+),(\d+)\)', re.search(r'HerbSite\[\] Herbs=\{(.*?)\};', source, re.S)[1])]
    def coordinate(name):
        return int(re.search(r'\b'+name+r'=(\d+)', source)[1])
    georgia = coordinate('GeorgiaX'), coordinate('GeorgiaY')
    spice = coordinate('SpiceX'), coordinate('SpiceY')
    if len(herbs) != 12 or len(set(herbs)) != 12:
        raise ValueError('Expected twelve distinct herbs')
    data = (runtime/'database/maps/1011.map').read_bytes()
    if hashlib.sha256(data).hexdigest() != MAP_HASH:
        raise ValueError('Reviewed terrain changed')
    sites = [georgia]+herbs
    # Georgia's southern approach, outside her conservative footprint.
    origin = georgia[0], georgia[1]+2
    return dict(map=1011, sha256=MAP_HASH, origin=origin,
                routes=routes(data, sites, origin, sites+[spice]),
                includes_dynamic_obstacles=False, native_acceptance=False)


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--runtime', type=Path, default=Path('bin/Debug'))
    args = p.parse_args()
    print(json.dumps(audit(Path(__file__).resolve().parents[1], args.runtime), indent=2))
