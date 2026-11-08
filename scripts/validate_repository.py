"""Validate tracked publication content; diagnostics never print secret values."""
import hashlib
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
paths = subprocess.check_output(['git', '-C', str(ROOT), 'ls-files', '-z']).decode().split('\0')
paths = [p for p in paths if p]
manifest = json.loads((ROOT / 'docs/source-manifest.json').read_text(encoding='utf-8'))
allowed_binaries = manifest['vendored_binary_sha256']
prohibited_suffixes = {'.exe','.pdb','.pfx','.p12','.key','.dmp','.pcap','.pcapng','.zip','.7z','.rar','.bak','.log'}
secret_patterns = {
    'private key': re.compile(rb'-----BEGIN (?:RSA |EC |DSA |OPENSSH |PGP )?PRIVATE KEY'),
    'GitHub token': re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b'),
    'AWS key': re.compile(rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'service token': re.compile(rb'\b(?:xox[baprs]-[A-Za-z0-9-]{20,}|AIza[A-Za-z0-9_-]{35}|sk_live_[A-Za-z0-9]{20,})\b'),
    'credential URL': re.compile(rb'[a-zA-Z][a-zA-Z0-9+.-]*://[^\s/@:]{1,80}:[^\s/@]{1,120}@'),
}
errors = []
for rel in paths:
    path = pathlib.PurePosixPath(rel)
    if any(part in {'.migration','.local','.git','.revora-endpoint','bin','obj','node_modules','__pycache__'} for part in path.parts):
        errors.append((rel,'private/generated directory'))
    if path.suffix.lower() in prohibited_suffixes or path.name in {'.env','appsettings.Development.json','Configuracion.ini','app.config'}:
        errors.append((rel,'private/generated artifact'))
    if path.name.startswith('.env.') and path.name != '.env.example':
        errors.append((rel,'local environment configuration'))
    if path.name.endswith('.secrets.json'):
        errors.append((rel,'local secrets configuration'))
    data = (ROOT / rel).read_bytes()
    if path.suffix.lower() == '.dll':
        if allowed_binaries.get(rel) != hashlib.sha256(data).hexdigest():
            errors.append((rel,'binary absent from reviewed dependency manifest'))
    if len(data) > 50 * 1024 * 1024:
        errors.append((rel,'oversized publication file'))
    for label, pattern in secret_patterns.items():
        if pattern.search(data):
            errors.append((rel,label))

required = ['README.md','LICENSE','THIRD_PARTY_NOTICES.md','docs/source-manifest.json',
            'START-REVORA.cmd','docs/QUICKSTART.md','docs/AI_AGENT_SETUP.md',
            'docs/CLIENT_ENDPOINT_TOOL.md','tools/ClientEndpoint/Gui/Revora.ClientEndpoint.csproj',
            'tools/ClientEndpoint/VERSION','tools/ClientEndpoint/Core/ServerDatCodec.cs',
            'tools/ClientEndpoint/Core/ServerDatFiles.cs',
            'website/site/index.html','website/site/app.js','website/site/styles.css','website/site/config.js',
            'launcher/PlayRevora.Launcher.sln','website/src/Revora.Web/Revora.Web.csproj','server/Revora.csproj',
            'licenses/Npgsql-4.1.14-LICENSE.txt','licenses/dotnet-corefx-LICENSE.txt','website/licenses/COPS-v7-LICENSE.txt']
for rel in required:
    if rel not in paths:
        errors.append((rel,'required tracked file missing'))
for rel in allowed_binaries:
    if rel not in paths:
        errors.append((rel,'declared dependency missing'))

if errors:
    for rel, reason in errors:
        print(f'FAIL {rel}: {reason}')
    sys.exit(1)
print(f'PASS {len(paths)} tracked files: component layout, required notices, artifact exclusions, dependency hashes, and high-confidence secret patterns.')
print('This bounded check is not a guarantee that all possible secrets or licensing issues are absent.')
