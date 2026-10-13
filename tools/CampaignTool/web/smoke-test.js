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
    await page.evaluate((p) => window.__editor.openFile(p, true), path);
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
  await page.selectOption('#simScenario', { index: 1 });
  await page.click('#simRun');
  await page.waitForSelector('#simOut table', { timeout: 20000 });
  assert(await page.$$eval('#simOut table tr', ns => ns.length) > 100, 'the simulation lists what happened');
  await shot('editor-simulate');

  await page.click('[data-tab=graph]');
  await page.waitForSelector('svg.graph .node', { timeout: 10000 });
  assert(await page.$$eval('svg.graph .node', ns => ns.length) === 59, 'the graph has a node for every beat');
  await shot('editor-graph');

  await page.click('[data-tab=ref]');
  await page.fill('#refSearch', 'brigade');
  assert(await page.$$eval('#refList .refitem', ns => ns.length) > 5, 'the reference finds the brigade words');
  await shot('editor-reference');

  assert(errors.length === 0, 'the page reported no errors' + (errors.length ? ': ' + errors.join('; ') : ''));
  await browser.close();
})().catch(e => { console.error('FAIL', e); process.exit(1); });
