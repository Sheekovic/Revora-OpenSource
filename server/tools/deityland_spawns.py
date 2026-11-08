"""Deterministic DeityLand placements on connected, walkable hunting islands."""
from pathlib import Path
import collections
import struct


def spawn_cells(root):
    db = Path(root) / 'bin/Debug/database'
    floor = (db / 'maps/10250.map').read_bytes()
    w, h = struct.unpack_from('<ii', floor)
    cells = floor[8:]
    assert len(cells) == w*h and set(cells) == {0, 1}
    dmap = (db / 'map/map/island-snail01.DMap').read_bytes()
    assert struct.unpack_from('<ii', dmap, 268) == (w,h)
    offset = 276+h*(w*6+4)
    count = struct.unpack_from('<I', dmap, offset)[0]
    assert count == 32
    gates = [struct.unpack_from('<iii', dmap, offset+4+i*12)[:2] for i in range(count)]

    def walk(x,y):
        # Map.LoadMonsters checks Floor.Monster: cache byte 0 is traversable.
        return 0 <= x < w and 0 <= y < h and cells[y*w+x] == 0

    seen = set()
    regions = []
    for gx,gy in gates:
        for y in range(gy-5,gy+6):
            for x in range(gx-5,gx+6):
                if not walk(x,y) or (x,y) in seen:
                    continue
                queue = collections.deque([(x,y)])
                seen.add((x,y))
                region = []
                while queue:
                    p = queue.popleft()
                    region.append(p)
                    xx,yy = p
                    for v in ((xx-1,yy),(xx+1,yy),(xx,yy-1),(xx,yy+1)):
                        if v not in seen and walk(*v):
                            seen.add(v)
                            queue.append(v)
                if len(region) >= 1000:
                    regions.append(region)
    regions.sort(key=lambda r:(-len(r), min(r)))
    assert len(regions) == 15, 'Expected 15 hunting regions plus the small gate junction'
    chosen = []
    sizes = []
    for index,region in enumerate(regions):
        candidates = []
        for x,y in sorted(region):
            if x%4 or y%4:
                continue
            if max(abs(x-1021),abs(y-1281)) < 35:
                continue
            if any(max(abs(x-gx),abs(y-gy)) < 20 for gx,gy in gates):
                continue
            if not all(walk(x+dx,y+dy) for dx in (-1,0,1) for dy in (-1,0,1)):
                continue
            assert struct.unpack_from('<H',dmap,276+y*(w*6+4)+x*6)[0] == 0
            candidates.append((x,y))
        amount = 3 if index < 10 else 2
        assert len(candidates) >= amount
        # First creature on the arrival island is near camp; other islands
        # start centrally, then spread the remaining creatures across the region.
        camp = any(max(abs(x-1022),abs(y-1283)) <= 8 for x,y in region)
        center = (1022,1283) if camp else (sum(x for x,y in region)//len(region),sum(y for x,y in region)//len(region))
        distance = lambda a,b:(a[0]-b[0])**2+(a[1]-b[1])**2
        selected = [min(candidates,key=lambda p:(distance(p,center),p))]
        while len(selected) < amount:
            p = max(candidates,key=lambda p:(min(distance(p,s) for s in selected),p))
            assert all(distance(p,s) >= 12**2 for s in selected)
            selected.append(p)
        chosen.extend(selected)
        sizes.append((len(region),selected))
    assert len(chosen) == len(set(chosen)) == 40
    return chosen, sizes


if __name__ == '__main__':
    points, regions = spawn_cells(Path(__file__).resolve().parents[1])
    print('40 clear spawns across 15 gate-connected hunting areas:')
    for size, points in regions:
        print(size, points)
