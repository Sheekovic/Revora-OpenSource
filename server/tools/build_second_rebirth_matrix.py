"""Convert reviewed public retention facts to numeric Revora data; no network or client writes."""
import argparse
import csv
import hashlib
import html
import io
import re
from pathlib import Path

SOURCE_SHA256 = "8ed88353f6f06289465d3062a756a1139a944e15b728ce70c5efc50a9fd466eb"
ROLES = {"trojan": 1, "warrior": 2, "archer": 4, "ninja": 5, "monk/saint": 6,
         "monk": 6, "pirate": 7, "dragon warrior": 8, "water taoist": 13, "fire taoist": 14}
MAGIC = {"accuracy":1015,"advanced cure":1175,"bless":9876,"bloody bat":4050,"cruel shade":3050,
 "cure":1005,"cyclone":1110,"divine hare":1350,"dodge":3080,"dragon roar":12280,"fire":1001,
 "fire circle":1120,"fire evil":4060,"flying moon":1320,"freezing arrow":5000,"gale bomb":11070,
 "golem":1270,"healing rain":1055,"invisibility":1075,"iron shirt":5100,"magic shield":1090,
 "meditation":1195,"pervade":3090,"poison star":6002,"poisonous arrows":5002,"reflect":3060,
 "revive":1050,"roar":1040,"serenity":10400,"shield":1020,"spirit healing":1190,"star of accuracy":1085,
 "stigma":1095,"summon guard":4000,"superman":1025,"thunder":1000,"toxic fog":6001,"water elf":1280}
ALIASES = {"cdragon roar":"dragon roar","gdragon roar":"dragon roar","faccuracy":"accuracy",
 "freezing arrows":"freezing arrow","gale bombe":"gale bomb","spirit heading":"spirit healing",
 "spirithealing":"spirit healing","poisonous arrow":"poisonous arrows","poison":"poisonous arrows"}

def clean(value):
    return " ".join(html.unescape(re.sub(r"<[^>]+>", " ", value)).split())

def extract(text):
    # A transition may span multiple sibling DIVs. Strong tags are not reliable
    # row delimiters either; use its heading and actual role/name separators.
    heads = list(re.finditer(r"<h4[^>]*>(.*?)</h4>", text, re.S | re.I))
    alternatives = "|".join(re.escape(role) for role in sorted(ROLES, key=len, reverse=True))
    pattern = "(" + alternatives + r")\s*>\s*(.*?)(?=(?:" + alternatives + r")\s*>|$)"
    rows = {}
    for index, heading in enumerate(heads):
        parts = [v.strip().lower() for v in clean(heading[1]).split(">") if v.strip()]
        if len(parts) != 2 or any(v not in ROLES for v in parts):
            raise ValueError("Unrecognized transition heading")
        body = text[heading.end():heads[index + 1].start() if index + 1 < len(heads) else len(text)]
        body = re.split(r"</table>", body, maxsplit=1, flags=re.I)[0]
        for role, arts in re.findall(pattern, clean(body), re.S | re.I):
            key = tuple(ROLES[v] for v in parts) + (ROLES[role.lower()],)
            rows.setdefault(key, []).append(arts.strip())
    return rows

def magic_ids(arts):
    arts = arts.lower().replace("spirit healing and golem", "spirit healing,golem")
    arts = arts.replace("healing rain water elf", "healing rain,water elf").replace("flying moon . reflect", "flying moon,reflect").replace("(xp)", "")
    result = set()
    for label in arts.split(","):
        label = " ".join(label.split())
        label = ALIASES.get(label, label)
        # Ordinary destination-school arts are supplied by the runtime planner.
        # Pure and special rebirth arts have separate history gates.
        if not label or "skill" in label or label == "fire taoist":
            continue
        result.add(MAGIC[label])
    return sorted(result)

def render(source):
    if hashlib.sha256(source).hexdigest() != SOURCE_SHA256:
        raise ValueError("Official source changed; review before regenerating")
    rows = extract(source.decode("utf-8"))
    if sum(map(len, rows.values())) != 729 or len(rows) != 728:
        raise ValueError("Unexpected published transition coverage")
    out = io.StringIO(newline="")
    writer = csv.writer(out, lineterminator="\n")
    writer.writerow(["first_school", "second_school", "third_school", "retained_magic_ids", "evidence"])
    for first in sorted(set(ROLES.values())):
        for second in sorted(set(ROLES.values())):
            for third in sorted(set(ROLES.values())):
                key = first, second, third
                if key in ((4, 7, 4), (4, 7, 1)):
                    # Explicit Revora retention inference; no inferred new grants.
                    ids, evidence = [5002, 11070], "revora-retention-inference"
                else:
                    if len(rows.get(key, [])) != 1:
                        raise ValueError("Unexpected missing or duplicate transition")
                    ids, evidence = magic_ids(rows[key][0]), "official-current"
                writer.writerow([*key, ";".join(map(str, ids)), evidence])
    return out.getvalue()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--html", required=True, type=Path)
    parser.add_argument("--output", type=Path, default=Path("Game/Features/Data/SecondRebirthSkills.csv"))
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    generated = render(args.html.read_bytes())
    if args.check:
        if args.output.read_text(encoding="utf-8") != generated:
            raise SystemExit("Numeric transition data differs from the reviewed source")
    else:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(generated, encoding="utf-8", newline="")
    print("729 numeric transitions: 727 published; 2 explicit retention inferences.")

if __name__ == "__main__":
    main()
