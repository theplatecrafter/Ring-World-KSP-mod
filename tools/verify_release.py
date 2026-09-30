"""Check the distributable, rather than trusting the build directory."""
import hashlib, json, pathlib, zipfile
from release_notes import release_body
root=pathlib.Path(__file__).resolve().parents[1]
release=json.loads((root/'GameData/NivenRingworld/NivenRingworld.version').read_text(encoding='utf-8-sig'))['VERSION']
version_text='.'.join(str(release[k]) for k in ('MAJOR','MINOR','PATCH'))
archive=root/f'artifacts/NivenRingworld-{version_text}.zip'
for name in ('gear','guidance','residence','scenery','landmarks','game','tracking','reentry','visual-options','science'):
    report=(root/f'artifacts/validation/{name}-smoke.txt').read_text(encoding='utf-8-sig')
    assert '[RingworldSmoke] PASS' in report and '[RingworldSmoke] FAIL' not in report, name+' regression not passed'
    if name=='reentry':
        assert report.count('CAMERA entry and exit blends completed')>=2, 'Camera transition regression missing'
for name in ('installed-issues-smoke','extensions-Base','extensions-Clouds','extensions-Scattering','extensions-Combined'):
    report=(root/f'artifacts/validation/{name}.txt').read_text(encoding='utf-8-sig')
    assert '[RingworldSmoke] PASS' in report and '[RingworldSmoke] FAIL' not in report, name+' regression not passed'
combined=(root/'artifacts/validation/extensions-Combined.txt').read_text(encoding='utf-8-sig')
assert 'WATER sustained 1500-tick' in combined, 'Sustained flotation regression missing'
assert 'WATER non-ring' in combined, 'Non-ring dispatch regression missing'
with zipfile.ZipFile(archive) as package:
    assert package.testzip() is None,'ZIP CRC failure'
    names={n.replace('\\','/'):n for n in package.namelist()}
    for required in ('GameData/NivenRingworld/Plugins/NivenRingworld.dll','GameData/NivenRingworld/Plugins/Ringworld.Core.dll','GameData/NivenRingworld/Assets/ringworldscenery','GameData/NivenRingworld/Landmarks.cfg','docs/developers/ASSET-AUTHORING.md','docs/reference/LANDMARK-INVENTORY.md','RELEASE-NOTES.md','docs/README.md','docs/reference/COLOSSI-AND-FORESTS.md','GameData/NivenRingworld/Colossi.cfg','LICENSE'):
        assert required in names,required
    release_body(root/'RELEASE-NOTES.md', version_text)
    for document in [root/'RELEASE-NOTES.md', *sorted((root/'docs').rglob('*'))]:
        if document.is_file():
            key=document.relative_to(root).as_posix()
            assert key in names and package.read(names[key])==document.read_bytes(), 'Documentation mismatch: '+key
    assert not any(n.startswith(('GameData/000_Harmony/','GameData/Cyla/','ThirdParty/')) for n in names), 'Bundled dependency in release'
    assert {n.split('/')[1] for n in names if n.startswith('GameData/')} == {'NivenRingworld'}
    for item in ('Assets/ringworldvisuals','Expeditions.cfg','NivenRingworld.version'):
        assert 'GameData/NivenRingworld/'+item in names
    for path in (root/'GameData/NivenRingworld').rglob('*'):
        if path.is_file():
            key='GameData/NivenRingworld/'+path.relative_to(root/'GameData/NivenRingworld').as_posix()
            assert package.read(names[key])==path.read_bytes(), 'Source/package mismatch: '+key
    for dll_name in ('NivenRingworld.dll','Ringworld.Core.dll'):
        assert package.read(names['GameData/NivenRingworld/Plugins/'+dll_name])==(root/'src/Ringworld.KSP/bin/Release/net472'/dll_name).read_bytes()
    multi=(root/'artifacts/validation/multi-ring-smoke.txt').read_text(encoding='utf-8-sig')
    assert '[RingworldSmoke] PASS multiple rings' in multi and '[RingworldSmoke] FAIL' not in multi
    assert 'PASS tracking encounter' in multi
    absent=(root/'artifacts/validation/multi-ring-without-cyla.txt').read_text(encoding='utf-8-sig')
    assert 'Optional Cyla absent: requested backend safely falls back to Original' in absent
    assert '[RingworldSmoke] PASS multiple rings' in absent and '[RingworldSmoke] FAIL' not in absent
    version=json.loads(package.read(names['GameData/NivenRingworld/NivenRingworld.version']))
    assert version['VERSION']==release
    if tuple(release[k] for k in ('MAJOR','MINOR','PATCH')) >= (1,0,3):
        assert 'GameData/NivenRingworld/LICENSE' in names
        assert 'docs/guides/QUALITY-PRESETS.md' in names and 'docs/publishing/CKAN-PUBLISHING.md' in names
        quality_report=(root/'artifacts/validation/landmarks-smoke.txt').read_text(encoding='utf-8-sig')
        assert 'QUALITY all 11 presets roundtrip' in quality_report
        assert 'QUALITY Economy near vertices=' in quality_report

    dll=package.read(names['GameData/NivenRingworld/Plugins/NivenRingworld.dll'])
    for forbidden in (b'GearSmoke',b'StabilitySmoke',b'LandmarkSmoke',b'GuidanceSmoke',b'SmokeTest',b'StabilityExplosionTrace',b'MultiRingSmoke',b'ResearchSmoke',b'CylaBlackSkySmoke'):
        assert forbidden not in dll,'Test harness in release DLL: '+repr(forbidden)
    for n in names:
        assert not any(s in n.lower() for s in ('assembly-csharp','unityengine','.analysis/','template_instance/','.blend','persistent.sfs')),'Non-distributable file: '+n
    registry=package.read(names['GameData/NivenRingworld/Scenery.cfg']).decode()
    assert registry.count('RINGWORLD_BUNDLED_SCENERY_ASSET')==92,'Incomplete scenery registry'
    assert package.read(names['GameData/NivenRingworld/Landmarks.cfg']).count(b'RINGWORLD_LANDMARK_ASSET')==25
    assert package.read(names['GameData/NivenRingworld/Colossi.cfg']).count(b'RINGWORLD_COLOSSUS_ASSET')==8
digest=hashlib.sha256(archive.read_bytes()).hexdigest()
(archive.with_suffix('.zip.sha256')).write_text(digest+'  '+archive.name+'\n')
report=f'PASS: v{version_text} package CRC, required files, 92-prefab registry, 25 landmarks + 8 rare templates, recorded regression gates, dependency-free package matches current source/assets and compiled DLLs; no harness/game assemblies.\nSHA256 {digest}\n'
(root/'artifacts/validation/release-check.txt').write_text(report)
print(report)
