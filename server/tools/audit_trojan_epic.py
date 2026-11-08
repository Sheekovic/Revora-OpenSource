"""Read-only comparison of legacy Trojan runtime metadata and the 6711 journal."""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import re
import struct
from audit_warrior_epic import item_rows, sections

QUESTS = (3269, 3278, 3275, 3270, 3276, 3271, 3277, 3272, 3273, 3279, 3280, 3274)
ITEMS = {614439, 3003335, 3003336, 3003338, 3003340}


def require(value, message):
    if not value:
        raise ValueError(message)


def bindings(raw):
    require(len(raw) >= 4, 'Missing map count')
    count, = struct.unpack_from('<I', raw)
    require(count <= 100000, 'Invalid map count')
    cursor, result = 4, {}
    for _ in range(count):
        require(cursor + 8 <= len(raw), 'Truncated map entry')
        identity, size = struct.unpack_from('<II', raw, cursor)
        cursor += 8
        require(0 < size < 1024 and cursor + size + 4 <= len(raw), 'Truncated map path')
        result.setdefault(identity, []).append(raw[cursor:cursor + size].decode('ascii'))
        cursor += size + 4
    require(cursor == len(raw), 'Trailing map bytes')
    return result


def approach(raw, start, target):
    require(len(raw) >= 8, 'Missing terrain header')
    width, height = struct.unpack_from('<ii', raw)
    require(0 < width <= 4096 and 0 < height <= 4096 and len(raw) == 8 + width * height, 'Invalid terrain dimensions')
    grid = raw[8:]
    require(set(grid) <= {0, 1}, 'Unknown terrain encoding')
    def valid(x, y):
        return 0 <= x < width and 0 <= y < height and grid[x + y * width] != 1
    occupied = {(target[0] + dx, target[1] + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
    require(valid(*start) and start not in occupied, 'Invalid entry tile')
    require(0 <= target[0] < width and 0 <= target[1] < height, 'Target outside terrain')
    queue, seen = deque([(start[0], start[1], 0)]), {start}
    while queue:
        x, y, distance = queue.popleft()
        if (x, y) not in occupied and max(abs(x - target[0]), abs(y - target[1])) <= 6:
            return dict(dimensions=[width, height], reachable=True, steps=distance, approach=[x, y])
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            point = x + dx, y + dy
            if point not in occupied and point not in seen and valid(*point):
                seen.add(point)
                queue.append((point[0], point[1], distance + 1))
    return dict(dimensions=[width, height], reachable=False)


def audit(repo, snapshot):
    client, runtime = repo.parents[1] / '6711', repo / 'bin/Debug'
    hashes = {}
    def read(path, label):
        data = path.read_bytes()
        hashes[label] = hashlib.sha256(data).hexdigest()
        return data
    data = read(snapshot, 'database_snapshot')
    db = json.loads(data.decode('utf-16' if data.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig'))
    require(db['read_only'] is True, 'Expected read-only database evidence')
    aliases = {row['id']: row['mapdoc'] for row in db['maps']}
    require(all(aliases.get(identity) == 3825 for identity in range(4121, 4125)),
            'Legacy room terrain alias changed')
    server_data = read(runtime / 'database/itemtype-new.txt', 'server_items')
    a = item_rows(server_data, identities=ITEMS)
    b = item_rows(read(client / 'ini/itemtype.dat', 'client_items'), encrypted=True, identities=ITEMS)
    items = []
    for identity in sorted(ITEMS):
        require(a[identity][:6] == b[identity][:6], 'Quest item identity or eligibility differs')
        items.append(dict(id=identity, name=a[identity][1].decode('ascii'), profession=int(a[identity][2]), level=int(a[identity][4])))
    levels = [int(f[4]) for row in server_data.splitlines() for f in [row.split(b'@@')]
              if len(f) > 4 and f[0].isdigit() and 614000 <= int(f[0]) < 615000 and f[4].isdigit()]
    quests = {}
    for _, fields in sections(read(client / 'ini/Questinfo.ini', 'client_journal')):
        identity = int(fields.get(b'MissionId', b'0'))
        if identity in QUESTS:
            require(identity not in quests, 'Duplicate Trojan journal mission')
            links = []
            for match in re.finditer(rb'\[(npc|mob) ([^\]]+)\]', b' '.join(fields.values())):
                links.append(match[0].decode('ascii'))
            quests[identity] = dict(id=identity, name=fields[b'Name'].decode('ascii'), previous=int(fields[b'Prequest']),
                                    minimum_level=int(fields[b'Lv_min']), professions=fields[b'Profession'].decode('ascii'), links=links)
    require(set(quests) == set(QUESTS), 'Incomplete Trojan journal')
    for index, identity in enumerate(QUESTS):
        require(quests[identity]['previous'] == (QUESTS[index - 1] if index else 0), 'Trojan journal order changed')
    journal_npcs = sorted({int(link.split(',')[1]) for quest in quests.values()
                           for link in quest['links'] if link.startswith('[npc ')})
    require(set(journal_npcs) <= set(db['requested_npcs']), 'Database snapshot omits a native journal actor')
    profiles = dict(sections(read(client / 'ini/npc.ini', 'client_npc_profiles')))
    maps = bindings(read(client / 'ini/GameMap.dat', 'client_map_index'))
    terrain = read(runtime / 'database/maps/3825.map', 'server_terrain_3825')
    require(3825 in maps and len(maps[3825]) == 1, 'Legacy base map missing or ambiguous in client')
    relative = maps[3825][0].replace('\\', '/')
    require(relative.startswith('map/map/') and '..' not in relative.split('/'), 'Unsafe archive binding')
    read(client / relative, 'client_terrain_archive_3825')
    actors = []
    for row in db['npcs']:
        model = row['lookface'] // 10
        name = profiles.get(('[NpcType%d]' % model).encode(), {}).get(b'Name')
        require(name is not None, 'Registered Trojan NPC profile missing')
        record = dict(row, profile=model, name=name.decode('ascii'))
        if row['mapid'] in range(4121, 4125):
            record['route'] = approach(terrain, (45, 45), (row['cellx'], row['celly']))
        actors.append(record)
    plaza = read(repo / 'Game/Features/TwinCityPlaza.cs', 'Game/Features/TwinCityPlaza.cs')
    closed = re.search(rb'ClosedDeskIds\s*=\s*new uint\[\]\s*\{([^}]+)\}', plaza)
    require(closed is not None, 'Closed desk declaration changed')
    entrance_closed = 66712 in {int(value) for value in re.findall(rb'\b\d+\b', closed[1])}
    for name in ['Npcs.cs', 'Database/MonsterTable.cs', 'Game/Map.cs']:
        read(repo / name, name)
    return dict(scope='Static client/server catalogs, read-only database actors and grid reachability; not ordinary earning or native acceptance.',
                database_observed_utc=db['observed_utc'], maps=db['maps'], actors=actors, monsters=db['monsters'], spawns=db['spawns'],
                journal=[quests[i] for i in QUESTS], items=items, maximum_cross_saber_level=max(levels),
                legacy_base_archive=relative, legacy_entrance_closed=entrance_closed,
                journal_npcs_queried=journal_npcs,
                unrelated_duplicate_map_ids=[i for i, paths in maps.items() if len(paths) > 1],
                journal_npcs_registered=[row['id'] for row in db['npcs'] if row['id'] in journal_npcs], sha256=hashes)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--snapshot', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(Path(__file__).resolve().parents[1], args.snapshot)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(missions=len(result['journal']), actors=len(result['actors']), monsters=len(result['monsters']),
                         reachable_rooms=sum(row.get('route', {}).get('reachable', False) for row in result['actors']),
                         max_weapon_level=result['maximum_cross_saber_level'], registered_journal_npcs=result['journal_npcs_registered'])))
