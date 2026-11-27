// Tekent de Stitch-schermen (docs/ontwerp/stitch/*/code.html) in licht en donker als PNG, om naast Kapture te leggen.
// Gebruik: node tools/stitch_render.js docs/ontwerp/stitch docs/ontwerp/stitch/schermafdrukken
const { chromium } = require('playwright');
const path = require('path'), fs = require('fs');
(async () => {
  const root = process.argv[2], out = process.argv[3];
  const b = await chromium.launch();
  for (const dir of fs.readdirSync(root)) {
    const f = path.join(root, dir, 'code.html');
    if (!fs.existsSync(f)) continue;
    for (const mode of ['licht', 'donker']) {
      const p = await b.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
      try {
        await p.goto('file://' + f, { waitUntil: 'networkidle', timeout: 45000 });
        await p.evaluate(m => { document.documentElement.classList.toggle('dark', m === 'donker'); if (window.lucide) lucide.createIcons(); }, mode);
        await p.waitForTimeout(800);
        await p.screenshot({ path: path.join(out, dir + '-' + mode + '.png'), fullPage: true });
        console.log('ok', dir, mode);
      } catch (e) { console.log('fout', dir, mode, e.message.split('\n')[0]); }
      await p.close();
    }
  }
  await b.close();
})();
