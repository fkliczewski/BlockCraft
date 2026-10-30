// Pomocnik edycji: node ed.js plik.json  (lista {p, a, b}); dopasowuje EOL pliku
const fs = require("fs");
const list = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
for (const { p, a, b } of list) {
  let s = fs.readFileSync(p, "utf8");
  const crlf = s.includes("\r\n");
  const A = crlf ? a.replace(/\r?\n/g, "\r\n") : a.replace(/\r\n/g, "\n");
  const B = crlf ? b.replace(/\r?\n/g, "\r\n") : b.replace(/\r\n/g, "\n");
  if (!s.includes(A)) { console.log("BRAK w " + p + ": " + a.slice(0, 70)); continue; }
  s = s.split(A).join(B);
  fs.writeFileSync(p, s);
}
