"""Pack unchanged native Aseprite exports; no drawing or frame retouching."""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()

plan = read(sys.argv[1])
root = Path(plan['root'])
results = []
titles = {'idle-baguette': '吃法棍', 'double-click-angry': '双击微怒', 'double-click-dizzy': '双击眩晕'}
for entry in plan['animations']:
    native = read(entry['nativeReport'])
    name = entry['name']
    count, durations = native['frames'], native['durations']
    assert 2 <= count <= 240 and len(durations) == count
    assert native['width'] == 192 and native['height'] == 208
    assert all(isinstance(ms, int) and 20 <= ms <= 5000 for ms in durations)
    total = sum(durations)
    assert 1000 <= total <= (119000 if name == 'idle-baguette' else 30000)
    assert digest(entry['source']) == entry['sourceSha256'] == digest(entry['snapshot'])
    columns = min(8, count)
    frames = [Image.open(Path(entry['split']) / f'{i+1:03}.png').convert('RGBA') for i in range(count)]
    assert all(im.size == (192, 208) for im in frames)
    atlas = Image.new('RGBA', (columns * 192, ((count + columns - 1) // columns) * 208))
    for i, frame in enumerate(frames):
        atlas.paste(frame, ((i % columns) * 192, (i // columns) * 208))
    animation = dict(name=name, row=0, frames=count, loop=False, durations=durations)
    if name == 'double-click-angry':
        metadata = json.loads(native.get('spriteData') or '{}')
        # Current native project metadata is authoritative; no GIF timings.
        animation['speechStartMs'] = metadata.get('speechStartMs', 650)
        animation['speechEndMs'] = metadata.get('speechEndMs', 2500)
        assert 0 <= animation['speechStartMs'] < animation['speechEndMs'] <= total
    merged = root / 'merged' / name
    preview = root / 'preview' / name
    merged.mkdir(parents=True)
    preview.mkdir(parents=True)
    sheet = merged / f'{name}.png'
    manifest = merged / f'{name}.json'
    atlas.save(sheet)
    write(manifest, dict(width=192, height=208, columns=columns, animations=[animation]))
    decoded = Image.open(sheet).convert('RGBA')
    for i, frame in enumerate(frames):
        cell = decoded.crop(((i % columns) * 192, (i // columns) * 208, (i % columns + 1) * 192, (i // columns + 1) * 208))
        assert cell.tobytes() == frame.tobytes(), 'Atlas changed native pixels'
    contact = Image.new('RGB', (288 * columns, 340 * ((count + columns - 1) // columns)), '#e8edf2')
    draw = ImageDraw.Draw(contact)
    urls, changes = [], []
    import base64
    for i, frame in enumerate(frames):
        x, y = (i % columns) * 288, (i // columns) * 340
        draw.text((x + 10, y + 8), f'{i+1} / {count} - {durations[i]} ms', fill='#332b31')
        contact.paste(frame.resize((288, 312), Image.Resampling.NEAREST), (x, y + 28), frame.resize((288, 312), Image.Resampling.NEAREST))
        urls.append('data:image/png;base64,' + base64.b64encode((Path(entry['split']) / f'{i+1:03}.png').read_bytes()).decode())
    contact.save(preview / 'contact-sheet.png')
    # These are previews only; the runtime always uses exact native PNG/ms.
    gif_frames = []
    for frame in frames:
        background = Image.new('RGBA', frame.size, '#e8edf2')
        background.alpha_composite(frame)
        gif_frames.append(background.convert('RGB').resize((384, 416), Image.Resampling.NEAREST))
    gif = preview / f'{name}.gif'
    gif_frames[0].save(gif, save_all=True, append_images=gif_frames[1:], duration=durations, loop=0, optimize=False)
    previous = root.parent / 'src' / 'Assets' / f'{name}.png'
    previous_manifest = previous.with_suffix('.json')
    if previous.exists() and previous_manifest.exists():
        old = Image.open(previous).convert('RGBA')
        old_config = read(previous_manifest)
        old_columns = old_config['columns']
        old_count = old_config['animations'][0]['frames']
        for i, frame in enumerate(frames):
            if i >= old_count:
                changes.append(dict(frame=i+1, pixels='new'))
                continue
            prior = old.crop(((i % old_columns) * 192, (i // old_columns) * 208, (i % old_columns + 1) * 192, (i // old_columns + 1) * 208))
            changed = sum(a != b and (a[3] != b[3] or a[3] > 0) for a, b in zip(prior.getdata(), frame.getdata()))
            if changed:
                changes.append(dict(frame=i+1, pixels=changed))
    html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>Teto 原生工程导出</title>
<style>body{font:16px 'Microsoft YaHei',sans-serif;max-width:1000px;margin:30px auto;background:#f4eee9}canvas{width:384px;height:416px;image-rendering:pixelated;background:#e8edf2}input{width:384px}button{font:inherit;padding:8px;margin:10px}</style>
<h1>__TITLE__ · 当前 Aseprite 工程导出</h1><p>__COUNT__ 帧 · __TOTAL__ 秒 · 保留原始像素与逐帧时长</p>
<canvas id="c" width="192" height="208"></canvas><p id="s"></p><input id="r" type="range" min="0" max="__MAX__"><br><button id="play">播放一次</button><button id="pause">暂停</button><p><a href="contact-sheet.png">全部帧</a></p>
<script>const urls=__URLS__,ds=__DURATIONS__;let ims=[],i=0,run=false,last=0,elapsed=0;const ctx=document.querySelector('#c').getContext('2d'),r=document.querySelector('#r'),s=document.querySelector('#s');
function paint(){ctx.clearRect(0,0,192,208);ctx.drawImage(ims[i],0,0);r.value=i;s.textContent=`第 ${i+1}/${ds.length} 帧 · ${ds[i]} ms`}
function tick(t){if(run&&last){elapsed+=t-last;while(run&&elapsed>=ds[i]){elapsed-=ds[i];if(i===ds.length-1)run=false;else{i++;paint()}}}last=t;requestAnimationFrame(tick)}
Promise.all(urls.map(u=>new Promise(ok=>{const a=new Image;a.onload=()=>ok(a);a.src=u}))).then(a=>{ims=a;paint();requestAnimationFrame(tick)});
document.querySelector('#play').onclick=()=>{i=0;elapsed=0;last=0;run=true;paint()};document.querySelector('#pause').onclick=()=>run=false;r.oninput=()=>{run=false;i=+r.value;paint()};</script></html>'''
    for key, value in {'__TITLE__': titles[name], '__COUNT__': str(count), '__TOTAL__': str(total/1000), '__MAX__': str(count-1), '__URLS__': json.dumps(urls), '__DURATIONS__': json.dumps(durations)}.items():
        html = html.replace(key, value)
    (preview / 'preview.html').write_text(html, encoding='utf-8')
    result = dict(name=name, project=entry['source'], snapshot=entry['snapshot'], sourceSha256=entry['sourceSha256'], frames=count, durationMs=total, durations=durations,
                  layers=native['layers'], split=entry['split'], nativeReport=entry['nativeReport'], sheet=str(sheet), manifest=str(manifest), preview=str(preview / 'preview.html'), gif=str(gif),
                  changes=changes, changedFramesSincePreviousExport=len(changes), sheetMatchesNativeExport=True, projectsUnmodified=digest(entry['source']) == entry['sourceSha256'], sheetSha256=digest(sheet), manifestSha256=digest(manifest))
    if name == 'double-click-angry':
        result.update(speechStartMs=animation['speechStartMs'], speechEndMs=animation['speechEndMs'])
    results.append(result)
    print(json.dumps({k: result[k] for k in ['name', 'frames', 'durationMs', 'changedFramesSincePreviousExport', 'projectsUnmodified']}))
write(root / 'native-animation-export-result.json', dict(version=plan['version'], root=str(root), animations=results))
