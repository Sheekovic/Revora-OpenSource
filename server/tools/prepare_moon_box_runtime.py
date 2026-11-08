"""Stage a separate game runtime. Never launches or modifies the live runtime."""
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess
from xml.dom import minidom

PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT / "bin/Debug"
BUILD = PROJECT / "obj/chi-refill-validation/bin"
TARGET = Path(r"C:\Revora\DisposablePostgres\moon-box-candidate\game")
CATALOGS = """
GameMap.dat Stats.ini Portals.ini RevivePoints.ini itemtype-new.txt ItemtypeSub.txt
ItemsPlus.ini magictype.txt rune_levexp.txt hairface_storage_type.txt coat_storage_type.txt
NeiGongInfo.ini Storage.ini WrapTypeData.ini task_reward_type.ini Monsters.txt
Questinfo.ini WelcomeMessages.txt ability_score.txt item_refine_attr.txt
item_refine_cost.txt item_refine_effect.txt item_refine_effect_ex.txt item_refine_upgrade.txt
Slayer/souls_protection.txt Slayer/JianghuAttributes.txt Slayer/JingHuCultivateStatus.txt Slayer/PokerTables.txt
Slayer/QuizShow.txt Slayer/operating_prize.txt Slayer/Roulettes.txt Slayer/StageGoal.txt
GoldenTree/global_lottery_pool.dat GoldenTree/globallotterycondition.ini
shops/Shop.dat shops/emoneyshop.ini shops/emoneyShopV2.ini shops/HonorShop.ini
shops/GoldenLeagueShop.ini shops/SlayerPointshop.ini shops/exchange_shop_goods.txt
""".split()
TOP_LEVEL = """
Geo.dat GeoIP.dat Storage.ini ability_score.txt coat_storage_type.txt
item_refine_attr.txt item_refine_cost.txt item_refine_effect.txt
item_refine_effect_ex.txt item_refine_upgrade.txt NeiGongInfo.ini signin.ini
""".split()


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def main():
    if TARGET.exists():
        raise RuntimeError("Candidate game directory exists; inspect instead of overwriting")
    if not (TARGET.parent / "data/PG_VERSION").is_file():
        raise RuntimeError("Provision the candidate database first")
    selected = {}

    def select(source, relative):
        if not source.is_file() or source.is_symlink() or source.is_junction():
            raise RuntimeError("Missing or redirected candidate input: " + relative)
        resolved = source.resolve()
        if not (resolved.is_relative_to(SOURCE.resolve()) or resolved.is_relative_to(BUILD.resolve())):
            raise RuntimeError("Candidate input escaped reviewed source roots")
        destination = (TARGET / relative).resolve()
        if not destination.is_relative_to(TARGET.resolve()):
            raise RuntimeError("Candidate destination escaped its root")
        selected[relative.lower()] = (resolved, relative)

    for name in CATALOGS:
        select(SOURCE / "database" / name, "database/" + name)
    for name in TOP_LEVEL:
        select(SOURCE / name, name)
    select(BUILD / "Revora.exe", "Revora.exe")
    for source in BUILD.glob("*.dll"):
        select(source, source.name)
    for source in (SOURCE / "database/maps").glob("*.map"):
        select(source, "database/maps/" + source.name)

    # Preserve the installed index unchanged; select only its named terrain and
    # reviewed map caches. Unknown/escaping paths are never followed.
    index = (SOURCE / "database/GameMap.dat").read_bytes()
    if len(index) < 4:
        raise RuntimeError("Truncated map index")
    count = struct.unpack_from("<I", index)[0]
    if count > 10000:
        raise RuntimeError("Unbounded map count")
    offset, unresolved = 4, []
    for _ in range(count):
        if offset + 8 > len(index):
            raise RuntimeError("Truncated map record")
        map_id, size = struct.unpack_from("<II", index, offset)
        offset += 8
        if not 1 <= size <= 4096 or offset + size + 4 > len(index):
            raise RuntimeError("Invalid map path length")
        path = index[offset:offset + size].decode("cp1252").replace("\\", "/")
        offset += size + 4
        if path.endswith(".7z"):
            path = path[:-3] + ".dmap"
        terrain = SOURCE / "database" / path
        if not terrain.resolve().is_relative_to((SOURCE / "database").resolve()):
            raise RuntimeError("Map terrain escaped database directory")
        if terrain.is_file():
            select(terrain, "database/" + path)
        elif not (SOURCE / "database/maps" / (str(map_id) + ".map")).is_file() and not 1712 <= map_id <= 1720:
            unresolved.append(map_id)
    if offset != len(index):
        raise RuntimeError("Unexpected trailing map index bytes")

    config = minidom.parse(str(BUILD / "Revora.exe.config")).documentElement
    # Preserve namespace declarations on assemblyBinding instead of hoisting
    # xmlns attributes onto configuration, which .NET Framework rejects.
    sections = [node.toxml() for node in config.childNodes if node.nodeType == node.ELEMENT_NODE and node.tagName in ("runtime", "startup")]
    clean_config = '<?xml version="1.0" encoding="utf-8"?>\n<configuration>\n' + '\n'.join(sections) + '\n</configuration>\n'
    TARGET.mkdir()
    manifest = {"source_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=PROJECT, text=True).strip(),
                "clean_build_claimed": False, "files": [], "unresolved_map_ids": unresolved}
    for source, relative in sorted(selected.values(), key=lambda pair: pair[1].lower()):
        destination = TARGET / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        source_hash = digest(source)
        if digest(destination) != source_hash:
            raise RuntimeError("Candidate copy hash mismatch")
        manifest["files"].append({"path": relative, "sha256": source_hash, "bytes": destination.stat().st_size})
    (TARGET / "Revora.exe.config").write_text(clean_config, encoding="utf-8")
    (TARGET / "Configuracion.ini").write_text(
        "[configuracion]\nIP=127.0.0.1\nGamePort=19559\nAuthPort=19558\nProtectionPort=19619\nServidor=Slayer\n\n"
        "[MySql]\nHost=127.0.0.1\nPort=25439\nUsername=revora_candidate_runtime\nPassword=\nDatabase=revora_candidate_game\n",
        encoding="ascii")
    # Fresh state; never copy character progression, board data or event status.
    (TARGET / "database/InnerPower.txt").write_text("Count=0\n", encoding="ascii")
    for name in ("database/broadcasts.txt", "database/Slayer/broadcasts.txt", "database/Slayer/PoleEmulator.txt"):
        (TARGET / name).write_text("", encoding="ascii")
    for name in ("database/exceptions", "database/gmlogs", "database/Perfection"):
        (TARGET / name).mkdir(exist_ok=True)
    for relative in ("Revora.exe.config", "Configuracion.ini", "database/InnerPower.txt", "database/broadcasts.txt", "database/Slayer/broadcasts.txt", "database/Slayer/PoleEmulator.txt"):
        path = TARGET / relative
        manifest["files"].append({"path": relative, "sha256": digest(path), "bytes": path.stat().st_size, "generated": True})
    (TARGET.parent / "game-runtime-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print("Staged", len(manifest["files"]), "files; bytes:", sum(x["bytes"] for x in manifest["files"]))
    print("Unresolved terrain IDs:", ",".join(map(str, unresolved)) or "none")
    print("Candidate server remains unstarted; no clean-source release claim.")


if __name__ == "__main__":
    main()
