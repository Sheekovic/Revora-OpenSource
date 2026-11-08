"""Guard the compiled production tree against unlocked silver read/modify/write."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


class SilverWriterTests(unittest.TestCase):
    def test_compiled_sources_use_owned_silver_mutations(self):
        project = ET.parse(ROOT / "Revora.csproj")
        paths = [ROOT / node.attrib["Include"].replace("\\", "/")
                 for node in project.iter()
                 if node.tag.endswith("Compile") and "Include" in node.attrib]
        self.assertGreater(len(paths), 100)
        violations = []
        for path in paths:
            source = path.read_text(encoding="utf-8-sig")
            # Ignore strings and comments, including verbatim strings and inactive
            # research files that are absent from the actual project compile list.
            code = re.sub(r'@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|//[^\r\n]*|/\*.*?\*/',
                          " ", source, flags=re.S)
            if re.search(r'\bMoney\s*(?:[+\-*/%&|^]=|\+\+|--)|(?:\+\+|--)\s*\w+(?:\.\w+)*\.Money\b', code):
                violations.append(str(path.relative_to(ROOT)))
        self.assertEqual([], violations, "Use the owned silver mutation API.")


if __name__ == "__main__":
    unittest.main()
