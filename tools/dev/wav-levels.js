// Poziomy nagrania miksera (float32 stereo): szczyt i RMS w oknach 1 s. Użycie: node wav-levels.js plik.wav
const fs = require('fs');
const b = fs.readFileSync(process.argv[2]);
let o = 12, fmt = null, data = null;
while (o + 8 <= b.length) {
  const id = b.toString('ascii', o, o + 4), len = b.readUInt32LE(o + 4);
  if (id === 'fmt ') fmt = { ch: b.readUInt16LE(o + 10), rate: b.readUInt32LE(o + 12), bits: b.readUInt16LE(o + 22) };
  if (id === 'data') { data = b.subarray(o + 8, Math.min(b.length, o + 8 + len)); break; }
  o += 8 + len + (len & 1);
}
const n = Math.floor(data.length / 4), win = fmt.rate * fmt.ch;
let gpeak = 0, clipped = 0;
for (let s = 0, sec = 0; s < n; s += win, sec++) {
  let peak = 0, sum = 0, cnt = 0;
  for (let i = s; i < Math.min(n, s + win); i++) { const v = Math.abs(data.readFloatLE(i * 4)); if (v > peak) peak = v; if (v >= .99) clipped++; sum += v * v; cnt++; }
  gpeak = Math.max(gpeak, peak);
  const db = x => x > 0 ? (20 * Math.log10(x)).toFixed(1) : '-inf';
  console.log(`${String(sec).padStart(4)} s  peak ${db(peak).padStart(6)} dBFS  rms ${db(Math.sqrt(sum / cnt)).padStart(6)} dBFS`);
}
console.log(`szczyt calosci ${(20 * Math.log10(gpeak)).toFixed(1)} dBFS, probek >= 0.99: ${clipped}`);
