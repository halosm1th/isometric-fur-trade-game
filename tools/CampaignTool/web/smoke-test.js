// A browser test for the editor: starts nothing itself, so run the server first.
//
//   dotnet run --project tools/CampaignTool -- serve --port 8799 &
//   NODE_PATH=$(npm root -g) node tools/CampaignTool/web/smoke-test.js http://localhost:8799 [screenshot-dir]
//
// Needs Playwright with a Chromium (the sandbox has one at /opt/pw-browsers).
// It opens the long campaign, breaks a word, types a completion, runs the
// simulator, draws the graph and searches the reference, and fails if the page
// reports any error. With a directory it also saves screenshots.

const { chromium } = require('playwright');
const base = process.argv[2] || 'http://localhost:8765';
const shots = process.argv[3];
const assert = (cond, what) => { if (!cond) { console.error('FAIL:', what); process.exitCode = 1; } else console.log('ok  ', what); };

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1360, height: 820 }, colorScheme: process.env.SCHEME || 'light' });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  const shot = async (name) => { if (shots) await page.screenshot({ path: `${shots}/${name}.png` }); };
  const open = async (path) => {
    // wait for this file's own check, not the one already on screen
    await page.evaluate(async (p) => { await window.__editor.openFile(p, true); window.__editor.state.check = null; await window.__editor.runCheck(); }, path);
    await page.waitForFunction(() => window.__editor.state.check);
  };
  const setText = (from, to) => page.evaluate(([a, b]) => { const t = document.getElementById('input'); t.value = t.value.replace(a, b); t.dispatchEvent(new Event('input')); }, [from, to]);

  await page.goto(base + '/');
  await page.waitForFunction(() => window.__editor && window.__editor.state.check, null, { timeout: 15000 });

  await open('tools/CampaignTool/src/company-years.camp');
  assert(await page.evaluate(() => window.__editor.state.check.beats) === 59, 'the long campaign compiles to 59 beats');
  const classes = await page.$$eval('#highlight span', ns => [...new Set(ns.map(n => n.className))]);
  for (const c of ['t-kw', 't-type', 't-hook', 't-builtin', 't-str', 't-num', 't-cmt', 't-beat', 't-member', 't-enumv']) assert(classes.includes(c), `highlighting produces ${c}`);

  await page.evaluate(() => { const t = document.getElementById('input'); const i = t.value.indexOf('beat SevenOaks'); const line = t.value.slice(0, i).split('\n').length; window.__editor.jump(line, 1); });
  await shot('editor-light');

  await setText('world.setLondon(0.85, 0.85);', 'world.setLondn(0.85, 0.85);');
  await page.waitForFunction(() => window.__editor.state.check.error, null, { timeout: 8000 });
  assert((await page.$$eval('#highlight .t-err', ns => ns.map(n => n.textContent))).join() === 'setLondn', 'the misspelt word is underlined');
  assert((await page.evaluate(() => window.__editor.state.check.error.message)).includes("Did you mean 'setLondon'"), 'the error suggests the right word');
  await page.evaluate(() => window.__editor.jump(window.__editor.state.check.error.line, 1));
  await shot('editor-error');

  await open('tools/CampaignTool/src/company-years.camp');
  await page.evaluate(() => { const t = document.getElementById('input'); const i = t.value.indexOf('flags.set("blockade");'); t.focus(); t.setSelectionRange(i, i); });
  await page.keyboard.type('world.ra');
  await page.waitForSelector('#complete:not([hidden]) li');
  assert((await page.$$eval('#complete li .n', ns => ns.map(n => n.textContent))).includes('raiseTension'), 'completion offers world.raiseTension');
  await page.keyboard.press('Escape');

  await open('tools/CampaignTool/src/company-years.camp');
  await page.click('[data-tab=sim]');
  await page.selectOption('#simScenario', 'tools/CampaignTool/examples/company-years.scenario.json');
  await page.click('#simRun');
  await page.waitForSelector('#simOut table', { timeout: 20000 });
  assert(await page.$$eval('#simOut table tr', ns => ns.length) > 100, 'the simulation lists what happened');
  await shot('editor-simulate');

  await page.click('[data-tab=graph]');
  await page.waitForSelector('svg.graph .node', { timeout: 10000 });
  assert(await page.$$eval('svg.graph .node', ns => ns.length) === 59, 'the graph has a node for every beat');
  await shot('editor-graph');

  await page.click('[data-tab=ref]');
  await page.fill('#tab-ref .refSearch', 'brigade');
  assert(await page.$$eval('#tab-ref .refitem', ns => ns.length) > 5, 'the reference finds the brigade words');
  await shot('editor-reference');

  // Hovering an argument in the reference says what it is (say's to?).
  await page.fill('#tab-ref .refSearch', 'say');
  const sayTo = page.locator('#tab-ref .refitem', { hasText: 'say(string text' }).locator('.sig .p[data-tipname="to"]').first();
  await sayTo.hover();
  await page.waitForSelector('#tip:not([hidden])');
  assert((await page.textContent('#tip')).includes('Who hears it'), "hovering say's `to` explains it");
  assert(await page.$$eval('#tab-ref .refitem .params li', ns => ns.length) > 0, 'each function lists its arguments');
  await page.fill('#tab-ref .refSearch', 'quest');
  assert(await page.$$eval('#tab-ref .refitem', ns => ns.some(n => n.textContent.includes('Quest.start'))), 'the reference has the quest words');

  // In the script, hovering a named argument says what it is.
  await page.evaluate(() => { const t = document.getElementById('input'); t.dataset.noComplete = '1'; t.value = 'campaign T(title: "t");\nbeat B() {\n    OnActivate() { say("x", to: Company.HBC); }\n}\n'; t.dispatchEvent(new Event('input')); delete t.dataset.noComplete; t.scrollTop = 0; t.scrollLeft = 0; });
  const box = await page.locator('#input').boundingBox();
  const cw = await page.evaluate(() => { const s = document.createElement('span'); s.textContent = 'x'.repeat(100); document.getElementById('highlight').appendChild(s); const w = s.getBoundingClientRect().width / 100; s.remove(); return w; });
  const col = '    OnActivate() { say("x", '.length;
  let said = '';
  for (let dx = -1; dx <= 2 && !said.includes('Who hears it'); dx++) {
    await page.mouse.move(box.x + 12 + (col + dx) * cw + cw / 2, box.y + 12 + 2 * 20 + 10);
    said = await page.$eval('#hover', n => n.hidden ? '' : n.textContent);
  }
  assert(said.includes('Who hears it'), 'hovering `to:` in the script explains it');

  // The panes can be dragged wider.
  const before = await page.$eval('.side', n => n.offsetWidth);
  const bar = await page.locator('#splitSide').boundingBox();
  await page.mouse.move(bar.x + 2, bar.y + 200); await page.mouse.down();
  await page.mouse.move(bar.x - 150, bar.y + 200, { steps: 5 }); await page.mouse.up();
  assert(await page.$eval('.side', n => n.offsetWidth) > before + 100, 'dragging the bar widens the results panel');
  const fBefore = await page.$eval('#files', n => n.offsetWidth);
  const fbar = await page.locator('#splitFiles').boundingBox();
  await page.mouse.move(fbar.x + 2, fbar.y + 200); await page.mouse.down();
  await page.mouse.move(fbar.x + 80, fbar.y + 200, { steps: 5 }); await page.mouse.up();
  assert(await page.$eval('#files', n => n.offsetWidth) > fBefore + 50, 'dragging the bar widens the file list');
  await shot('editor-resized');

  // The reference opens in a page of its own.
  const ref = await browser.newPage({ viewport: { width: 1100, height: 820 }, colorScheme: process.env.SCHEME || 'light' });
  ref.on('pageerror', e => errors.push(e.message));
  await ref.goto(base + '/?view=reference');
  await ref.waitForSelector('#refPage .refitem');
  assert(await ref.$$eval('#refPage .refitem', ns => ns.length) > 50, 'the reference page lists the language');
  assert(await ref.$eval('main', n => getComputedStyle(n).display) === 'none', 'the reference page has no editor');
  if (shots) await ref.screenshot({ path: `${shots}/reference-page.png` });

  assert(errors.length === 0, 'the page reported no errors' + (errors.length ? ': ' + errors.join('; ') : ''));
  await browser.close();
})().catch(e => { console.error('FAIL', e); process.exit(1); });
