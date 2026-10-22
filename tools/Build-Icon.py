"""Package an existing transparent PNG master as a multi-resolution Windows ICO.

This is format conversion/downscaling only; no background removal or retouching.
Usage: python Build-Icon.py INPUT.png OUTPUT.ico
Requires Pillow. The source PNG is never overwritten.
"""
import hashlib
import json
import struct
import sys
from pathlib import Path
from PIL import Image

source, target = map(Path, sys.argv[1:3])
master = Image.open(source)
assert master.mode == 'RGBA', 'The icon master must retain genuine alpha.'
assert master.width == master.height and master.width >= 256
alpha = master.getchannel('A')
assert alpha.getextrema() == (0, 255)
assert all(master.getpixel(p)[3] == 0 for p in (
    (0, 0), (master.width-1, 0), (0, master.height-1),
    (master.width-1, master.height-1)))
sizes = [(n, n) for n in (16, 20, 24, 32, 40, 48, 64, 128, 256)]
target.parent.mkdir(parents=True, exist_ok=True)
master.save(target, format='ICO', sizes=sizes)
data = target.read_bytes()
reserved, kind, count = struct.unpack_from('<HHH', data)
assert (reserved, kind, count) == (0, 1, len(sizes))
decoded = Image.open(target)
assert decoded.ico.sizes() == set(sizes)
for size in sizes:
    tile = decoded.ico.getimage(size).convert('RGBA')
    assert tile.getchannel('A').getextrema() == (0, 255)
print(json.dumps(dict(
    master=str(source), masterSize=master.size, icon=str(target),
    sizes=sizes, alphaPreserved=True, formatConversionOnly=True,
    masterSha256=hashlib.sha256(source.read_bytes()).hexdigest().upper(),
    iconSha256=hashlib.sha256(data).hexdigest().upper())))
