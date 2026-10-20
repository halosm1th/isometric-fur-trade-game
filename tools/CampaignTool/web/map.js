// The Map tab: the world the campaign plays in (Data/World/world.json, its
// ground, posts and camps), the cast placed in the editor, and every place
// the open campaign's quests and beats name - with the road a quest takes
// drawn between its steps, and a warning for any name that is nowhere.
// Click a pin to jump to its step. Drag to pan, wheel to zoom.
// Uses editor.js's $, api, post, esc, el, state and jump.

const map = {
  world: null, ground: null, pins: [], error: '', built: false,
  view: { scale: 0, ox: 0, oy: 0 }, hover: null, selected: null,
  show: { families: false, effects: true, labels: true, cast: true }, quest: '',
  castAt: new Map(), seq: 0,
};

const COMPANY = { HBC: '#c8443a', NWC: '#3d6fd1', AMER: '#2f9a58', FR: '#8a8f99' };
const COMPANY_NAME = { HBC: "Hudson's Bay Company", NWC: 'North West Company', AMER: 'American', FR: 'French (gone)' };

function questHue(id) { let h = 0; for (const c of id) h = (h * 31 + c.charCodeAt(0)) >>> 0; return h % 360; }
const questColor = (id, a) => `hsla(${questHue(id)}, 75%, 55%, ${a ?? 1})`;

async function drawMap() {
  const box = $('tab-map');
  if (!map.built) {
    map.built = true;
    box.innerHTML = `<div class="row map-controls">
        <select id="mapQuest" title="Show one quest's road"><option value="">All quests</option></select>
        <input type="search" id="mapFind" list="mapPlaces" placeholder="Find a place…" size="16">
        <datalist id="mapPlaces"></datalist>
        <label><input type="checkbox" id="mapEffects" checked> beats &amp; effects</label>
        <label><input type="checkbox" id="mapFamilies"> family camps</label>
        <label><input type="checkbox" id="mapLabels" checked> names</label>
        <button id="mapFit" title="Fit the campaign's places (or the whole map)">Fit</button>
      </div>
      <div id="mapMissing"></div>
      <div class="map-wrap"><canvas id="mapCanvas" tabindex="0" aria-label="World map"></canvas><div id="mapTip" class="map-tip" hidden></div></div>
      <div class="legend map-legend"><span><i class="sq" style="background:${COMPANY.HBC}"></i>HBC post</span><span><i class="sq" style="background:${COMPANY.NWC}"></i>NWC post</span><span><i class="sq" style="background:${COMPANY.AMER}"></i>American post</span><span><i class="dot"></i>band camp</span><span><i class="pin"></i>quest step (numbered, joined in order)</span><span><i class="dia"></i>beat or effect</span></div>
      <div id="mapInfo" class="map-info"></div>`;
    $('mapQuest').onchange = (e) => { map.quest = e.target.value; fitMap(); paintMap(); };
    $('mapEffects').onchange = (e) => { map.show.effects = e.target.checked; paintMap(); };
    $('mapFamilies').onchange = (e) => { map.show.families = e.target.checked; paintMap(); };
    $('mapLabels').onchange = (e) => { map.show.labels = e.target.checked; paintMap(); };
    $('mapFit').onclick = () => { fitMap(); paintMap(); };
    $('mapFind').onchange = (e) => findPlace(e.target.value);
    wireCanvas($('mapCanvas'));
    new ResizeObserver(() => paintMap()).observe(box.querySelector('.map-wrap'));
  }
  if (!map.world) {
    try {
      map.world = await api('world');
      for (const c of map.world.cast) { if (!map.castAt.has(c.place)) map.castAt.set(c.place, []); map.castAt.get(c.place).push(c); }
      $('mapPlaces').innerHTML = map.world.places.filter(p => p.kind !== 'family').map(p => `<option value="${esc(p.name)}">`).join('');
      const img = new Image();
      img.onload = () => { map.ground = img; paintMap(); };
      img.src = '/api/ground';
    } catch (e) { box.querySelector('.map-wrap').innerHTML = `<div class="empty">${esc(e.message)}</div>`; return; }
  }
  await refreshMapPins();
}

// The campaign's places, from the script as it stands (after each check).
async function refreshMapPins() {
  if (!map.world) return;
  const seq = ++map.seq;
  try { const r = await post('map', el.input.value); if (seq !== map.seq) return; map.pins = r.pins; map.error = ''; }
  catch (e) { if (seq !== map.seq) return; map.error = e.message; }
  const quests = [...new Map(map.pins.filter(p => p.group === 'quest').map(p => [p.quest, p.title || p.quest])).entries()];
  const sel = $('mapQuest'), cur = map.quest;
  sel.innerHTML = '<option value="">All quests</option>' + quests.map(([id, t]) => `<option value="${esc(id)}">${esc(t)}</option>`).join('');
  sel.value = quests.some(([id]) => id === cur) ? cur : '';
  map.quest = sel.value;
  renderMissing();
  if (!map.view.scale) fitMap();
  paintMap();
  renderInfo();
}

const visiblePins = () => map.pins.filter(p => p.ok && (map.show.effects || p.kind !== 'effect') && (!map.quest || p.quest === map.quest));

function renderMissing() {
  const box = $('mapMissing');
  const bad = map.pins.filter(p => !p.ok && (!map.quest || p.quest === map.quest));
  let h = map.error ? `<div class="problem"><span class="sev error">error</span><div class="msg">The script does not compile, so its places cannot be drawn: ${esc(map.error)}</div></div>` : '';
  if (bad.length) h += `<div class="map-missing-head">${bad.length} name${bad.length > 1 ? 's' : ''} not on the map</div>` + bad.map((p, i) =>
    `<div class="problem" data-i="${map.pins.indexOf(p)}"><span class="sev warning">nowhere</span><div class="msg">${esc(p.why)}<div class="at">${esc(p.label)} · line ${p.line}</div></div></div>`).join('');
  box.innerHTML = h;
  box.querySelectorAll('.problem[data-i]').forEach(d => d.onclick = () => jump(map.pins[+d.dataset.i].line, 1));
}

// ---- view -------------------------------------------------------------------------

function canvasSize() { const c = $('mapCanvas'); return { w: c.clientWidth, h: c.clientHeight }; }
const toScreen = (x, y) => [(x - map.view.ox) * map.view.scale, (y - map.view.oy) * map.view.scale];
const toWorld = (sx, sy) => [sx / map.view.scale + map.view.ox, sy / map.view.scale + map.view.oy];

function fitMap() {
  if (!map.world) return;
  const { w, h } = canvasSize(); if (!w || !h) return;
  const pts = visiblePins();
  let x0, y0, x1, y1;
  if (pts.length) {
    x0 = Math.min(...pts.map(p => p.x)); x1 = Math.max(...pts.map(p => p.x)); y0 = Math.min(...pts.map(p => p.y)); y1 = Math.max(...pts.map(p => p.y));
    const pad = Math.max(4000, (x1 - x0) * 0.15, (y1 - y0) * 0.15); x0 -= pad; x1 += pad; y0 -= pad; y1 += pad;
  } else { x0 = 0; y0 = 0; x1 = map.world.width; y1 = map.world.height; }
  map.view.scale = Math.min(w / (x1 - x0), h / (y1 - y0));
  map.view.ox = (x0 + x1) / 2 - w / 2 / map.view.scale; map.view.oy = (y0 + y1) / 2 - h / 2 / map.view.scale;
}

function findPlace(name) {
  const p = map.world && map.world.places.find(q => q.name.toLowerCase() === String(name).toLowerCase());
  if (!p) return;
  const { w, h } = canvasSize();
  map.view.scale = Math.max(map.view.scale, w / 60000);
  map.view.ox = p.x - w / 2 / map.view.scale; map.view.oy = p.y - h / 2 / map.view.scale;
  map.selected = { place: p }; paintMap(); renderInfo();
}

function wireCanvas(c) {
  let drag = null;
  c.addEventListener('wheel', (e) => {
    e.preventDefault();
    const r = c.getBoundingClientRect(), [wx, wy] = toWorld(e.clientX - r.left, e.clientY - r.top);
    const k = Math.exp(-e.deltaY * 0.0015);
    map.view.scale = Math.min(0.2, Math.max(0.0015, map.view.scale * k));
    map.view.ox = wx - (e.clientX - r.left) / map.view.scale; map.view.oy = wy - (e.clientY - r.top) / map.view.scale;
    paintMap();
  }, { passive: false });
  c.addEventListener('pointerdown', (e) => { drag = { x: e.clientX, y: e.clientY, ox: map.view.ox, oy: map.view.oy, moved: false }; c.setPointerCapture(e.pointerId); });
  c.addEventListener('pointermove', (e) => {
    const r = c.getBoundingClientRect();
    if (drag) {
      const dx = e.clientX - drag.x, dy = e.clientY - drag.y;
      if (Math.abs(dx) + Math.abs(dy) > 3) drag.moved = true;
      map.view.ox = drag.ox - dx / map.view.scale; map.view.oy = drag.oy - dy / map.view.scale;
      paintMap(); return;
    }
    const hit = hitTest(e.clientX - r.left, e.clientY - r.top);
    const key = hit ? (hit.place ? hit.place.name : '') + '|' + (hit.pins ? hit.pins.length : 0) : '';
    if (key !== (map.hover && map.hover.key)) { map.hover = hit ? { ...hit, key } : null; paintMap(); }
    showMapTip(hit, e.clientX - r.left, e.clientY - r.top);
  });
  c.addEventListener('pointerup', (e) => {
    const r = c.getBoundingClientRect(), wasDrag = drag && drag.moved; drag = null;
    if (wasDrag) return;
    const hit = hitTest(e.clientX - r.left, e.clientY - r.top);
    map.selected = hit; paintMap(); renderInfo();
    if (hit && hit.pins && hit.pins.length === 1) jump(hit.pins[0].line, 1);
  });
  c.addEventListener('pointerleave', () => { map.hover = null; $('mapTip').hidden = true; paintMap(); });
  c.addEventListener('keydown', (e) => {
    const step = 60 / map.view.scale;
    if (e.key === 'ArrowLeft') map.view.ox -= step; else if (e.key === 'ArrowRight') map.view.ox += step;
    else if (e.key === 'ArrowUp') map.view.oy -= step; else if (e.key === 'ArrowDown') map.view.oy += step;
    else if (e.key === '+' || e.key === '=') zoomBy(1.25); else if (e.key === '-') zoomBy(0.8); else return;
    e.preventDefault(); paintMap();
  });
}
function zoomBy(k) { const { w, h } = canvasSize(), [cx, cy] = toWorld(w / 2, h / 2); map.view.scale = Math.min(0.2, Math.max(0.0015, map.view.scale * k)); map.view.ox = cx - w / 2 / map.view.scale; map.view.oy = cy - h / 2 / map.view.scale; }

// What is under the pointer: pins first (all those at that spot), else a place.
function hitTest(sx, sy) {
  if (!map.world) return null;
  const near = (x, y, r) => { const [px, py] = toScreen(x, y); return (px - sx) ** 2 + (py - sy) ** 2 <= r * r; };
  const pins = visiblePins().filter(p => near(p.x, p.y, 9));
  let place = null, best = 1e9;
  for (const p of map.world.places) {
    if (p.kind === 'family' && !map.show.families) continue;
    const [px, py] = toScreen(p.x, p.y), d = (px - sx) ** 2 + (py - sy) ** 2;
    if (d < best && d <= 100) { best = d; place = p; }
  }
  if (!pins.length && !place) return null;
  if (!place && pins.length) place = map.world.places.find(q => q.name === pins[0].at) || null;
  return { place, pins };
}

function castLine(c) { return `${c.leader ? '★ ' : ''}${esc(c.name)} <span class="dim">${esc(c.role)}${c.faction && c.faction !== 'HBC' ? ', ' + esc(c.faction) : ''}</span>`; }
function placeLine(p) {
  if (!p) return '';
  const what = p.kind === 'post' || p.kind === 'nwc_post' ? `${COMPANY_NAME[p.company] || p.company} post${p.year ? `, ${p.year}` : ''}${p.closed ? ` (given up ${p.closed})` : ''}`
    : p.kind === 'camp' ? `${esc(p.nation)} camp${p.people ? `, ${p.people} people` : ''}` : p.kind === 'family' ? `${esc(p.nation)} family at ${esc(p.post)}` : esc(p.kind);
  return `<b>${esc(p.name)}</b> <span class="dim">${what}</span>`;
}

function showMapTip(hit, x, y) {
  const tip = $('mapTip');
  if (!hit) { tip.hidden = true; return; }
  let h = placeLine(hit.place);
  if (hit.pins.length) h += `<div class="tip-pins">${hit.pins.slice(0, 6).map(p => `<div><i style="background:${questColor(p.quest)}"></i>${esc(p.label)}</div>`).join('')}${hit.pins.length > 6 ? `<div class="dim">and ${hit.pins.length - 6} more (click)</div>` : ''}</div>`;
  const cast = hit.place ? map.castAt.get(hit.place.name) || [] : [];
  if (cast.length) h += `<div class="tip-cast">${cast.slice(0, 8).map(castLine).join('<br>')}${cast.length > 8 ? `<br><span class="dim">and ${cast.length - 8} more (click)</span>` : ''}</div>`;
  tip.innerHTML = h; tip.hidden = false;
  const wrap = tip.parentElement.getBoundingClientRect();
  tip.style.left = Math.min(x + 14, wrap.width - tip.offsetWidth - 6) + 'px';
  tip.style.top = Math.min(y + 14, wrap.height - tip.offsetHeight - 6) + 'px';
}

// The selected place: everything the campaign does there, and everyone who lives there.
function renderInfo() {
  const box = $('mapInfo');
  const s = map.selected;
  if (!s || !s.place) { box.innerHTML = `<div class="dim">Click a place for its people and for what the campaign does there. ${map.world ? `${map.world.cast.length} people are placed in the world.` : ''}</div>`; return; }
  const name = s.place.name;
  const pins = map.pins.filter(p => p.ok && p.at === name);
  const cast = map.castAt.get(name) || [];
  let h = `<div>${placeLine(s.place)}</div>`;
  if (pins.length) h += `<h5>In this campaign</h5>` + pins.map(p => `<div class="problem" data-line="${p.line}"><span class="sev" style="color:${questColor(p.quest)}">${p.kind === 'effect' ? 'effect' : p.order > 0 ? 'step ' + p.order : 'giver'}</span><div class="msg">${esc(p.label)}<div class="at">${esc(p.who || p.place || '')} · line ${p.line}</div></div></div>`).join('');
  h += cast.length ? `<h5>Who is here (${cast.length})</h5><div class="map-cast">${cast.map(c => `<div>${castLine(c)}</div>`).join('')}</div>` : `<h5>Who is here</h5><div class="dim">Nobody is placed here in the editor.</div>`;
  box.innerHTML = h;
  box.querySelectorAll('.problem[data-line]').forEach(d => d.onclick = () => jump(+d.dataset.line, 1));
}

// ---- painting ---------------------------------------------------------------------

function paintMap() {
  const c = $('mapCanvas'); if (!c || !map.world || $('tab-map').hidden) return;
  const dpr = window.devicePixelRatio || 1, w = c.clientWidth, h = c.clientHeight;
  if (!w || !h) return;
  if (!map.view.scale) fitMap();
  if (c.width !== Math.round(w * dpr) || c.height !== Math.round(h * dpr)) { c.width = Math.round(w * dpr); c.height = Math.round(h * dpr); }
  const g = c.getContext('2d');
  g.setTransform(dpr, 0, 0, dpr, 0, 0);
  const css = getComputedStyle(document.documentElement), text = css.getPropertyValue('--text').trim(), bg = css.getPropertyValue('--bg').trim();
  g.fillStyle = bg; g.fillRect(0, 0, w, h);
  const s = map.view.scale;
  // The ground: one pixel of ground.png per cell, a cell 128 x 32 world pixels.
  if (map.ground) {
    g.imageSmoothingEnabled = false;
    const [gx, gy] = toScreen(0, 0);
    g.globalAlpha = 0.9;
    g.drawImage(map.ground, gx, gy, map.world.columns * 128 * s, map.world.rows * 32 * s);
    g.globalAlpha = 1;
  }
  const inView = (sx, sy) => sx > -40 && sy > -40 && sx < w + 40 && sy < h + 40;
  const labels = [];
  // Camps, then posts on top.
  for (const p of map.world.places) {
    if (p.kind !== 'camp' && p.kind !== 'family') continue;
    if (p.kind === 'family' && !map.show.families) continue;
    const [sx, sy] = toScreen(p.x, p.y); if (!inView(sx, sy)) continue;
    g.beginPath(); g.arc(sx, sy, p.kind === 'family' ? 2.5 : 4, 0, Math.PI * 2);
    g.fillStyle = p.kind === 'family' ? 'rgba(150,110,70,.8)' : '#a8763e'; g.fill();
    g.lineWidth = 1; g.strokeStyle = 'rgba(0,0,0,.55)'; g.stroke();
    if (p.kind === 'camp') labels.push([sx + 6, sy + 3, p.name, 0]);
  }
  for (const p of map.world.places) {
    if (p.kind !== 'post' && p.kind !== 'nwc_post') continue;
    const [sx, sy] = toScreen(p.x, p.y); if (!inView(sx, sy)) continue;
    const col = COMPANY[p.company] || '#999', r = 4;
    if (p.closed) { g.lineWidth = 1.5; g.strokeStyle = col; g.strokeRect(sx - r, sy - r, r * 2, r * 2); }
    else { g.fillStyle = col; g.fillRect(sx - r, sy - r, r * 2, r * 2); g.lineWidth = 1; g.strokeStyle = 'rgba(0,0,0,.6)'; g.strokeRect(sx - r, sy - r, r * 2, r * 2); }
    labels.push([sx + 7, sy + 4, p.name, 1]);
  }
  // A quest's road: its steps in order, joined.
  const pins = visiblePins();
  const byQuest = new Map();
  for (const p of pins) if (p.group === 'quest' && p.order >= 0) { if (!byQuest.has(p.quest)) byQuest.set(p.quest, []); byQuest.get(p.quest).push(p); }
  g.lineWidth = 2;
  for (const [q, list] of byQuest) {
    list.sort((a, b) => a.order - b.order);
    g.strokeStyle = questColor(q, map.quest ? 0.95 : 0.6); g.setLineDash(map.quest ? [] : [6, 4]);
    g.beginPath();
    list.forEach((p, i) => { const [sx, sy] = toScreen(p.x, p.y); if (i) g.lineTo(sx, sy); else g.moveTo(sx, sy); });
    g.stroke();
  }
  g.setLineDash([]);
  // Pins: several at one spot fan out a little so each can be seen.
  const stack = new Map();
  for (const p of pins) {
    const k = Math.round(p.x) + ',' + Math.round(p.y), n = stack.get(k) || 0; stack.set(k, n + 1);
    let [sx, sy] = toScreen(p.x, p.y); if (!inView(sx, sy)) continue;
    if (n) { const a = n * 0.9; sx += Math.cos(a) * (6 + n * 1.5); sy += Math.sin(a) * (6 + n * 1.5); }
    const hot = map.hover && map.hover.pins && map.hover.pins.includes(p);
    if (p.kind === 'effect') {
      g.beginPath(); g.moveTo(sx, sy - 6); g.lineTo(sx + 6, sy); g.lineTo(sx, sy + 6); g.lineTo(sx - 6, sy); g.closePath();
      g.fillStyle = p.group === 'beat' ? 'rgba(255,203,107,.9)' : questColor(p.quest, 0.85); g.fill(); g.strokeStyle = hot ? text : 'rgba(0,0,0,.7)'; g.lineWidth = hot ? 2 : 1; g.stroke();
      continue;
    }
    g.beginPath(); g.arc(sx, sy, 8, 0, Math.PI * 2); g.fillStyle = questColor(p.quest); g.fill();
    g.lineWidth = hot ? 2.5 : 1.2; g.strokeStyle = hot ? text : 'rgba(0,0,0,.75)'; g.stroke();
    g.fillStyle = '#fff'; g.font = 'bold 10px system-ui, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
    g.fillText(p.order > 0 ? String(p.order) : 'G', sx, sy + 0.5);
  }
  // Names, where there is room for them.
  if (map.show.labels && s > 0.0045) {
    g.font = '11px system-ui, sans-serif'; g.textAlign = 'left'; g.textBaseline = 'alphabetic';
    const taken = [];
    labels.sort((a, b) => b[3] - a[3]);
    for (const [x, y, t, isPost] of labels) {
      if (!isPost && s < 0.008) continue;
      const wdt = g.measureText(t).width;
      if (taken.some(([a, b, c, d]) => x < c && x + wdt > a && y - 10 < d && y > b)) continue;
      taken.push([x, y - 10, x + wdt, y]);
      g.lineWidth = 3; g.strokeStyle = 'rgba(0,0,0,.55)'; g.strokeText(t, x, y); g.fillStyle = '#f2efe6'; g.fillText(t, x, y);
    }
  }
  // Scale bar: 100 km.
  const km = map.world.pxPerKm || 40, len = 100 * km * s;
  if (len > 20 && len < w * 0.6) {
    g.fillStyle = 'rgba(0,0,0,.55)'; g.fillRect(10, h - 26, len + 12, 18);
    g.strokeStyle = '#fff'; g.lineWidth = 2; g.beginPath(); g.moveTo(16, h - 13); g.lineTo(16 + len, h - 13); g.stroke();
    g.fillStyle = '#fff'; g.font = '10px system-ui, sans-serif'; g.textAlign = 'left'; g.fillText('100 km', 18, h - 15);
  }
}
