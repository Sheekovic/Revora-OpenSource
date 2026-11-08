"""Read-only Ancient Devil 6711 item/terrain evidence. Never installs content."""
import argparse
from collections import deque
import hashlib
import json
import re
from pathlib import Path
import struct
from audit_warrior_epic import item_rows, require
from ClientPatches.prepare_shop_buy_hotfix import transform

# Later-class pair identities are catalog evidence, not recovered guard scripts.
PAIRS = {
    "Warrior": (710016, 710011), "Trojan": (710017, 710012),
    "Fire": (710018, 710013), "Water": (710019, 710014),
    "Archer": (710020, 710015), "Ninja": (710634, 710635),
    "Monk": (711335, 711336), "Pirate": (711599, 711600),
    "DragonWarrior": (3004946, 3004947), "Windwalker": (3301711, 3301712),
}
GUARDS = ((229,233), (191,201), (181,176), (206,179), (243,219))
SWITCH = (218,208)
HASHES = {
    "client_achievements": "df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc",
    "client_monsters": "99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585",
    "server_monsters": "99d5f7d6b3ece68d9d17cb31d3170ab77c8f2501d80af15075cad467696cac9b",
    "client_items": "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a",
    "server_items": "1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef",
    "terrain": "b1974128a5de1efff2be6fa46482aa75ab46e6d4123e01332ce9117c2ef2574f",
}

def sha(data):
    return hashlib.sha256(data).hexdigest()

def index_entry(data, target):
    require(len(data)>=4, "Truncated index")
    count, = struct.unpack_from("<I", data)
    require(count<100000, "Unbounded index")
    pos, matches = 4, []
    for _ in range(count):
        require(pos+8<=len(data), "Truncated record")
        identity, size = struct.unpack_from("<II", data, pos)
        pos += 8
        require(0<size<1024 and pos+size+4<=len(data), "Invalid index path")
        path = data[pos:pos+size]
        pos += size
        flag, = struct.unpack_from("<I", data, pos)
        pos += 4
        if identity==target:
            matches.append((path.decode("ascii"),flag))
    require(pos==len(data), "Trailing index bytes")
    require(len(matches)==1, "Missing or duplicate target map")
    return matches[0]

def topology(data, guards=GUARDS, switch=SWITCH):
    require(len(data)>=8, "Truncated terrain")
    width,height=struct.unpack_from("<ii",data)
    require(0<width<=2048 and 0<height<=2048 and len(data)==8+width*height, "Invalid terrain dimensions")
    grid=data[8:]
    require(set(grid)<={0,1}, "Unknown terrain encoding")
    actors=tuple(guards)+(switch,)
    require(all(0<=x<width and 0<=y<height for x,y in actors), "Actor outside terrain")
    occupied={(x+dx,y+dy) for x,y in actors for dx in (-1,0,1) for dy in (-1,0,1)}
    def valid(p):
        x,y=p
        return 0<=x<width and 0<=y<height and p not in occupied and grid[x+y*width]==0
    near=[(x,y) for x in range(switch[0]-6,switch[0]+7) for y in range(switch[1]-6,switch[1]+7) if valid((x,y))]
    require(near, "Switch has no legal approach")
    start=min(near,key=lambda p:(abs(p[0]-switch[0])+abs(p[1]-switch[1]),p))
    reached,pending={start},deque([start])
    while pending:
        x,y=pending.popleft()
        for point in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if point not in reached and valid(point):
                reached.add(point);pending.append(point)
    approaches=[]
    for actor in actors:
        options=[p for p in reached if max(abs(p[0]-actor[0]),abs(p[1]-actor[1]))<=6]
        require(options,"Quest actor has no shared walking approach")
        approaches.append({"actor":actor,"approach":min(options,key=lambda p:(abs(p[0]-actor[0])+abs(p[1]-actor[1]),p))})
    return {"dimensions":[width,height],"probe_start":start,"reachable_cells":len(reached),"approaches":approaches}

def devil_profile(data):
    rows=re.findall(rb'^\[AncientDevil\]\r?$[\s\S]*?(?=^\[|\Z)',data,re.M)
    require(len(rows)==1,"Missing or ambiguous Ancient Devil profile")
    expected={"MaxLife":65535,"Level":120,"TypeID":2407,"Boss":0,"ZoomPercent":150}
    for key,value in expected.items():
        fields=re.findall(rb'^'+key.encode()+rb'=(\d+)\r?$',rows[0],re.M)
        require(len(fields)==1 and int(fields[0])==value,"Ancient Devil profile changed: "+key)
    return expected

def collection_achievement(data):
    rows=[line.split() for line in data.splitlines() if len(line.split())>1 and line.split()[1]==b"11020"]
    require(len(rows)==1,"Missing or ambiguous collection achievement")
    row=rows[0]
    require(len(row)==7 and row[0]==b"60303" and row[4]==b"10","Collection achievement identity/score changed")
    return {"ui_identity":60303,"achievement_id":11020,"score":10}

def audit(client,runtime):
    paths={"client_items":client/"ini/itemtype.dat","server_items":runtime/"database/itemtype-new.txt",
           "terrain":runtime/"database/maps/1082.map","client_monsters":client/"ini/monster.dat",
           "server_monsters":runtime/"database/Monsters.txt","client_achievements":client/"ini/Achievement.dat"}
    inputs={k:p.read_bytes() for k,p in paths.items()}
    for key,value in inputs.items():
        require(sha(value)==HASHES[key],"Reviewed input changed: "+key)
    achievements=transform(inputs["client_achievements"])
    require(transform(achievements,True)==inputs["client_achievements"],"Achievement codec did not round trip")
    collection=collection_achievement(achievements)
    identities={identity for pair in PAIRS.values() for identity in pair}
    native=item_rows(inputs["client_items"],True,identities)
    server=item_rows(inputs["server_items"],False,identities)
    for identity in identities:
        require(len(native[identity])>53 and len(server[identity])>53,"Incomplete item record")
        require(native[identity][1]==server[identity][1] and native[identity][53]==server[identity][53],"Catalog identity/description mismatch")
    for _,amulet in PAIRS.values():
        require(b"nine~different~classes" in native[amulet][53],"Amulet collection requirement changed")
    plain=transform(inputs["client_monsters"])
    require(transform(plain,True)==inputs["client_monsters"],"Monster catalog codec did not round trip")
    native_profile=devil_profile(plain)
    require(native_profile==devil_profile(inputs["server_monsters"]),"Ancient Devil profile mismatch")
    indexes={}
    for label,path in (("client",client/"ini/GameMap.dat"),("server",runtime/"database/GameMap.dat")):
        data=path.read_bytes();entry=index_entry(data,1082)
        require(entry==("map/map/idland-map.7z",256),"Unexpected Ancient Island terrain registration")
        indexes[label]={"hash":sha(data),"entry":entry}
    return {"item_pairs":PAIRS,"distinct_amulets":10,"required_distinct_classes":9,
            "hashes":HASHES,"collection_achievement":collection,"ancient_devil_profile":native_profile,"indexes":indexes,"topology":topology(inputs["terrain"]),
            "limitations":["Static catalog and terrain evidence only", "No admission, exchange, encounter or reward validation", "No native-client acceptance or deployment"]}

if __name__=="__main__":
    parser=argparse.ArgumentParser();parser.add_argument("--client",type=Path,required=True);parser.add_argument("--runtime",type=Path,required=True)
    args=parser.parse_args();print(json.dumps(audit(args.client,args.runtime),indent=2))
