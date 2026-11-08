"""Guard against reply-only NPC coordinators; this is not gameplay acceptance."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]


def code_only(source):
    # Preserve newlines while excluding comments, normal/verbatim strings and chars.
    tokens = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*.*?\*/'
    return re.sub(tokens, lambda m: "\n" * m[0].count("\n"), source, flags=re.S)


def method_body(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 1
    for index in range(opening + 1, len(source)):
        depth += (source[index] == "{") - (source[index] == "}")
        if depth == 0:
            return source[opening + 1:index]
    raise ValueError("Unclosed method body")


def handlers(source, argument):
    return set(re.findall(
        r"\b([A-Z]\w*)(?:\.Current)?\.HandleNpc\s*\(\s*client\s*,\s*"
        + re.escape(argument) + r"\s*\)", source))


def missing_openings(npcs, packets):
    openings = method_body(code_only(npcs), "void GetDialog(")
    replies = code_only(packets).split("case PacketID.NpcDialog:", 1)[1]
    # Outer switch cases have this indentation; nested cases do not end the branch.
    replies = re.split(r"\n {16}case ", replies, maxsplit=1)[0]
    return handlers(replies, "req") - handlers(openings, "npcRequest")


class NpcDispatchCoverageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.npcs = (ROOT / "Npcs.cs").read_text(encoding="utf-8-sig")
        cls.packets = (ROOT / "Network/PacketHandler.cs").read_text(encoding="utf-8-sig")

    def test_all_registered_reply_coordinators_have_opening_routes(self):
        self.assertEqual(set(), missing_openings(self.npcs, self.packets))

    def test_original_warrior_omission_is_detected(self):
        hook = "if (WarriorEpicQuest.HandleNpc(client, npcRequest)) return;"
        self.assertEqual(1, self.npcs.count(hook))
        self.assertEqual({"WarriorEpicQuest"}, missing_openings(self.npcs.replace(hook, ""), self.packets))

    def test_comments_and_strings_cannot_replace_registration(self):
        hook = "if (WarriorEpicQuest.HandleNpc(client, npcRequest)) return;"
        for replacement in ("// " + hook, "/*\n" + hook + "\n*/", 'var fake = "' + hook + '";'):
            with self.subTest(replacement=replacement):
                self.assertEqual({"WarriorEpicQuest"}, missing_openings(self.npcs.replace(hook, replacement), self.packets))

    def test_elsewhere_in_the_file_does_not_satisfy_opening(self):
        hook = "if (WarriorEpicQuest.HandleNpc(client, npcRequest)) return;"
        source = self.npcs.replace(hook, "") + "\nvoid Unrelated() { " + hook + " }"
        self.assertEqual({"WarriorEpicQuest"}, missing_openings(source, self.packets))


if __name__ == "__main__":
    unittest.main()
