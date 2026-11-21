// =====================================================================
// HDTOOL · ĐIỀU PHỐI STUDIO — máy chủ cục bộ (chỉ nghe 127.0.0.1)
// Đọc: .agents/CHI_DAO.md, .agents/BAO_CAO.md, .agents/LENH_SEP.md, git,
//      nhật ký làm việc của Antigravity (~/.gemini/antigravity/brain) và Claude (~/.claude/projects)
// Ghi: .agents/LENH_SEP.md (lệnh của Sếp), gộp nhánh vào main, push GitHub (khi Sếp bấm nút)
// Chạy: node .agents/studio/server.mjs   (hoặc MoDieuPhoiStudio.bat)
// =====================================================================
import http from 'node:http';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFile, execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..', '..');
const PORT = Number(process.env.STUDIO_PORT || 4870);
const AGENTS = path.join(ROOT, '.agents');
const F_CHIDAO = path.join(AGENTS, 'CHI_DAO.md');
const F_BAOCAO = path.join(AGENTS, 'BAO_CAO.md');
const F_LENH = path.join(AGENTS, 'LENH_SEP.md');
const BRAIN_DIR = path.join(os.homedir(), '.gemini', 'antigravity', 'brain');
const CLAUDE_DIR = path.join(os.homedir(), '.claude', 'projects', ROOT.replace(/[:\\/ ]/g, '-'));

// ---------------- Tiện ích ----------------
function readText(f) {
  try { return fs.readFileSync(f, 'utf8').replace(/^\uFEFF/, ''); } catch { return ''; }
}
function git(args) {
  try {
    return execFileSync('git', ['-c', 'core.quotepath=off', '-c', 'i18n.logOutputEncoding=UTF-8', ...args], {
      cwd: ROOT, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'], timeout: 15000
    }).trim();
  } catch { return ''; }
}
function gitRun(args, extraEnv) {
  return new Promise(resolve => {
    execFile('git', args, { cwd: ROOT, encoding: 'utf8', timeout: 120000, env: { ...process.env, GIT_TERMINAL_PROMPT: '0', ...(extraEnv || {}) } },
      (err, stdout, stderr) => resolve({ ok: !err, out: ((stdout || '') + (stderr || '')).trim() }));
  });
}
function tailLines(file, maxBytes = 160 * 1024) {
  try {
    const st = fs.statSync(file);
    const start = Math.max(0, st.size - maxBytes);
    const fd = fs.openSync(file, 'r');
    const buf = Buffer.alloc(st.size - start);
    fs.readSync(fd, buf, 0, buf.length, start);
    fs.closeSync(fd);
    const lines = buf.toString('utf8').split(/\r?\n/);
    if (start > 0) lines.shift();
    return lines.filter(Boolean);
  } catch { return []; }
}
function unq(v) {
  let s = v == null ? '' : String(v);
  if (s.length > 1 && s.startsWith('"') && s.endsWith('"')) { try { s = JSON.parse(s); } catch { s = s.slice(1, -1); } }
  return s.replace(/\s+/g, ' ').trim();
}
function short(s, n) { s = unq(s); return s.length > n ? s.slice(0, n - 1) + '…' : s; }
function base(p) { return p ? path.basename(unq(p).replace(/\\/g, '/')) : ''; }

// ---------------- Đọc CHI_DAO / BAO_CAO / LENH_SEP ----------------
function parseTasks(md) {
  const tasks = [];
  const re = /^###\s+(NV-\d+)\s+[—-]\s+(.+?)\s+[—-]\s+`([^`]+)`\s*$/gm;
  let m;
  while ((m = re.exec(md))) tasks.push({ id: m[1], title: m[2].replace(/`/g, '').trim(), status: m[3].trim() });
  return tasks;
}
function sectionAfter(md, headerRegex) {
  const m = md.match(headerRegex);
  if (!m) return '';
  return md.slice(m.index + m[0].length);
}
function parseEntries(text) {
  // Mỗi mục bắt đầu bằng "### " ; trả về {head, lines[], fields{}}
  const out = [];
  const parts = text.split(/^###\s+/m).slice(1);
  for (const p of parts) {
    const lines = p.split(/\r?\n/);
    const head = lines.shift().trim();
    const body = lines.filter(l => l.trim() && !l.startsWith('## ') && !l.startsWith('```'));
    const fields = {};
    for (const l of body) {
      const fm = l.match(/^\s*[-*]\s*([^:]{2,40}):\s*(.*)$/);
      if (fm) fields[fm[1].trim().toLowerCase()] = fm[2].trim();
    }
    const id = (head.match(/NV-\d+/) || [''])[0];
    out.push({ id, head, body: body.map(l => l.replace(/^\s*[-*]\s*/, '').trim()).slice(0, 30), fields });
  }
  return out;
}
function parseReviews(md) {
  const sec = sectionAfter(md, /^##\s*5\..*$/m);
  return parseEntries(sec).map(e => ({
    ...e,
    verdict: /CẦN SỬA/i.test(e.head) ? 'CẦN SỬA' : (/ĐẠT/i.test(e.head) ? 'ĐẠT' : '')
  }));
}
function parseReports(md) {
  const sec = sectionAfter(md, /^##\s*DANH SÁCH BÁO CÁO.*$/m);
  return parseEntries(sec).map(e => ({ ...e, status: e.fields['trạng thái'] || '', branch: e.fields['nhánh'] || '', commit: e.fields['commit'] || '' }));
}
function parseCommands(md) {
  return parseEntries(md).map(e => {
    const to = (e.head.match(/GỬI:\s*([^—-]+)/i) || ['', ''])[1].trim();
    const time = (e.head.match(/\d{4}-\d{2}-\d{2} \d{2}:\d{2}/) || [''])[0];
    const text = e.body.filter(l => !/^Trạng thái:/i.test(l)).join('\n');
    return { time, to, text, status: e.fields['trạng thái'] || '' };
  });
}

// ---------------- Hoạt động của Antigravity ----------------
function antigravityActivity() {
  const res = { found: false, session: '', updatedAt: 0, running: false, current: '', actions: [] };
  let latest = null;
  try {
    for (const d of fs.readdirSync(BRAIN_DIR, { withFileTypes: true })) {
      if (!d.isDirectory()) continue;
      const f = path.join(BRAIN_DIR, d.name, '.system_generated', 'logs', 'transcript.jsonl');
      try { const st = fs.statSync(f); if (!latest || st.mtimeMs > latest.m) latest = { f, m: st.mtimeMs, id: d.name }; } catch { }
    }
  } catch { return res; }
  if (!latest) return res;
  res.found = true; res.session = latest.id.slice(0, 8); res.updatedAt = latest.m;
  let lastStatus = '';
  for (const line of tailLines(latest.f)) {
    let s; try { s = JSON.parse(line); } catch { continue; }
    lastStatus = s.status || lastStatus;
    const ts = s.created_at ? Date.parse(s.created_at) : 0;
    if (Array.isArray(s.tool_calls) && s.tool_calls.length) {
      for (const c of s.tool_calls) res.actions.push({ ts, text: summarizeAnti(c) });
    } else if (s.type === 'USER_INPUT' && s.content) {
      const t = unq(s.content);
      if (t && !t.startsWith('<')) res.actions.push({ ts, text: 'Nhận lệnh: ' + short(t, 90), kind: 'prompt' });
    } else if (s.type === 'PLANNER_RESPONSE' && s.content) {
      res.actions.push({ ts, text: 'Trả lời: ' + short(s.content, 90), kind: 'reply' });
    }
  }
  res.running = lastStatus === 'RUNNING' || lastStatus === 'PENDING' || (Date.now() - latest.m < 45000);
  res.actions = res.actions.slice(-14).reverse();
  res.current = res.actions.length ? res.actions[0].text : '';
  return res;
}
function summarizeAnti(c) {
  const a = c.args || {};
  const file = base(a.Path || a.TargetFile || a.File || a.AbsolutePath || a.path);
  switch (c.name) {
    case 'edit_file': case 'replace_file_content': case 'multi_replace_file_content': return 'Sửa ' + (file || 'mã nguồn');
    case 'write_file': case 'write_to_file': return 'Ghi ' + (file || 'tệp');
    case 'read_file': case 'view_file': return 'Đọc ' + (file || 'tệp');
    case 'run_command': return 'Chạy: ' + short(a.toolSummary || a.CommandLine || 'lệnh', 80);
    case 'grep_search': case 'find_by_name': return 'Tìm: ' + short(a.Query || a.Pattern || a.SearchPath || '', 60);
    default: return short(c.name || 'thao tác', 60) + (file ? ' ' + file : '');
  }
}

// ---------------- Hoạt động của Claude ----------------
function claudeActivity() {
  const res = { found: false, session: '', updatedAt: 0, running: false, current: '', actions: [] };
  let latest = null;
  try {
    for (const n of fs.readdirSync(CLAUDE_DIR)) {
      if (!n.endsWith('.jsonl')) continue;
      const f = path.join(CLAUDE_DIR, n);
      try { const st = fs.statSync(f); if (!latest || st.mtimeMs > latest.m) latest = { f, m: st.mtimeMs, id: n }; } catch { }
    }
  } catch { return res; }
  if (!latest) return res;
  res.found = true; res.session = latest.id.slice(0, 8); res.updatedAt = latest.m;
  for (const line of tailLines(latest.f, 400 * 1024)) {
    let e; try { e = JSON.parse(line); } catch { continue; }
    const ts = e.timestamp ? Date.parse(e.timestamp) : 0;
    const content = e.message && Array.isArray(e.message.content) ? e.message.content : null;
    if (e.type === 'assistant' && content) {
      for (const c of content) {
        if (c.type === 'tool_use') res.actions.push({ ts, text: summarizeClaude(c) });
      }
    } else if (e.type === 'user' && e.message && typeof e.message.content === 'string') {
      const t = unq(e.message.content);
      if (t && !t.startsWith('<')) res.actions.push({ ts, text: 'Nhận lệnh: ' + short(t, 90), kind: 'prompt' });
    }
  }
  res.running = Date.now() - latest.m < 45000;
  res.actions = res.actions.filter(a => a.text).slice(-14).reverse();
  res.current = res.actions.length ? res.actions[0].text : '';
  return res;
}
function summarizeClaude(c) {
  const i = c.input || {};
  switch (c.name) {
    case 'Read': return 'Đọc ' + base(i.file_path);
    case 'Edit': return 'Sửa ' + base(i.file_path);
    case 'Write': return 'Ghi ' + base(i.file_path);
    case 'Bash': case 'PowerShell': return 'Chạy: ' + short(i.description || i.command || '', 80);
    case 'Grep': return 'Tìm: ' + short(i.pattern || '', 60);
    case 'Glob': return 'Tìm tệp: ' + short(i.pattern || '', 60);
    default:
      if (/update_plan|wrap_up|ToolSearch|mark_chapter/.test(c.name || '')) return '';
      if (/Claude_Browser/.test(c.name || '')) return 'Trình duyệt: ' + String(c.name).split('__').pop() + (i.action ? ' ' + i.action : '');
      if (c.name === 'Agent') return 'Giao tác tử phụ: ' + short(i.description || '', 60);
      if (c.name === 'Artifact') return 'Trang Artifact: ' + (i.action || 'publish');
      return short(String(c.name || '').split('__').pop(), 60);
  }
}

// ---------------- Git ----------------
function gitInfo() {
  const branch = git(['branch', '--show-current']);
  const head = git(['log', '-1', '--format=%h|%s']).split('|');
  const dirty = git(['status', '--porcelain']).split(/\r?\n/).filter(Boolean).map(l => ({ code: l.slice(0, 2).trim(), file: l.slice(3) }));
  const mainHead = git(['rev-parse', '--short', 'main']);
  const toPush = Number(git(['rev-list', '--count', 'origin/main..main']) || 0);
  const branches = git(['for-each-ref', 'refs/heads', '--format=%(refname:short)|%(objectname:short)|%(committerdate:iso-strict)|%(subject)'])
    .split(/\r?\n/).filter(Boolean).map(l => {
      const [name, sha, date, ...sub] = l.split('|');
      if (name === 'main') return null;
      const counts = git(['rev-list', '--left-right', '--count', `main...${name}`]).split(/\s+/);
      const merged = checkAncestor(name, 'main');
      return { name, sha, date, subject: sub.join('|'), behind: Number(counts[0] || 0), ahead: Number(counts[1] || 0), merged };
    }).filter(Boolean);
  const commits = git(['log', '--all', '-14', '--date=iso-strict', '--format=%h|%ad|%D|%s']).split(/\r?\n/).filter(Boolean).map(l => {
    const [sha, date, refs, ...sub] = l.split('|');
    return { sha, date, refs, subject: sub.join('|') };
  });
  return { branch, head: { sha: head[0] || '', subject: head.slice(1).join('|') }, dirty, mainHead, toPush, branches, commits };
}
function checkAncestor(a, b) {
  try { execFileSync('git', ['merge-base', '--is-ancestor', a, b], { cwd: ROOT, stdio: 'ignore' }); return true; } catch { return false; }
}

// ---------------- Theo dõi thay đổi tệp ----------------
const events = [];
function pushEvent(e) { events.unshift({ ts: Date.now(), ...e }); if (events.length > 80) events.length = 80; }
const lastSeen = new Map();
const WATCH_RE = /^(Source\/.*\.cs|\.agents\/[^/]+\.md|\.agents\/skills\/.*\.md|SKILL\.md|AGENTS\.md|GEMINI\.md|PreviewStudio\/.*\.html|Build\.ps1|VERSION\.txt)$/;
const knownMtime = new Map();
function statMtime(rel) { try { return fs.statSync(path.join(ROOT, rel)).mtimeMs; } catch { return -1; } }
// Ghi nhớ thời điểm sửa ban đầu: Windows báo "change" cả khi tệp chỉ được ĐỌC (vd. lúc biên dịch) -> chỉ tính khi mtime đổi thật
(function primeMtimes(dir, rel) {
  let list = []; try { list = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
  for (const d of list) {
    const r = rel ? rel + '/' + d.name : d.name;
    if (d.isDirectory()) { if (/^(Source|\.agents|PreviewStudio|\.agents\/skills.*)$/.test(r) || r.startsWith('.agents/')) primeMtimes(path.join(dir, d.name), r); }
    else if (WATCH_RE.test(r)) knownMtime.set(r, statMtime(r));
  }
})(ROOT, '');
try {
  fs.watch(ROOT, { recursive: true }, (type, file) => {
    if (!file) return;
    const f = file.replace(/\\/g, '/');
    if (!WATCH_RE.test(f)) return;
    const m = statMtime(f);
    if (knownMtime.get(f) === m) return;
    knownMtime.set(f, m);
    const now = Date.now();
    if (now - (lastSeen.get(f) || 0) < 4000) return;
    lastSeen.set(f, now);
    if (m < 0) { pushEvent({ kind: 'file', who: '', text: 'Xóa ' + f }); return; }
    let who = '';
    if (f.endsWith('BAO_CAO.md')) who = 'antigravity';
    else if (f.endsWith('CHI_DAO.md')) who = 'claude';
    else if (f.endsWith('LENH_SEP.md')) who = 'sep';
    else {
      const a = safeMtime('anti'), c = safeMtime('claude');
      if (a && now - a < 120000 && (!c || a >= c)) who = 'antigravity';
      else if (c && now - c < 120000) who = 'claude';
    }
    pushEvent({ kind: 'file', who, text: 'Thay đổi ' + f });
  });
} catch (e) { console.log('Không theo dõi được tệp:', e.message); }
const mtimeCache = { anti: 0, claude: 0, at: 0 };
function safeMtime(which) {
  if (Date.now() - mtimeCache.at > 5000) {
    mtimeCache.anti = antigravityActivity().updatedAt;
    mtimeCache.claude = claudeActivity().updatedAt;
    mtimeCache.at = Date.now();
  }
  return mtimeCache[which];
}

// ---------------- Trạng thái tổng ----------------
function state() {
  const chidao = readText(F_CHIDAO);
  const tasks = parseTasks(chidao);
  const reviews = parseReviews(chidao);
  const reports = parseReports(readText(F_BAOCAO));
  const commands = parseCommands(readText(F_LENH));
  const g = gitInfo();
  const active = tasks.find(t => /ĐANG GIAO/i.test(t.status)) || null;
  let pipeline = null;
  if (active) {
    const bname = 'anti/' + active.id.toLowerCase();
    const br = g.branches.find(b => b.name.toLowerCase() === bname) || null;
    const rep = reports.find(r => r.id === active.id) || null;
    const rev = reviews.find(r => r.id === active.id) || null;
    const merged = !!(br && br.merged && br.ahead === 0 && rev && rev.verdict === 'ĐẠT');
    const pushed = merged && g.toPush === 0;
    pipeline = { task: active, branch: br, report: rep, review: rev, merged, pushed, branchName: bname };
  }
  let version = '';
  try { version = fs.readFileSync(path.join(ROOT, 'VERSION.txt'), 'utf8').trim(); } catch { }
  return {
    now: Date.now(), root: ROOT, version, tasks, reviews, reports, commands, git: g, pipeline, events,
    antigravity: antigravityActivity(), claude: claudeActivity()
  };
}

// ---------------- Hành động ----------------
function nowStr() {
  const d = new Date(); const p = n => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}
function sendCommand(to, text) {
  const target = { antigravity: 'ANTIGRAVITY', claude: 'CLAUDE', ca_hai: 'CẢ HAI' }[to];
  if (!target) throw new Error('Người nhận không hợp lệ');
  const clean = String(text || '').replace(/\r/g, '').trim();
  if (!clean) throw new Error('Nội dung lệnh trống');
  if (clean.length > 4000) throw new Error('Lệnh quá dài (tối đa 4000 ký tự)');
  let md = readText(F_LENH);
  if (!md) md = '# HỘP THƯ LỆNH CỦA SẾP HỒ\n\n> Lệnh gửi từ Điều Phối Studio. Antigravity và Claude đọc các lệnh `MỚI` gửi cho mình trước khi làm việc,\n> thực hiện xong thì đổi `Trạng thái: MỚI` thành `Trạng thái: ĐÃ NHẬN (<tên>)`. Mục mới nhất ở trên cùng.\n\n## DANH SÁCH LỆNH\n';
  const entry = `\n### ${nowStr()} — GỬI: ${target}\n${clean.split('\n').map(l => '- ' + l).join('\n')}\n- Trạng thái: MỚI\n`;
  const idx = md.search(/^## DANH SÁCH LỆNH.*$/m);
  if (idx >= 0) {
    const lineEnd = md.indexOf('\n', idx);
    md = md.slice(0, lineEnd + 1) + entry + md.slice(lineEnd + 1);
  } else md += '\n## DANH SÁCH LỆNH\n' + entry;
  fs.writeFileSync(F_LENH, md, 'utf8');
  pushEvent({ kind: 'cmd', who: 'sep', text: `Sếp gửi lệnh cho ${target}: ${short(clean, 80)}` });
}
function runKiemTra() {
  return new Promise(resolve => {
    execFile('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(AGENTS, 'tools', 'KiemTra.ps1')],
      { cwd: ROOT, encoding: 'utf8', timeout: 240000, maxBuffer: 4 * 1024 * 1024 },
      (err, stdout, stderr) => {
        const out = ((stdout || '') + (stderr || '')).replace(/\x1b\[[0-9;]*m/g, '').trim();
        const pass = /KET QUA: DAT/.test(out);
        pushEvent({ kind: 'check', who: 'sep', text: 'Chạy KiemTra.ps1: ' + (pass ? 'ĐẠT' : 'KHÔNG ĐẠT') });
        resolve({ ok: true, pass, out });
      });
  });
}
async function mergeBranch(name) {
  const g = gitInfo();
  const br = g.branches.find(b => b.name === name);
  if (!br) throw new Error('Không tìm thấy nhánh ' + name);
  if (br.ahead === 0) throw new Error('Nhánh không có commit mới so với main');
  if (br.behind > 0) throw new Error('Nhánh đang thiếu ' + br.behind + ' commit của main — cần cập nhật nhánh trước (không gộp nhanh được)');
  let r;
  if (g.branch === 'main') r = await gitRun(['merge', '--ff-only', name]);
  else r = await gitRun(['fetch', '.', `${name}:main`]);
  pushEvent({ kind: 'git', who: 'sep', text: (r.ok ? 'Đã gộp ' : 'Gộp lỗi ') + name + ' vào main' });
  if (!r.ok) throw new Error(r.out || 'Gộp không thành công');
  return r.out;
}
async function pushMain() {
  const r = await gitRun(['push', 'origin', 'main']);
  pushEvent({ kind: 'git', who: 'sep', text: r.ok ? 'Đã push main lên GitHub' : 'Push lỗi' });
  if (!r.ok) throw new Error(r.out || 'Push không thành công');
  return r.out;
}

// ---------------- HTTP ----------------
function send(res, code, body, type) {
  res.writeHead(code, { 'Content-Type': type || 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(typeof body === 'string' ? body : JSON.stringify(body));
}
function readBody(req) {
  return new Promise((resolve, reject) => {
    let data = '';
    req.on('data', c => { data += c; if (data.length > 64 * 1024) { reject(new Error('Quá lớn')); req.destroy(); } });
    req.on('end', () => { try { resolve(data ? JSON.parse(data) : {}); } catch { reject(new Error('JSON không hợp lệ')); } });
  });
}
const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://127.0.0.1');
    if (req.method === 'GET' && (url.pathname === '/' || url.pathname === '/index.html')) {
      return send(res, 200, fs.readFileSync(path.join(__dirname, 'index.html'), 'utf8'), 'text/html; charset=utf-8');
    }
    if (req.method === 'GET' && url.pathname === '/api/state') return send(res, 200, state());
    if (req.method === 'POST') {
      // Chỉ nhận yêu cầu từ chính trang Studio trên máy này
      const origin = req.headers.origin || '';
      if (req.headers['x-studio'] !== '1' || (origin && !/^http:\/\/(127\.0\.0\.1|localhost):\d+$/.test(origin))) return send(res, 403, { ok: false, error: 'Từ chối' });
      const body = await readBody(req);
      if (url.pathname === '/api/lenh') { sendCommand(body.to, body.text); return send(res, 200, { ok: true }); }
      if (url.pathname === '/api/kiemtra') return send(res, 200, await runKiemTra());
      if (url.pathname === '/api/merge') return send(res, 200, { ok: true, out: await mergeBranch(String(body.branch || '')) });
      if (url.pathname === '/api/push') return send(res, 200, { ok: true, out: await pushMain() });
    }
    send(res, 404, { ok: false, error: 'Không có' });
  } catch (e) {
    send(res, 400, { ok: false, error: e.message || String(e) });
  }
});
server.listen(PORT, '127.0.0.1', () => {
  console.log('HDTOOL Điều Phối Studio: http://localhost:' + PORT);
  console.log('Thư mục dự án: ' + ROOT);
  console.log('Đóng cửa sổ này để tắt Studio.');
});
