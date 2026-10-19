// node gifs.mjs <folderShowcase> <folderWyjściowy> [szerokość] [scena1,scena2,...] – składa klatki z --showcase w GIF-y
import fs from 'node:fs';
import path from 'node:path';
import { PNG } from 'pngjs';
import gifenc from 'gifenc';
const { GIFEncoder, quantize, applyPalette } = gifenc;

const [src, out, widthArg, only] = process.argv.slice(2);
const onlySet = only ? new Set(only.split(',')) : null;
const W = Number(widthArg || 480);
const perFramePalette = new Set(['daycycle', 'tnt', 'title']);
fs.mkdirSync(out, { recursive: true });

function resize(png, w) {
  const h = Math.round(png.height * w / png.width);
  const sx = png.width / w, sy = png.height / h;
  const o = new Uint8Array(w * h * 4);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const x0 = x * sx, x1 = x0 + sx, y0 = y * sy, y1 = y0 + sy;
    let r = 0, g = 0, b = 0, n = 0;
    for (let yy = Math.floor(y0); yy < Math.ceil(y1); yy++) for (let xx = Math.floor(x0); xx < Math.ceil(x1); xx++) {
      const wx = Math.min(xx + 1, x1) - Math.max(xx, x0), wy = Math.min(yy + 1, y1) - Math.max(yy, y0), wt = wx * wy;
      const i = (yy * png.width + xx) * 4;
      r += png.data[i] * wt; g += png.data[i + 1] * wt; b += png.data[i + 2] * wt; n += wt;
    }
    const k = (y * w + x) * 4;
    o[k] = r / n; o[k + 1] = g / n; o[k + 2] = b / n; o[k + 3] = 255;
  }
  return { data: o, width: w, height: h };
}

for (const scene of fs.readdirSync(src).filter(d => fs.statSync(path.join(src, d)).isDirectory() && (!onlySet || onlySet.has(d))).sort()) {
  const dir = path.join(src, scene);
  const files = fs.readdirSync(dir).filter(f => f.endsWith('.png')).sort();
  const frames = files.map(f => resize(PNG.sync.read(fs.readFileSync(path.join(dir, f))), W));
  const { width, height } = frames[0];
  let shared = null;
  if (!perFramePalette.has(scene)) {
    const pick = frames.filter((_, i) => i % 6 === 0);
    const all = new Uint8Array(pick.length * width * height * 4);
    pick.forEach((f, i) => all.set(f.data, i * width * height * 4));
    shared = quantize(all, 256);
  }
  const gif = GIFEncoder();
  frames.forEach((f, i) => {
    const palette = shared || quantize(f.data, 256);
    const index = applyPalette(f.data, palette);
    gif.writeFrame(index, width, height, { palette, delay: 100, repeat: 0 });
  });
  gif.finish();
  const file = path.join(out, scene + '.gif');
  fs.writeFileSync(file, gif.bytes());
  console.log(scene, frames.length, 'klatek', (fs.statSync(file).size / 1048576).toFixed(2), 'MB');
}
