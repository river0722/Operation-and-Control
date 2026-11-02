
import sys, os, glob
from PIL import Image
import numpy as np
root = sys.argv[1]
for d in sorted(os.listdir(root)):
    files = sorted(glob.glob(os.path.join(root, d, "*.png")))
    gaps = []
    for f in files:
        im = Image.open(f).convert("L")
        a = np.array(im)
        h, w = a.shape
        # ignore footer zone (bottom 8%)
        body = a[:int(h*0.92), :]
        rows = np.where((body < 200).any(axis=1))[0]
        last = rows.max() if len(rows) else 0
        gap = 1 - last/float(body.shape[0])
        gaps.append(round(float(gap), 2))
    print(d, "pages", len(files), "bottom gaps", gaps)
