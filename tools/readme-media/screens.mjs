// node screens.mjs <srcDir> <outDir> [width] – zmniejsza zrzuty screen_*.png
import fs from 'node:fs';
import path from 'node:path';
import { PNG } from 'pngjs';
const [src, out, widthArg] = process.argv.slice(2);
const W = Number(widthArg || 960);
fs.mkdirSync(out, { recursive: true });
for (const f of fs.readdirSync(src).filter(f => /^screen_.*\.png$/.test(f))) {
  const png = PNG.sync.read(fs.readFileSync(path.join(src, f)));
  const h = Math.round(png.height * W / png.width), sx = png.width / W, sy = png.height / h;
  const o = new PNG({ width: W, height: h });
  for (let y = 0; y < h; y++) for (let x = 0; x < W; x++) {
    const x0 = x * sx, x1 = x0 + sx, y0 = y * sy, y1 = y0 + sy; let r = 0, g = 0, b = 0, n = 0;
    for (let yy = Math.floor(y0); yy < Math.ceil(y1); yy++) for (let xx = Math.floor(x0); xx < Math.ceil(x1); xx++) {
      const wt = (Math.min(xx + 1, x1) - Math.max(xx, x0)) * (Math.min(yy + 1, y1) - Math.max(yy, y0)), i = (yy * png.width + xx) * 4;
      r += png.data[i] * wt; g += png.data[i + 1] * wt; b += png.data[i + 2] * wt; n += wt;
    }
    const k = (y * W + x) * 4; o.data[k] = r / n; o.data[k + 1] = g / n; o.data[k + 2] = b / n; o.data[k + 3] = 255;
  }
  const name = f.replace(/^screen_/, '');
  fs.writeFileSync(path.join(out, name), PNG.sync.write(o, { colorType: 2 }));
  console.log(name, (fs.statSync(path.join(out, name)).size / 1024).toFixed(0), 'KB');
}
