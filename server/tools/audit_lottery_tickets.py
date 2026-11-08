"""Read-only audit of the old-to-small lottery ticket exchange contract."""
import json
from pathlib import Path
from audit_warrior_epic import digest, item_rows, require


def audit():
    root = Path(__file__).resolve().parents[1]
    client = (root.parents[1] / "6711/ini/itemtype.dat").read_bytes()
    server = (root / "bin/Debug/database/itemtype.txt").read_bytes()
    require(digest(client) == "d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a", "Client catalog changed")
    require(digest(server) == "594ffe5f252c9445a6087004372519a6f150f089c7bff42d4ec30400911a0e88", "Server catalog changed")
    names = {710212: b"LotteryTicket", 711504: b"SmallLotteryTicket"}
    native, hosted = item_rows(client, True, names), item_rows(server, False, names)
    for identity, name in names.items():
        require(native[identity][1] == name, "Unexpected ticket identity")
        require(native[identity][:8] == hosted[identity][:8], "Ticket requirements differ")
    for rows in (native, hosted):
        require(b"Swap~it~for~3~Small~Lottery~Tickets" in b" ".join(rows[710212]), "Old ticket exchange contract differs")
        require(b"Pay~3~Small~Lottery~Tickets" in b" ".join(rows[711504]), "Small ticket draw cost differs")
    return {"identities": 2, "exchange": "710212 x1 -> 711504 x3", "client_sha256": digest(client),
            "server_sha256": digest(server), "limits": "Catalog contract only; no lottery odds or native draw acceptance certified."}


if __name__ == "__main__":
    print(json.dumps(audit(), indent=2))
