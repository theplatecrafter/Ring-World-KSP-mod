"""Publish an already-built, verified release using Git's GitHub credential helper.

No credentials are saved or printed. Run only when publication is authorized.
"""
import sys
import hashlib
import json
import pathlib
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from release_notes import release_body

root = pathlib.Path(__file__).resolve().parents[1]
subprocess.run([sys.executable, str(root/'tools/verify_release.py')], cwd=root, check=True)
repo = 'theplatecrafter/Ring-World-KSP-mod'
version_data=json.loads((root/'GameData/NivenRingworld/NivenRingworld.version').read_text())['VERSION']
version='.'.join(str(version_data[k]) for k in ('MAJOR','MINOR','PATCH'))
tag = 'Release-v'+version
archive = root / f'artifacts/NivenRingworld-{version}.zip'
checksum = archive.with_suffix('.zip.sha256')
assert checksum.read_text().split()[0] == hashlib.sha256(archive.read_bytes()).hexdigest()
assert subprocess.check_output(['git', 'branch', '--show-current'], cwd=root, text=True).strip() == 'main'
assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=root, text=True).strip(), 'Working tree is dirty'
commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
assert subprocess.check_output(['git', 'rev-parse', tag + '^{commit}'], cwd=root, text=True).strip() == commit
auth = subprocess.run(['git', 'credential', 'fill'], cwd=root,
                      input='protocol=https\nhost=github.com\n\n',
                      capture_output=True, text=True, check=True)
credentials = dict(line.split('=', 1) for line in auth.stdout.splitlines() if '=' in line)
token = credentials['password']

def request(url, method='GET', data=None, binary=False):
    payload = data if binary else (json.dumps(data).encode() if data is not None else None)
    headers = {'Authorization': 'Bearer ' + token, 'User-Agent': 'NivenRingworld-release',
               'Accept': 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28'}
    if payload is not None:
        headers['Content-Type'] = 'application/octet-stream' if binary else 'application/json'
    with urllib.request.urlopen(urllib.request.Request(url, data=payload, headers=headers, method=method), timeout=180) as response:
        return json.load(response)

base = 'https://api.github.com/repos/' + repo
releases = request(base + '/releases')
existing = next((release for release in releases if release['tag_name'] == tag), None)
if existing and not existing['draft']:
    raise SystemExit('Release already published: ' + existing['html_url'])
release = existing or request(base + '/releases', 'POST', {
    'tag_name': tag, 'target_commitish': commit, 'name': 'Niven Ringworld Expedition v'+version,
    'body': release_body(root / 'RELEASE-NOTES.md', version),
    'draft': True, 'prerelease': False})
for path in (archive, checksum, root / f'distribution/NivenRingworld-{version}.ckan', root / 'distribution/NivenRingworld.netkan'):
    if any(asset['name'] == path.name for asset in release['assets']):
        raise SystemExit('Draft asset already present; verify it before retrying: ' + path.name)
    request(release['upload_url'].split('{')[0] + '?name=' + urllib.parse.quote(path.name),
            'POST', path.read_bytes(), binary=True)
published = request(base + '/releases/' + str(release['id']), 'PATCH', {'draft': False, 'make_latest': 'true'})
print(published['html_url'])
