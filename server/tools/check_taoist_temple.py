"""Read-only Taoist temple prerequisites; does not certify an earning route."""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import struct

from stage_gloomy_jail import index_records


def inspect(client, database):
    index = (client / 'ini/GameMap.dat').read_bytes()
    if hashlib.sha256(index).hexdigest() != '86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0':
        raise ValueError('Unreviewed client map index')
    if (3933, b'map/map/epicm-taoist.7z', 256) not in index_records(index):
        raise ValueError('Taoist temple identity missing')
    terrain = (database / 'maps/3933.map').read_bytes()
    terrain_hash = hashlib.sha256(terrain).hexdigest()
    if terrain_hash != 'e0ad4f2c8242e8722f7a12679946012c5d972cbd0339d563d2dc8ea9e09e9716':
        raise ValueError('Unreviewed server terrain')
    width, height = struct.unpack_from('<II', terrain)
    if (width, height, len(terrain)) != (132, 132, 17432):
        raise ValueError('Invalid terrain dimensions')
    sites = [(81, 75), (80, 80), (64, 64), (48, 48), (76, 83)]
    # Map.AddNpc reserves the center and its eight neighbors for talkers.
    occupied = {(x + dx, y + dy) for x, y in sites
                for dx in (-1, 0, 1) for dy in (-1, 0, 1)}

    def walkable(x, y):
        return (0 <= x < width and 0 <= y < height
                and terrain[8 + y * width + x] != 1 and (x, y) not in occupied)

    landing = (84, 77)
    if not walkable(*landing):
        raise ValueError('Landing blocked')
    reached, queue = {landing}, deque([landing])
    # Cardinal-only paths avoid assuming client diagonal corner behavior.
    while queue:
        x, y = queue.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            point = (x + dx, y + dy)
            if point not in reached and walkable(*point):
                reached.add(point)
                queue.append(point)
    approaches = []
    for x, y in sites:
        options = sorted((a, b) for a, b in reached if max(abs(a-x), abs(b-y)) == 2)
        if not options:
            raise ValueError('Unreachable NPC approach')
        approaches.append({'npc': [x, y], 'approach': options[0]})
    return {'map': 3933, 'terrain_sha256': terrain_hash, 'landing': landing,
            'cardinal_reachable_cells': len(reached), 'approaches': approaches,
            'ordinary_entry_verified': False, 'earning_route_verified': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', required=True, type=Path)
    parser.add_argument('--database', required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps(inspect(args.client, args.database), indent=2))
