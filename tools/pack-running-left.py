"""Losslessly embed native Aseprite frames in the existing running-left row."""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw

def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def save(path, data):
    Path(path).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()

root = Path(sys.argv[1])
plan = load(root / 'export-plan.json')
entry = plan['animations'][0]
native = load(entry['nativeReport'])
assert digest(entry['source']) == entry['sourceSha256'] == digest(entry['snapshot'])
base = root / 'baseline' / 'Assets'
config = load(base / 'animations.json')
old = Image.open(base / 'teto.png').convert('RGBA')
atlas = old.copy()
target = next(a for a in config['animations'] if a['name'] == 'running-left')
row = target['row']
assert (native['width'], native['height']) == (192, 208)
assert 2 <= native['frames'] <= old.width // 192
assert len(native['durations']) == native['frames'] and all(20 <= d <= 5000 for d in native['durations'])
frames = [Image.open(Path(entry['split']) / f'{i+1:03}.png').convert('RGBA') for i in range(native['frames'])]
changes = []
for i, frame in enumerate(frames):
    assert frame.size == (192, 208)
    box = (i * 192, row * 208, (i+1) * 192, (row+1) * 208)
    prior = old.crop(box)
    changed = sum(a != b and (a[3] != b[3] or a[3] > 0) for a, b in zip(prior.getdata(), frame.getdata()))
    if changed:
        changes.append({'frame': i+1, 'pixels': changed})
    atlas.paste(frame, box[:2])
merged = root / 'merged'
preview = root / 'preview'
merged.mkdir(); preview.mkdir()
sheet = merged / 'teto.png'
manifest = merged / 'animations.json'
atlas.save(sheet)
if target['frames'] == native['frames'] and target['durations'] == native['durations']:
    manifest.write_bytes((base / 'animations.json').read_bytes())
else:
    target['frames'] = native['frames']; target['durations'] = native['durations']
    save(manifest, config)
decoded = Image.open(sheet).convert('RGBA')
for i, frame in enumerate(frames):
    assert decoded.crop((i * 192, row * 208, (i+1) * 192, (row+1) * 208)).tobytes() == frame.tobytes()
for y in range(old.height // 208):
    if y != row:
        box = (0, y * 208, old.width, (y+1) * 208)
        assert decoded.crop(box).tobytes() == old.crop(box).tobytes(), 'Unrequested animation row changed'
small = Image.new('RGBA', (192 * len(frames), 208))
for i, frame in enumerate(frames):
    small.paste(frame, (i * 192, 0))
small.save(merged / 'running-left.png')
save(merged / 'running-left.json', {'width': 192, 'height': 208, 'columns': len(frames), 'animations': [{'name': 'running-left', 'row': 0, 'frames': len(frames), 'loop': True, 'durations': native['durations']}]})
contact = Image.new('RGB', (384 * 4, 450 * ((len(frames)+3)//4)), '#e8edf2')
draw = ImageDraw.Draw(contact)
gif_frames = []
for i, frame in enumerate(frames):
    bg = Image.new('RGBA', frame.size, '#e8edf2'); bg.alpha_composite(frame)
    enlarged = bg.convert('RGB').resize((384, 416), Image.Resampling.NEAREST)
    gif_frames.append(enlarged)
    x, y = (i % 4)*384, (i//4)*450
    draw.text((x+12, y+8), f'{i+1} / {len(frames)} - {native["durations"][i]} ms', fill='#332b31')
    contact.paste(enlarged, (x, y+30))
contact.save(preview / 'contact-sheet.png')
gif = preview / 'running-left.gif'
gif_frames[0].save(gif, save_all=True, append_images=gif_frames[1:], duration=native['durations'], loop=0, optimize=False)
result = dict(version='0.1.17', revision=plan['revision'], name='running-left', project=entry['source'], snapshot=entry['snapshot'], sourceSha256=entry['sourceSha256'],
              frames=len(frames), durations=native['durations'], durationMs=sum(native['durations']), loop=True, row=row, split=entry['split'], nativeReport=entry['nativeReport'],
              sheet=str(sheet), manifest=str(manifest), sheetSha256=digest(sheet), manifestSha256=digest(manifest), preview=str(preview / 'contact-sheet.png'), gif=str(gif),
              changes=changes, changedFrames=len(changes), otherCoreFramesUnchanged=sum(a['frames'] for a in config['animations'] if a['name'] != 'running-left'),
              otherRowsUnchanged=True, nativePixelsPreserved=True, projectsUnmodified=digest(entry['source']) == entry['sourceSha256'], manifestUnchanged=manifest.read_bytes() == (base / 'animations.json').read_bytes())
save(root / 'running-left-export-result.json', result)
print(json.dumps({k: result[k] for k in ('frames','durationMs','changedFrames','otherCoreFramesUnchanged','manifestUnchanged','projectsUnmodified')}))
