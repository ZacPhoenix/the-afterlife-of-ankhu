'use strict';
// Headless re-implementation of the Borrowed Dawn prototype logic (prototype script, lines 32-80).
// The block between "BEGIN VERBATIM" and "END VERBATIM" is copied character-for-character from the
// prototype. Only the DOM helpers ($, say, render, change, save, tone) are replaced by stubs.
// `--verify-source <html>` re-reads the prototype as plain text (never executed) and checks that every
// verbatim line still appears in it unchanged.
//
// Usage:
//   node js_reference.js bfs <out.jsonl>                     exhaustive BFS over JS states x actions
//   node js_reference.js fuzz <seed> <count> <out.jsonl> <bfs.jsonl>   random walk with random typed text
//   node js_reference.js --verify-source <prototype.html>

const fs = require('fs');

// ------------------------------------------------------------------ DOM stubs / instrumentation
let s, history = [], steps = 0;
let rec = { accepted: false, messages: [] };
const $ = id => ({ showModal() { rec.messages.push('[help modal]'); } });
function say(t) { rec.messages.push(t); }
function tone() {}
function render() {}
function save() { history.push(JSON.stringify(s)); if (history.length > 100) history.shift(); steps++; }
function change(fn, msg) { save(); fn(); render(); rec.accepted = true; say(msg); tone(); }

// ------------------------------------------------------------------ BEGIN VERBATIM
const initial=()=>({water:'east',loc:'floor',bindings:[],mirror:'raft',awake:false,read:false,bell:false,struck:false,echo:false,won:false});
const bound=t=>s.bindings.includes(t),high=()=>s.water==='east'||bound('elevator'),shadow=()=>s.mirror==='socket'&&(!s.awake||bound('shadow')),sun=()=>s.mirror==='socket'&&s.awake&&s.water==='east';
function move(to){if(s.won)return say('The morning has returned. Restart to play again.');if(s.loc==='floor'&&to==='lift'&&high())return say('The elevator is above you. Send the water west to lower it.');if(s.loc==='lift'&&((to==='gallery'&&!high())||(to==='floor'&&high())))return say('There is no landing at this height. Change the water direction.');if((s.loc==='gallery'&&to==='landing'||s.loc==='landing'&&to==='gallery')&&!high())return say('The elevator must be high to complete the upper walkway.');if((to==='gate'||s.loc==='gate')&&!bound('bridge'))return say('The pendulum is moving. Bind the bridge before crossing.');change(()=>s.loc=to,'Ankhu moves to the '+to+'.')}
function command(raw){let c=raw.toLowerCase().trim().replace(/[.,!?]/g,'').replace(/\s+/g,' ').replace(/\bthe /g,'');if(!c)return;if(s.won)return say('The puzzle is complete. Restart to play again.');
if(/^flow (west|east)$/.test(c)){let d=c.split(' ')[1];if(d===s.water)return say('The water is already in the '+d+' basin.');return change(()=>s.water=d,d==='west'?'Water pours west. The raft rises.':'Water returns east. The elevator gains buoyant support.')}
if(c.startsWith('bind ')){let t=c.slice(5);if(!['elevator','shadow','bridge'].includes(t))return say('Bindable targets: elevator, shadow, bridge.');if(bound(t))return say('That binding is already held.');if(s.bindings.length>=2)return say('Both bindings are occupied. Find another way to support something, then release it.');if(t==='elevator'&&(!high()||!['gallery','landing','lift'].includes(s.loc)))return say('Reach the upper gallery before anchoring the raised elevator.');if(t==='shadow'&&!shadow())return say('A shadow must fall on the seal before you can bind it.');if(t==='bridge'&&s.loc!=='landing')return say('Reach the bell landing to address the bridge.');return change(()=>s.bindings.push(t),t==='bridge'?'The bridge settles horizontally and holds.':`A violet thread binds the ${t}.`)}
if(c.startsWith('release ')){let t=c.slice(8);if(!bound(t))return say('That target is not bound.');if(t==='bridge'&&s.loc==='gate')return say('Cross back before releasing your only return path.');return change(()=>s.bindings=s.bindings.filter(x=>x!==t),`You release the ${t}.`)}
if(c==='take mirror'){if(s.loc!=='gallery')return say('The mirror is collected from the upper gallery balcony.');if(s.mirror!=='raft')return say('You have already collected the mirror.');if(s.water!=='west'||!high())return say('The raft must be raised and the elevator must hold the balcony connection.');return change(()=>s.mirror='carried','You carry the sun-mirror. Descend to the socket below.')}
if(c==='install mirror'){if(s.loc!=='floor'||s.mirror!=='carried')return say('Carry the mirror to the ground-floor socket first.');return change(()=>s.mirror='socket','The mirror catches sunlight. The Bearer casts a shadow onto the seal.')}
if(c==='read inscription'||c==='read gallery'){if(s.loc!=='gallery')return say('Reach the upper gallery to read its inscriptions.');if(s.read)return say('The name is “He Who Returns the Morning.” The relief shows a shadow remaining after its owner leaves.');return change(()=>s.read=true,'The gate is “He Who Returns the Morning.” A relief depicts a body departing while its shadow remains.')}
if(c==='wake bearer'||c==='wake statue'){if(s.awake)return say('The Bearer is already in its alcove.');return change(()=>s.awake=true,'The Bearer walks into its alcove. '+(bound('shadow')?'Its shadow remains.':'Its shadow follows. Perhaps sleep can restore it.'))}
if(c==='sleep bearer'||c==='sleep statue'){if(!s.awake)return say('The Bearer is already at rest.');return change(()=>s.awake=false,'The Bearer returns to its marked pedestal and sleeps.')}
if(c==='recall bell'){if(s.loc!=='landing')return say('Reach the bell landing first.');if(s.bell)return say('The bell is already whole.');return change(()=>s.bell=true,'Gold fills the bell’s cracks. It remembers its intact form.')}
if(c==='strike bell'){if(s.loc!=='landing'||!s.bell)return say('Stand at the bell landing and repair the bell before striking it.');change(()=>s.struck=true,'A clear bronze note fills the chamber. The resonator remembers it.');tone(440);return}
if(c==='echo bell'){if(!s.struck)return say('The bell has no clear sound to remember. Repair it, then strike it.');if(s.echo)return say('The bell is already repeating its toll.');return change(()=>s.echo=true,'The bell repeats its remembered note. This uses no binding.')}
if(c==='he who returns morning'||c==='he who returns the morning'){if(s.loc!=='gate')return say('Speak the gate’s name from its threshold.');if(!s.read)return say('Learn the name from the gallery first.');if(!sun()||!shadow()||!s.echo)return say('The gate hears you, but its sun, shadow, and sound must all be present.');return change(()=>s.won=true,'The gate opens. Dawn pours into the chamber.')}
if(c==='help')return $('help').showModal();say('The chamber does not understand. Try flow west, bind shadow, wake bearer, recall bell, or echo bell.');}
// ------------------------------------------------------------------ END VERBATIM

// UI navigation offered by render() (copied from the `nav` expression): only these moves are clickable.
const NEIGHBOURS = { floor: ['lift'], lift: ['floor', 'gallery'], gallery: ['floor', 'landing'], landing: ['gallery', 'gate'], gate: ['landing'] };

// Same normalization line as command(); used only to label which branch JS dispatched to (diagnostics).
function jsNormalize(raw) { return raw.toLowerCase().trim().replace(/[.,!?]/g, '').replace(/\s+/g, ' ').replace(/\bthe /g, ''); }
// Mirrors the if-chain order of command() to name the interpretation. Does not affect decisions.
function jsInterpret(raw) {
  const c = jsNormalize(raw);
  if (!c) return 'EMPTY';
  if (/^flow (west|east)$/.test(c)) return c;
  if (c.startsWith('bind ')) { const t = c.slice(5); return ['elevator', 'shadow', 'bridge'].includes(t) ? 'bind ' + t : 'BIND_INVALID'; }
  if (c.startsWith('release ')) { const t = c.slice(8); return ['elevator', 'shadow', 'bridge'].includes(t) ? 'release ' + t : 'RELEASE_INVALID'; }
  if (c === 'take mirror' || c === 'install mirror' || c === 'recall bell' || c === 'strike bell' || c === 'echo bell') return c;
  if (c === 'read inscription' || c === 'read gallery') return 'read inscription';
  if (c === 'wake bearer' || c === 'wake statue') return 'wake bearer';
  if (c === 'sleep bearer' || c === 'sleep statue') return 'sleep bearer';
  if (c === 'he who returns morning' || c === 'he who returns the morning') return 'he who returns the morning';
  if (c === 'help') return 'HELP';
  return 'UNKNOWN';
}

const clone = st => JSON.parse(JSON.stringify(st));
const keyOf = st => JSON.stringify({ ...st, bindings: [...st.bindings].sort() });

/** Runs one action against a copy of `state`. action = {t:'cmd', text} | {t:'move', to}. */
function run(state, action) {
  s = clone(state); history = []; steps = 0;
  rec = { accepted: false, messages: [] };
  if (action.t === 'move') move(action.to); else command(action.text);
  const next = clone(s);
  return { accepted: rec.accepted, next, msg: rec.messages.join(' | '), changed: keyOf(next) !== keyOf(state) };
}

const TYPED = [
  'flow west', 'flow east',
  'bind elevator', 'bind shadow', 'bind bridge',
  'release elevator', 'release shadow', 'release bridge',
  'wake bearer', 'sleep bearer', 'recall bell', 'strike bell', 'echo bell',
  'take mirror', 'install mirror', 'read inscription', 'he who returns the morning',
  // prototype-internal aliases
  'read gallery', 'wake statue', 'sleep statue', 'he who returns morning',
  // inputs the prototype rejects at parse level
  'bind gate', 'release gate', 'help', '',
];

function actionsFor(st) {
  return TYPED.map(text => ({ t: 'cmd', text })).concat(NEIGHBOURS[st.loc].map(to => ({ t: 'move', to })));
}

function bfs() {
  const start = (s = initial(), clone(s));
  const seen = new Map([[keyOf(start), start]]);
  const queue = [start];
  const rows = [];
  while (queue.length) {
    const st = queue.shift();
    for (const a of actionsFor(st)) {
      const r = run(st, a);
      rows.push({ state: st, action: a, accepted: r.accepted, changed: r.changed, next: r.next, msg: r.msg,
        interp: a.t === 'cmd' ? jsInterpret(a.text) : 'go to ' + a.to });
      const k = keyOf(r.next);
      if (!seen.has(k)) { seen.set(k, r.next); queue.push(r.next); }
    }
  }
  return { states: [...seen.values()], rows };
}

// ------------------------------------------------------------------ random text generator
function rng(seed) { let a = seed >>> 0; return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
function makeGen(R) {
  const pick = arr => arr[Math.floor(R() * arr.length)];
  const VERBS = ['flow', 'bind', 'release', 'unbind', 'wake', 'awaken', 'sleep', 'recall', 'strike', 'ring', 'echo', 'take', 'pick up', 'install', 'place', 'read', 'board', 'enter', 'descend', 'cross', 'go', 'go to', 'walk to', 'move to', 'speak', 'say', 'help', 'look', 'he who returns', 'he who'];
  const OBJS = ['west', 'east', 'w', 'e', 'north', 'elevator', 'lift', 'shadow', 'bridge', 'pendulum', 'bearer', 'statue', 'bell', 'resonator', 'mirror', 'sun mirror', 'inscription', 'inscriptions', 'gallery', 'balcony', 'floor', 'ground', 'landing', 'bell landing', 'gate', 'threshold', 'morning', 'the morning', 'to floor', 'xyzzy', ''];
  const ARTICLES = ['the', 'The', 'THE', 'a', 'an', 'the the'];
  const PUNCT = ['.', ',', '!', '?', ';', ':', '-', "'", '"', '…', '(', ')', '_', '/'];
  const WS = [' ', '  ', '\t', ' ', '\n', '　'];
  const WEIRD = ['ｆｌｏｗ ｗｅｓｔ', 'ﬂow west', 'FLOW WEST', 'Flow West!', 'flow\twest', 'bind shadow', 'bathe west', 'flow-the west', 'flow west the', 'the flow west', 'theflow west', 'flow thewest', 'release the', 'bind the', 'flow west.', 'flow west .', '. flow west', 'he, who returns the morning!', 'He Who Returns The Morning', 'speak he who returns morning', 'İnstall mirror', 'ınstall mirror', 'strıke bell', 'ﬁ', 'wake  bearer ', 'echo bell?!', 'recall bell…', 'take mirror;', 'read the inscription', 'read the gallery', 'release elevator shadow', 'bind elevator,shadow', 'flow west east', 'bind 1', 'release 0', '42', '   ', ''];
  const CANON = TYPED.filter(t => t);
  const caseMix = str => [...str].map(ch => R() < 0.3 ? ch.toUpperCase() : ch).join('');
  return function gen() {
    const r = R();
    let out;
    if (r < 0.40) out = pick(CANON);                                    // canonical, usually accepted path
    else if (r < 0.55) out = pick(WEIRD);                               // hand-picked edge cases
    else {                                                              // compositional
      const parts = [];
      if (R() < 0.15) parts.push(pick(ARTICLES));
      parts.push(pick(VERBS));
      if (R() < 0.25) parts.push(pick(ARTICLES));
      if (R() < 0.9) parts.push(pick(OBJS));
      if (R() < 0.08) parts.push(pick(OBJS));
      if (R() < 0.08) parts.push(pick(ARTICLES));
      out = parts.map(p => p + (R() < 0.08 ? pick(PUNCT) : '')).join(R() < 0.15 ? pick(WS) : ' ');
    }
    if (R() < 0.25) out = caseMix(out);
    if (R() < 0.10) out = pick(WS) + out;
    if (R() < 0.10) out = out + pick(WS);
    if (R() < 0.10) out = out + pick(PUNCT);
    if (R() < 0.05) out = pick(PUNCT) + out;
    return out;
  };
}

function fuzz(seed, count, bfsStates) {
  const R = rng(seed);
  const gen = makeGen(R);
  const rows = [];
  let st = clone(bfsStates[0]);
  for (let i = 0; i < count; i++) {
    if (i % 200 === 0) st = clone(bfsStates[Math.floor(R() * bfsStates.length)]);   // restart from a random reachable state
    let a;
    if (R() < 0.25) a = { t: 'move', to: NEIGHBOURS[st.loc][Math.floor(R() * NEIGHBOURS[st.loc].length)] };
    else a = { t: 'cmd', text: gen() };
    const r = run(st, a);
    rows.push({ state: st, action: a, accepted: r.accepted, changed: r.changed, next: r.next, msg: r.msg,
      interp: a.t === 'cmd' ? jsInterpret(a.text) : 'go to ' + a.to });
    st = r.next;
  }
  return rows;
}

function writeJsonl(path, rows) {
  const fd = fs.openSync(path, 'w');
  let buf = [];
  for (const r of rows) { buf.push(JSON.stringify(r)); if (buf.length >= 5000) { fs.writeSync(fd, buf.join('\n') + '\n'); buf = []; } }
  if (buf.length) fs.writeSync(fd, buf.join('\n') + '\n');
  fs.closeSync(fd);
}

function verifySource(htmlPath) {
  const html = fs.readFileSync(htmlPath, 'utf8');
  const self = fs.readFileSync(__filename, 'utf8');
  const block = self.split('// ------------------------------------------------------------------ BEGIN VERBATIM\n')[1]
    .split('// ------------------------------------------------------------------ END VERBATIM')[0];
  const lines = block.split('\n').filter(l => l.length);
  const htmlLines = new Set(html.split('\n'));
  const missing = lines.filter(l => !htmlLines.has(l));
  console.log(`verify-source: ${lines.length - missing.length}/${lines.length} verbatim lines found unchanged in ${htmlPath}`);
  for (const m of missing) console.log('  MISSING: ' + m.slice(0, 120));
  process.exitCode = missing.length ? 1 : 0;
}

if (require.main === module) {
  const [mode, ...args] = process.argv.slice(2);
  if (mode === 'bfs') {
    const { states, rows } = bfs();
    writeJsonl(args[0], rows);
    console.log(`js bfs: ${states.length} states, ${rows.length} (state, action) rows -> ${args[0]}`);
  } else if (mode === 'fuzz') {
    const seed = Number(args[0]), count = Number(args[1]), out = args[2], bfsFile = args[3];
    const states = [];
    const seenK = new Set();
    for (const line of fs.readFileSync(bfsFile, 'utf8').split('\n')) {
      if (!line) continue;
      const st = JSON.parse(line).state; const k = keyOf(st);
      if (!seenK.has(k)) { seenK.add(k); states.push(st); }
    }
    const rows = fuzz(seed, count, states);
    writeJsonl(out, rows);
    console.log(`js fuzz seed=${seed}: ${rows.length} actions -> ${out}`);
  } else if (mode === '--verify-source') {
    verifySource(args[0]);
  } else {
    console.error('usage: node js_reference.js bfs <out> | fuzz <seed> <count> <out> <bfs.jsonl> | --verify-source <html>');
    process.exitCode = 2;
  }
}

module.exports = { initial, run, NEIGHBOURS, jsInterpret, jsNormalize };
