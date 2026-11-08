import importlib.util
from pathlib import Path
import sys
import tempfile
import subprocess
import lief

script=Path(__file__).resolve().parents[1]/'patch_direct_native.py'
spec=importlib.util.spec_from_file_location('patch',script)
patch=importlib.util.module_from_spec(spec);spec.loader.exec_module(patch)
source=Path(sys.argv[1]);original=source.read_bytes()
first,manifest=patch.generate(original);second,_=patch.generate(original)
assert first==second
image=lief.PE.parse(list(first));before=lief.PE.parse(list(original))
for entry in manifest['patches']:
 rva=int(entry['rva'],16);pre=bytes.fromhex(entry['preimage']);post=bytes.fromhex(entry['postimage'])
 assert original[before.rva_to_offset(rva):before.rva_to_offset(rva)+len(pre)]==pre
 assert first[image.rva_to_offset(rva):image.rva_to_offset(rva)+len(post)]==post
assert image.get_import('RevoraAuthClient.dll').get_entry('RevoraAuthDirectPatch2429V2')
try:patch.generate(b'unknown client')
except ValueError:pass
else:raise AssertionError('Unknown hash accepted')
with tempfile.TemporaryDirectory(prefix='revora-patch-') as temp:
 target=Path(temp)/'derivative.exe';record=Path(temp)/'manifest.json'
 subprocess.run([sys.executable,str(script),str(source),str(target),str(record)],check=True,capture_output=True)
 assert target.read_bytes()==first
 subprocess.run([sys.executable,str(script),str(source),str(target),str(record),'--restore'],check=True,capture_output=True)
 assert target.read_bytes()==original
print('PASS deterministic output; exact pre/postimages; versioned IAT; unknown hash rejection; byte-exact reversal')
