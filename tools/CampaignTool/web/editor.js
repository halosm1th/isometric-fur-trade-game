'use strict';
// The campaign editor: a text area with a highlighted copy behind it, checked
// by the real compiler on every pause in typing. No libraries, nothing to build.

const $ = (id) => document.getElementById(id);
const el = { input: $('input'), hl: $('highlight'), gutter: $('gutter'), files: $('files'), status: $('status'), fileName: $('fileName'),
  dirty: $('dirty'), banner: $('banner'), hover: $('hover'), complete: $('complete'), count: $('problemCount') };

const state = { lib: null, files: [], path: null, saveAs: null, clean: '', check: null, scenarios: [], sim: null, graph: null, tab: 'problems', completing: null };
let checkTimer = 0, checkSeq = 0, charW = 8, lineH = 20;

const store = { get(k) { try { return localStorage.getItem(k); } catch { return null; } }, set(k, v) { try { localStorage.setItem(k, v); } catch { } } };

async function api(route, opts) {
  const r = await fetch('/api/' + route, opts);
  const j = await r.json();
  if (!r.ok) throw new Error(j.error || r.statusText);
  return j;
}
const post = (route, body) => api(route, { method: 'POST', headers: { 'Content-Type': 'text/plain' }, body: typeof body === 'string' ? body : JSON.stringify(body) });
const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
function setStatus(text, kind) { el.status.textContent = text; el.status.className = 'status' + (kind ? ' ' + kind : ''); }

// ------------------------------------------------------------------ highlighting

const TOKEN = /\/\/[^\n]*|\/\*[\s\S]*?(?:\*\/|$)|"(?:[^"\\\n]|\\.)*"?|\d+(?:\.\d+)?|[A-Za-z_]\w*|=>|==|!=|<=|>=|&&|\|\||\+\+|--|\+=|-=|\.\.|[^\s\w]/g;
let vocab = { keywords: new Set(), types: new Set(), hooks: new Set(), globals: new Set(), objects: new Set(), enums: new Set(), enumValues: {} };

function buildVocab() {
  const L = state.lib;
  vocab = {
    keywords: new Set(L.keywords), types: new Set(L.types), hooks: new Set(L.hooks.map(h => h.name)),
    globals: new Set(L.members.filter(m => m.owner === '' && m.kind === 'function').map(m => m.name)),
    objects: new Set(L.objects), enums: new Set(Object.keys(L.enums)), enumValues: L.enums,
  };
}

function declared(src) {
  const beats = new Set(), fns = new Set(), names = new Set();
  for (const m of src.matchAll(/\bbeat\s+([A-Za-z_]\w*)/g)) beats.add(m[1]);
  for (const m of src.matchAll(/\b(?:void|bool|int|float|string|Date|Days|Company|Person|Item|Season|Kind|Beat|Brigade)\s+([A-Za-z_]\w*)\s*\(/g)) fns.add(m[1]);
  for (const m of src.matchAll(/\b(?:const\s+)?(?:int|float|string|bool|Date|Days|Company|Person|Item|Season|Kind|Beat|Brigade)\s+([A-Za-z_]\w*)\s*(?:=|;)/g)) names.add(m[1]);
  return { beats, fns, names };
}

function tokenize(src) {
  const toks = [], d = declared(src);
  let prev = null, prev2 = null;
  TOKEN.lastIndex = 0;
  let m;
  while ((m = TOKEN.exec(src))) {
    const t = m[0], s = m.index, e = s + t.length;
    let cls = null;
    const c = t[0];
    if (t.startsWith('//') || t.startsWith('/*')) cls = 'cmt';
    else if (c === '"') cls = 'str';
    else if (c >= '0' && c <= '9') cls = 'num';
    else if (/[A-Za-z_]/.test(c)) {
      let k = e; while (k < src.length && (src[k] === ' ' || src[k] === '\t')) k++;
      const next = src[k] || '';
      const p = prev && prev.t, pp = prev2 && prev2.t;
      if (p === 'beat') cls = 'beat';
      else if (vocab.keywords.has(t)) cls = 'kw';
      else if (vocab.hooks.has(t) && next === '(') cls = 'hook';
      else if (p === '.') cls = vocab.enums.has(pp) ? 'enumv' : 'member';
      else if (vocab.enums.has(t) || vocab.types.has(t)) cls = 'type';
      else if (vocab.objects.has(t)) cls = 'obj';
      else if (next === ':' && src[k + 1] !== ':' && (p === '(' || p === ',' || p === '{')) cls = 'arg';
      else if (next === '(') cls = vocab.globals.has(t) ? 'builtin' : (d.fns.has(t) ? 'fn' : null);
      else if (d.beats.has(t)) cls = 'beat';
    } else cls = 'punct';
    toks.push({ s, e, cls });
    prev2 = prev; prev = { t, s };
  }
  return toks;
}

function lineColToOffset(src, line, col) {
  let off = 0;
  for (let i = 1; i < line; i++) { const n = src.indexOf('\n', off); if (n < 0) return src.length; off = n + 1; }
  return Math.min(src.length, off + col - 1);
}

function render() {
  const src = el.input.value;
  const marks = [];
  if (state.check && state.check.error) {
    const er = state.check.error, s = lineColToOffset(src, er.line, er.col);
    marks.push({ s, e: Math.max(s + 1, Math.min(src.length, s + er.length)), cls: 'err' });
  }
  const toks = tokenize(src);
  let out = '', pos = 0;
  const piece = (a, b, cls) => {
    if (b <= a) return;
    let text = src.slice(a, b), inner = esc(text);
    // marks overlapping this piece
    const ov = marks.filter(mk => mk.s < b && mk.e > a);
    if (ov.length) {
      inner = '';
      let p = a;
      for (const mk of ov) {
        const ms = Math.max(mk.s, a), me = Math.min(mk.e, b);
        inner += esc(src.slice(p, ms)) + `<span class="t-${mk.cls}">` + esc(src.slice(ms, me)) + '</span>';
        p = me;
      }
      inner += esc(src.slice(p, b));
    }
    out += cls ? `<span class="t-${cls}">${inner}</span>` : inner;
  };
  for (const t of toks) { piece(pos, t.s, null); piece(t.s, t.e, t.cls); pos = t.e; }
  piece(pos, src.length, null);
  el.hl.innerHTML = out + '\n ';
  const lines = src.split('\n').length;
  const bad = new Set(), warn = new Set();
  if (state.check) {
    if (state.check.error) bad.add(state.check.error.line);
    for (const p of state.check.problems || []) (p.severity === 'error' ? bad : p.severity === 'warning' ? warn : new Set()).add(p.line);
  }
  let g = '';
  for (let i = 1; i <= lines; i++) g += `<div class="${bad.has(i) ? 'has-error' : warn.has(i) ? 'has-warn' : ''}">${i}</div>`;
  el.gutter.innerHTML = g;
  syncScroll();
}

function syncScroll() {
  el.hl.scrollTop = el.input.scrollTop; el.hl.scrollLeft = el.input.scrollLeft;
  el.gutter.scrollTop = el.input.scrollTop;
}

function measure() {
  const probe = document.createElement('span');
  probe.style.cssText = 'position:absolute;visibility:hidden;white-space:pre;font:13px/20px ' + getComputedStyle(el.input).fontFamily;
  probe.textContent = 'M'.repeat(100);
  document.body.appendChild(probe);
  charW = probe.getBoundingClientRect().width / 100;
  document.body.removeChild(probe);
}

// ------------------------------------------------------------------ checking

function scheduleCheck() { clearTimeout(checkTimer); setStatus('…'); checkTimer = setTimeout(runCheck, 350); }

async function runCheck() {
  const seq = ++checkSeq, src = el.input.value;
  try {
    const res = await post('check', src);
    if (seq !== checkSeq) return;
    state.check = res;
    const errors = (res.error ? 1 : 0) + (res.problems || []).filter(p => p.severity === 'error').length;
    const warns = (res.problems || []).filter(p => p.severity === 'warning').length;
    setStatus(errors ? `${errors} error${errors > 1 ? 's' : ''}` : warns ? `${res.beats} beats, ${warns} warning${warns > 1 ? 's' : ''}` : `${res.beats} beats, clean`, errors ? 'bad' : 'ok');
    el.count.textContent = errors || warns || '';
    el.count.className = 'count' + (errors ? ' bad' : warns ? ' warn' : '');
    renderProblems(); renderJson(); render();
  } catch (e) { setStatus(e.message, 'bad'); }
}

function renderProblems() {
  const box = $('tab-problems'), c = state.check;
  if (!c) { box.innerHTML = '<div class="empty">Checking…</div>'; return; }
  const notes = (c.problems || []).filter(p => p.severity === 'note').length;
  let h = notes ? `<label class="notes"><input type="checkbox" id="showNotes" ${state.showNotes ? 'checked' : ''}> show ${notes} note${notes > 1 ? 's' : ''} (flags that nothing reads, and the like)</label>` : '';
  if (c.error) h += `<div class="problem" data-line="${c.error.line}" data-col="${c.error.col}"><span class="sev error">error</span><div class="msg">${esc(c.error.message)}<div class="at">line ${c.error.line}, column ${c.error.col}</div></div></div>`;
  for (const p of c.problems || []) if (p.severity !== 'note' || state.showNotes) h += `<div class="problem" data-line="${p.line}"><span class="sev ${p.severity}">${p.severity}</span><div class="msg">${esc(p.message)}<div class="at">${esc(p.where)}</div></div></div>`;
  const shown = (c.error ? 1 : 0) + (c.problems || []).filter(p => p.severity !== 'note' || state.showNotes).length;
  box.innerHTML = h + (shown ? '' : '<div class="empty">No problems. The script compiles and the checker has nothing to say.</div>');
  const sn = $('showNotes'); if (sn) sn.onchange = () => { state.showNotes = sn.checked; renderProblems(); };
  box.querySelectorAll('.problem').forEach(n => n.onclick = () => jump(+n.dataset.line, +(n.dataset.col || 1)));
}

function renderJson() {
  const c = state.check;
  $('tab-json').innerHTML = c && c.json ? `<div class="row"><button id="copyJson">Copy</button><span class="status">what the game reads</span></div><pre class="json">${esc(c.json)}</pre>` : '<div class="empty">The JSON appears here once the script compiles.</div>';
  const b = $('copyJson'); if (b) b.onclick = () => navigator.clipboard && navigator.clipboard.writeText(c.json).then(() => setStatus('copied', 'ok'));
}

function jump(line, col) {
  const src = el.input.value, off = lineColToOffset(src, line, col || 1);
  el.input.focus(); el.input.setSelectionRange(off, off);
  el.input.scrollTop = Math.max(0, (line - 4) * lineH);
  syncScroll();
}

// ------------------------------------------------------------------ files

async function loadFiles() {
  const j = await api('files');
  state.files = j.files;
  state.scenarios = j.files.filter(f => f.kind === 'scenario');
  const groups = [['source', 'Campaigns'], ['example', 'Examples'], ['json', 'Shipped as JSON']];
  let h = '';
  for (const [kind, title] of groups) {
    const list = state.files.filter(f => f.kind === kind);
    if (!list.length) continue;
    h += `<h4>${title}</h4>`;
    for (const f of list) h += `<a data-path="${esc(f.path)}" class="${f.path === state.path ? 'current' : ''}" title="${esc(f.path)}">${esc(f.name)}${f.kind === 'source' && f.installed ? '<span class="tag">installed</span>' : ''}</a>`;
  }
  el.files.innerHTML = h;
  el.files.querySelectorAll('a').forEach(a => a.onclick = () => openFile(a.dataset.path));
}

function setDirty() {
  const d = el.input.value !== state.clean;
  el.dirty.hidden = !d;
}

async function openFile(path, force) {
  if (!force && el.input.value !== state.clean && !confirm('Discard unsaved changes?')) return;
  const j = await api('file?path=' + encodeURIComponent(path));
  state.path = j.converted ? j.saveAs : path;
  state.saveAs = j.converted ? j.saveAs : null;
  el.input.value = j.text;
  state.clean = j.converted ? '' : j.text;
  el.fileName.textContent = state.path;
  el.banner.hidden = !j.converted;
  if (j.converted) el.banner.textContent = `${path} was written as JSON. This is the same campaign in the script language; saving writes ${j.saveAs}, which the editor then builds the JSON from.`;
  store.set('lastFile', state.path);
  state.sim = null; state.graph = null;
  await loadFiles();
  setDirty(); render(); runCheck();
  if (state.tab === 'graph') showTab('graph');
}

async function save() {
  if (!state.path || !/^tools\/CampaignTool\/(src|examples)\//.test(state.path)) { return saveAs(); }
  try {
    await api('file?path=' + encodeURIComponent(state.path), { method: 'PUT', body: el.input.value });
    state.clean = el.input.value; state.saveAs = null; el.banner.hidden = true;
    setDirty(); setStatus('saved', 'ok'); loadFiles();
  } catch (e) { setStatus(e.message, 'bad'); }
}

async function saveAs() {
  const name = prompt('Name the campaign file (letters, digits, - and _):', 'my-campaign');
  if (!name) return;
  state.path = `tools/CampaignTool/src/${name.replace(/\.camp$/, '')}.camp`;
  el.fileName.textContent = state.path;
  await save();
}

const TEMPLATE = `// A new campaign. Beats are things that may happen; this one happens on a date.
campaign MyCampaign(
    title: "My campaign",
    description: "What it is about."
);

beat TheFirstThing(
    title: "The first thing that happens",
    date: date(1812, 6, 1)
) {
    OnActivate() {
        say("Something has happened.", Company.HBC);
    }
}
`;

function newFile() {
  if (el.input.value !== state.clean && !confirm('Discard unsaved changes?')) return;
  el.input.value = TEMPLATE; state.clean = ''; state.path = null; state.saveAs = null; el.banner.hidden = true;
  el.fileName.textContent = 'new campaign (not saved)';
  setDirty(); render(); runCheck();
}

async function install() {
  try {
    if (el.input.value !== state.clean) await save();
    const base = (state.path || 'campaign').split('/').pop().replace(/\.camp$/, '');
    const r = await post('install', { name: base, source: el.input.value });
    setStatus('installed ' + r.installed, 'ok'); loadFiles();
  } catch (e) { setStatus(e.message, 'bad'); }
}

// ------------------------------------------------------------------ autocomplete and hover

function wordBefore(src, pos) {
  let s = pos; while (s > 0 && /\w/.test(src[s - 1])) s--;
  return { start: s, word: src.slice(s, pos) };
}

function memberKey(type) { return type; }

function completionsAt(src, pos, force) {
  const L = state.lib, { start, word } = wordBefore(src, pos);
  const before = src.slice(0, start);
  const out = [];
  const add = (name, kind, detail, doc, insert) => out.push({ name, kind, detail, doc, insert: insert || name });
  const dot = /([A-Za-z_]\w*|\))\s*\.\s*$/.exec(before);
  if (dot) {
    let owner = null, target = dot[1];
    if (target === ')') {
      const callee = /([A-Za-z_]\w*)\s*\((?:[^()]|\([^()]*\))*\)\s*\.\s*$/.exec(before);
      if (callee && callee[1] === 'brigade') owner = 'Brigade';
    } else if (L.objects.includes(target)) owner = target;
    else if (L.enums[target]) {
      for (const v of L.enums[target]) add(v, 'value', target, L.enumDocs[target] || '');
      return { start, word, items: filt(out, word) };
    } else if (declared(src).beats.has(target)) owner = 'Beat';
    if (!owner) return null;
    for (const m of L.members.filter(m => m.owner === owner)) add(m.name, m.kind === 'function' ? 'method' : 'property', m.signature, m.doc, m.kind === 'function' ? m.name + '(' : m.name);
    return { start, word, items: filt(out, word) };
  }
  if (!word && !force) return null;
  if (word && /^\d/.test(word)) return null;
  if (lineHasOpenQuote(before)) return null;
  // inside a beat's braces the hooks are offered; at the top level the declarations
  const depth = braceDepth(before);
  if (depth === 1 && insideBeat(before)) for (const h of L.hooks) add(h.name, 'hook', h.condition ? 'condition' : 'effects', h.summary, h.name + (h.condition ? '() => ' : '() {\n        \n    }'));
  if (depth === 0) { add('beat', 'keyword', 'declaration', 'A thing that may happen to the world.', 'beat '); add('const', 'keyword', 'declaration', 'A name for a value known when the script is compiled.', 'const '); add('foreach', 'keyword', 'loop', 'Write a declaration for each value.', 'foreach (int i in 1..3) '); }
  if (depth >= 1) for (const k of ['if', 'else', 'for', 'foreach']) add(k, 'keyword', 'statement', '');
  for (const m of L.members.filter(m => m.owner === '' && m.kind === 'function')) add(m.name, 'function', m.signature, m.doc, m.name + '(');
  for (const o of L.objects) add(o, 'object', 'object', o === 'world' ? 'The state of the world.' : 'Named switches.', o);
  for (const e of Object.keys(L.enums)) add(e, 'type', 'values', L.enumDocs[e] || '', e + '.');
  for (const t of L.types) add(t, 'type', 'type', '', t + ' ');
  const d = declared(src);
  for (const b of d.beats) add(b, 'beat', 'beat', '');
  for (const f of d.fns) add(f, 'function', 'your function', '', f + '(');
  for (const n of d.names) add(n, 'name', 'yours', '');
  for (const a of L.beatArgs.concat(L.headerArgs)) if (depth === 0 && insideParens(before)) add(a.name + ':', 'argument', a.type, a.summary, a.name + ': ');
  for (const k of ['true', 'false']) add(k, 'keyword', '', '');
  return { start, word, items: filt(out, word) };
}

function lineHasOpenQuote(before) {
  const line = before.slice(before.lastIndexOf('\n') + 1);
  let open = false;
  for (let i = 0; i < line.length; i++) { if (line[i] === '\\') { i++; continue; } if (line[i] === '"') open = !open; }
  return open;
}
function braceDepth(before) {
  let d = 0, i = 0;
  while (i < before.length) {
    const c = before[i];
    if (c === '/' && before[i + 1] === '/') { while (i < before.length && before[i] !== '\n') i++; continue; }
    if (c === '/' && before[i + 1] === '*') { const e = before.indexOf('*/', i + 2); i = e < 0 ? before.length : e + 2; continue; }
    if (c === '"') { i++; while (i < before.length && before[i] !== '"' && before[i] !== '\n') { if (before[i] === '\\') i++; i++; } i++; continue; }
    if (c === '{') d++; else if (c === '}') d--;
    i++;
  }
  return d;
}
function insideBeat(before) { const k = before.lastIndexOf('beat '); return k >= 0 && before.indexOf('{', k) >= 0; }
function insideParens(before) {
  let d = 0;
  const lastBeat = Math.max(before.lastIndexOf('\nbeat '), before.lastIndexOf('campaign '));
  for (let i = Math.max(lastBeat, 0); i < before.length; i++) { if (before[i] === '(') d++; else if (before[i] === ')') d--; }
  return d > 0;
}

function filt(items, word) {
  const w = word.toLowerCase();
  const seen = new Set();
  return items.filter(i => { const k = i.kind + i.name; if (seen.has(k)) return false; seen.add(k); return !w || i.name.toLowerCase().startsWith(w); })
    .sort((a, b) => (a.name.toLowerCase().startsWith(w) ? 0 : 1) - (b.name.toLowerCase().startsWith(w) ? 0 : 1) || a.name.localeCompare(b.name)).slice(0, 40);
}

function caretXY() {
  const src = el.input.value, pos = el.input.selectionStart, before = src.slice(0, pos);
  const line = before.split('\n').length - 1, col = pos - (before.lastIndexOf('\n') + 1);
  return { x: 12 + col * charW - el.input.scrollLeft, y: 12 + (line + 1) * lineH - el.input.scrollTop };
}

function showComplete(force) {
  const r = completionsAt(el.input.value, el.input.selectionStart, force);
  if (!r || !r.items.length || (r.items.length === 1 && r.items[0].name === r.word)) { hideComplete(); return; }
  state.completing = { ...r, sel: 0 };
  drawComplete();
  const { x, y } = caretXY();
  el.complete.style.left = Math.max(4, x - r.word.length * charW) + 'px';
  el.complete.hidden = false;
  const room = el.complete.parentElement.clientHeight - y, h = el.complete.offsetHeight;
  el.complete.style.top = (room < h + 8 ? Math.max(4, y - lineH - h - 2) : y + 2) + 'px';
}
function drawComplete() {
  const c = state.completing;
  el.complete.innerHTML = c.items.map((i, n) => `<li data-n="${n}" class="${n === c.sel ? 'sel' : ''}"><span class="k">${esc(i.kind)}</span><span class="n">${esc(i.name)}</span><span class="d">${esc(i.detail || '')}</span></li>`).join('');
  el.complete.querySelectorAll('li').forEach(li => { li.onmousedown = (ev) => { ev.preventDefault(); c.sel = +li.dataset.n; acceptComplete(); }; });
  const sel = el.complete.querySelector('.sel'); if (sel) sel.scrollIntoView({ block: 'nearest' });
  const it = c.items[c.sel];
  showHover(it && (it.doc || it.detail) ? { sig: it.detail, doc: it.doc } : null, true);
}
function hideComplete() { state.completing = null; el.complete.hidden = true; if (!el.hover.dataset.mouse) el.hover.hidden = true; }
function acceptComplete() {
  const c = state.completing; if (!c) return;
  const it = c.items[c.sel], src = el.input.value, pos = el.input.selectionStart;
  el.input.value = src.slice(0, c.start) + it.insert + src.slice(pos);
  const np = c.start + it.insert.length;
  el.input.setSelectionRange(np, np);
  hideComplete(); onEdit();
}

function showHover(info, nearCaret, x, y) {
  if (!info) { el.hover.hidden = true; delete el.hover.dataset.mouse; return; }
  el.hover.innerHTML = (info.sig ? `<div class="sig">${esc(info.sig)}</div>` : '') + (info.doc ? `<div>${esc(info.doc)}</div>` : '') + (info.example ? `<div class="ex">${esc(info.example)}</div>` : '');
  el.hover.hidden = false;
  if (nearCaret) {
    const p = caretXY(), room = el.hover.parentElement.clientHeight - p.y;
    el.hover.style.left = Math.max(4, Math.min(el.complete.offsetLeft + el.complete.offsetWidth + 8, el.hover.parentElement.clientWidth - 470)) + 'px';
    el.hover.style.top = (room < el.complete.offsetHeight + 8 ? Math.max(4, p.y - lineH - el.hover.offsetHeight - 2) : p.y + 2) + 'px';
    delete el.hover.dataset.mouse;
  }
  else { el.hover.style.left = x + 'px'; el.hover.style.top = y + 'px'; el.hover.dataset.mouse = '1'; }
}

function wordInfo(src, off) {
  const L = state.lib;
  let s = off, e = off;
  while (s > 0 && /\w/.test(src[s - 1])) s--;
  while (e < src.length && /\w/.test(src[e])) e++;
  const w = src.slice(s, e); if (!w) return null;
  const prevDot = /([A-Za-z_]\w*)\s*\.\s*$/.exec(src.slice(0, s));
  const mem = L.members.filter(m => m.name === w);
  if (prevDot) {
    const owner = L.objects.includes(prevDot[1]) ? prevDot[1] : null;
    const pick = (owner && mem.find(m => m.owner === owner)) || mem.find(m => m.owner === 'Brigade' || m.owner === 'Beat');
    if (pick) return { sig: pick.signature, doc: pick.doc, example: pick.example };
    if (L.enums[prevDot[1]]) return { sig: prevDot[1] + '.' + w, doc: L.enumDocs[prevDot[1]] };
  }
  const g = mem.find(m => m.owner === '');
  if (g) return { sig: g.signature, doc: g.doc, example: g.example };
  const hook = L.hooks.find(h => h.name === w); if (hook) return { sig: hook.name + '()', doc: hook.summary };
  if (L.enums[w]) return { sig: w, doc: L.enumDocs[w] };
  const arg = L.beatArgs.find(a => a.name === w) || L.headerArgs.find(a => a.name === w); if (arg && src[e] === ':') return { sig: `${arg.name}: ${arg.type}`, doc: arg.summary };
  if (state.check && state.check.error) {
    const er = state.check.error, o = lineColToOffset(src, er.line, er.col);
    if (off >= o && off <= o + er.length) return { sig: 'error', doc: er.message };
  }
  return null;
}

// ------------------------------------------------------------------ editing

function onEdit() { setDirty(); render(); scheduleCheck(); }

el.input.addEventListener('input', () => { onEdit(); if (!el.input.dataset.noComplete) showComplete(false); });
el.input.addEventListener('scroll', () => { syncScroll(); hideComplete(); });
el.input.addEventListener('click', hideComplete);
el.input.addEventListener('blur', () => setTimeout(hideComplete, 150));
el.input.addEventListener('keydown', (ev) => {
  const c = state.completing;
  if (c) {
    if (ev.key === 'ArrowDown') { c.sel = (c.sel + 1) % c.items.length; drawComplete(); ev.preventDefault(); return; }
    if (ev.key === 'ArrowUp') { c.sel = (c.sel + c.items.length - 1) % c.items.length; drawComplete(); ev.preventDefault(); return; }
    if (ev.key === 'Tab' || ev.key === 'Enter') { acceptComplete(); ev.preventDefault(); return; }
    if (ev.key === 'Escape') { hideComplete(); ev.preventDefault(); return; }
  }
  if (ev.key === ' ' && ev.ctrlKey) { showComplete(true); ev.preventDefault(); return; }
  if ((ev.key === 's' || ev.key === 'S') && (ev.ctrlKey || ev.metaKey)) { ev.preventDefault(); save(); return; }
  if (ev.key === 'Enter' && ev.ctrlKey) { ev.preventDefault(); runCheck(); return; }
  if (ev.key === 'Tab') {
    ev.preventDefault();
    const s = el.input.selectionStart, e = el.input.selectionEnd, v = el.input.value;
    document.execCommand ? document.execCommand('insertText', false, '    ') : (el.input.value = v.slice(0, s) + '    ' + v.slice(e));
    return;
  }
  if (ev.key === 'Enter') {
    // keep the indentation of the line, and one more after an opening brace
    const s = el.input.selectionStart, v = el.input.value, ls = v.lastIndexOf('\n', s - 1) + 1, line = v.slice(ls, s);
    const indent = /^\s*/.exec(line)[0] + (/[{(\[]\s*$/.test(line) ? '    ' : '');
    ev.preventDefault();
    document.execCommand('insertText', false, '\n' + indent);
  }
});
el.input.addEventListener('mousemove', (ev) => {
  if (state.completing) return;
  const r = el.input.getBoundingClientRect();
  const x = ev.clientX - r.left - 12 + el.input.scrollLeft, y = ev.clientY - r.top - 12 + el.input.scrollTop;
  const line = Math.floor(y / lineH), col = Math.floor(x / charW);
  const lines = el.input.value.split('\n');
  if (line < 0 || line >= lines.length || col < 0 || col > lines[line].length) { showHover(null); return; }
  let off = 0; for (let i = 0; i < line; i++) off += lines[i].length + 1;
  const info = wordInfo(el.input.value, off + col);
  if (info) showHover(info, false, Math.min(r.width - 300, ev.clientX - r.left + 12), ev.clientY - r.top + 18); else showHover(null);
});
el.input.addEventListener('mouseleave', () => { if (!state.completing) showHover(null); });

// ------------------------------------------------------------------ tabs

function showTab(name) {
  state.tab = name;
  document.querySelectorAll('.tabs button').forEach(b => b.classList.toggle('active', b.dataset.tab === name));
  for (const t of ['problems', 'json', 'sim', 'graph', 'ref']) $('tab-' + t).hidden = t !== name;
  if (name === 'sim') renderSim();
  if (name === 'graph') drawGraph();
  if (name === 'ref') renderRef();
}
document.querySelectorAll('.tabs button').forEach(b => b.onclick = () => showTab(b.dataset.tab));

function renderSim() {
  const box = $('tab-sim');
  if (!box.dataset.built) {
    box.dataset.built = '1';
    box.innerHTML = `<div class="row">
      <label class="f">World to play against<select id="simScenario"><option value="">none (an empty world)</option></select></label>
      <label class="f">Adherence<select id="simAdh"><option value="">as scripted</option><option>Gentle</option><option>Steady</option><option>Firm</option><option>Strict</option></select></label>
      <label class="f">Seed<input type="text" id="simSeed" size="6" placeholder="script's"></label>
      <button id="simRun" class="primary">Play it forward</button></div><div id="simOut"></div>`;
    $('simRun').onclick = runSim;
  }
  const sel = $('simScenario'), cur = sel.value;
  sel.innerHTML = '<option value="">none (an empty world)</option>' + state.scenarios.map(s => `<option value="${esc(s.path)}">${esc(s.name.replace('.scenario.json', ''))}</option>`).join('');
  const guess = state.path && state.scenarios.find(s => s.name.startsWith((state.path.split('/').pop() || '').replace('.camp', '')));
  sel.value = cur || (guess ? guess.path : '');
  if (state.sim) drawSim(state.sim);
}

async function runSim() {
  const out = $('simOut'); out.innerHTML = '<div class="empty">Playing it forward…</div>';
  try {
    let scenario = '';
    const sp = $('simScenario').value;
    if (sp) scenario = (await api('file?path=' + encodeURIComponent(sp))).text;
    state.sim = await post('sim', { source: el.input.value, scenario, adherence: $('simAdh').value, seed: $('simSeed').value });
    drawSim(state.sim);
  } catch (e) { out.innerHTML = `<div class="problem"><span class="sev error">error</span><div class="msg">${esc(e.message)}</div></div>`; }
}

function drawSim(r) {
  const lineOf = (id) => state.check && state.check.beatLines && state.check.beatLines[id];
  let h = `<div class="status">${esc(r.start)} to ${esc(r.end)}: ${r.fired.length} things happened</div><table class="t"><tr><th>Date</th><th>Beat</th><th>How</th></tr>`;
  const rows = r.fired.map(f => ({ date: f.date, id: f.id, how: f.how, title: f.title })).concat(r.founded.map(f => ({ date: f.date, id: '', how: 'post founded', title: f.name })));
  rows.sort((a, b) => a.date < b.date ? -1 : a.date > b.date ? 1 : 0);
  for (const f of rows) h += `<tr class="${f.id ? 'jump' : ''}" data-id="${esc(f.id)}"><td class="d">${esc(f.date)}</td><td>${esc(f.title || f.id)}</td><td class="${f.how === 'forced' || f.how === 'triggered' ? 'how-forced' : f.how === 'by the world' ? 'how-world' : ''}">${esc(f.how)}</td></tr>`;
  h += '</table>';
  if (r.history.length) h += `<h5>Already history at the start</h5><div>${r.history.map(esc).join(', ')}</div>`;
  h += `<h5>Never happened</h5><div>${r.never.length ? r.never.map(esc).join(', ') : 'none'}</div>`;
  if (r.log.length) h += `<h5>What they did</h5><table class="t">${r.log.map(l => `<tr><td class="d">${esc(l.date)}</td><td>${esc(l.beat)}</td><td>${esc(l.text)}</td></tr>`).join('')}</table>`;
  const vars = Object.entries(r.vars).map(([k, v]) => `${k} = ${v}`).join(', ');
  if (vars) h += `<h5>Variables at the end</h5><div>${esc(vars)}</div>`;
  $('simOut').innerHTML = h;
  $('simOut').querySelectorAll('tr.jump').forEach(tr => tr.onclick = () => { const ln = lineOf(tr.dataset.id); if (ln) jump(ln, 1); });
}

async function drawGraph() {
  const box = $('tab-graph');
  box.innerHTML = '<div class="empty">Drawing…</div>';
  let g;
  try { g = await post('graph', el.input.value); } catch (e) { box.innerHTML = `<div class="empty">${esc(e.message)}</div>`; return; }
  state.graph = g;
  const ids = new Map(g.nodes.map((n, i) => [n.id, i]));
  const edges = g.edges.filter(e => ids.has(e.from) && ids.has(e.to) && e.from !== e.to);
  const layer = new Array(g.nodes.length).fill(0);
  for (let pass = 0; pass < g.nodes.length; pass++) {
    let moved = false;
    for (const e of edges) { const a = ids.get(e.from), b = ids.get(e.to); if (layer[b] < layer[a] + 1 && layer[a] < g.nodes.length) { layer[b] = layer[a] + 1; moved = true; } }
    if (!moved) break;
  }
  const maxLayer = Math.max(0, ...layer);
  const cols = Array.from({ length: maxLayer + 1 }, () => []);
  g.nodes.forEach((n, i) => cols[layer[i]].push(i));
  const W = 168, H = 30, GX = 54, GY = 12, pos = [];
  cols.forEach((col, ci) => col.forEach((ni, ri) => { pos[ni] = { x: 10 + ci * (W + GX), y: 10 + ri * (H + GY) }; }));
  const width = 20 + (maxLayer + 1) * (W + GX) - GX, height = 20 + Math.max(...cols.map(c => c.length)) * (H + GY);
  let svg = `<svg class="graph" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">`;
  for (const e of edges) {
    const a = pos[ids.get(e.from)], b = pos[ids.get(e.to)], x1 = a.x + W, y1 = a.y + H / 2, x2 = b.x, y2 = b.y + H / 2, mx = (x1 + x2) / 2;
    svg += `<path class="${e.label === 'triggers' ? 'trig' : ''}" d="M${x1},${y1} C${mx},${y1} ${mx},${y2} ${x2},${y2}"><title>${esc(e.from)} ${esc(e.label)} ${esc(e.to)}</title></path>`;
  }
  g.nodes.forEach((n, i) => {
    const p = pos[i], label = n.id.length > 24 ? n.id.slice(0, 23) + '…' : n.id;
    svg += `<g class="node k-${esc(n.kind)}" data-line="${n.line}"><rect x="${p.x}" y="${p.y}" width="${W}" height="${H}" rx="6"/><text x="${p.x + 8}" y="${p.y + 13}">${esc(label)}</text><text x="${p.x + 8}" y="${p.y + 25}" style="fill:var(--dim)">${esc(n.date || n.kind)}</text><title>${esc(n.title)}</title></g>`;
  });
  svg += '</svg>';
  box.innerHTML = `<div class="legend"><span><i style="border-color:var(--builtin)"></i>fixed</span><span><i style="border-color:var(--hook)"></i>event</span><span><i style="border-color:var(--fn)"></i>conditional</span><span><i style="border-color:var(--obj)"></i>pressure</span><span>solid: requires &nbsp; dashed: triggers</span></div>${svg}`;
  box.querySelectorAll('.node').forEach(n => n.onclick = () => jump(+n.dataset.line, 1));
}

function renderRef() {
  const box = $('tab-ref');
  if (!box.dataset.built) {
    box.dataset.built = '1';
    box.innerHTML = '<div class="row"><input type="search" id="refSearch" placeholder="Search the language…" size="28"></div><div id="refList"></div>';
    $('refSearch').oninput = drawRef;
  }
  drawRef();
}
function drawRef() {
  const L = state.lib, q = ($('refSearch').value || '').toLowerCase();
  const hit = (...xs) => !q || xs.some(x => String(x).toLowerCase().includes(q));
  let h = '';
  const sec = (title, items) => { if (items) h += `<h5>${title}</h5>${items}`; };
  const item = (sig, doc, ex, ins) => `<div class="refitem"><div class="sig">${esc(sig)}</div><div class="doc">${esc(doc)}</div>${ex ? `<div class="ex">${esc(ex)}</div>` : ''}${ins ? `<button data-ins="${esc(ins)}">Insert</button>` : ''}</div>`;
  sec('Beat hooks', L.hooks.filter(x => hit(x.name, x.summary)).map(x => item(x.name + '()', x.summary, '', x.name + (x.condition ? '() => ' : '() {\n        \n    }'))).join(''));
  sec('Beat header', L.beatArgs.filter(x => hit(x.name, x.summary)).map(x => item(`${x.name}: ${x.type}`, x.summary)).join(''));
  sec('Campaign header', L.headerArgs.filter(x => hit(x.name, x.summary)).map(x => item(`${x.name}: ${x.type}`, x.summary)).join(''));
  for (const [owner, title] of [['', 'Functions'], ['world', 'world'], ['flags', 'flags'], ['Beat', 'A beat'], ['Brigade', 'A brigade']])
    sec(title, L.members.filter(m => m.owner === owner && hit(m.full || m.name, m.name, m.doc, m.signature)).map(m => item(m.signature, m.doc, m.example, m.example)).join(''));
  sec('Named values', Object.entries(L.enums).filter(([k, v]) => hit(k, L.enumDocs[k], v.join(' '))).map(([k, v]) => item(k, L.enumDocs[k], v.slice(0, 14).map(x => k + '.' + x).join(', ') + (v.length > 14 ? ', …' : ''))).join(''));
  $('refList').innerHTML = h || '<div class="empty">Nothing matches.</div>';
  $('refList').querySelectorAll('button[data-ins]').forEach(b => b.onclick = () => {
    el.input.focus(); document.execCommand('insertText', false, b.dataset.ins); onEdit();
  });
}

// ------------------------------------------------------------------ start

$('btnSave').onclick = save; $('btnInstall').onclick = install; $('btnNew').onclick = newFile;
window.addEventListener('beforeunload', (ev) => { if (el.input.value !== state.clean) { ev.preventDefault(); ev.returnValue = ''; } });

(async function start() {
  measure();
  try {
    state.lib = await api('library'); buildVocab();
    await loadFiles();
    const last = store.get('lastFile');
    const first = state.files.find(f => f.path === last) || state.files.find(f => f.kind === 'source') || state.files[0];
    if (first) await openFile(first.path, true); else newFile();
  } catch (e) { setStatus(e.message, 'bad'); }
  window.__editor = { state, el, openFile, runCheck, showTab, jump };   // for the browser tests
})();
