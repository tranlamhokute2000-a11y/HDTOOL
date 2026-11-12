import express from 'express';
import cors from 'cors';
import fs from 'fs';
import os from 'os';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = 5174;

app.use(cors());
app.use(express.json());

const PROJECT_ROOT = path.resolve(__dirname, '../../');
const WORK_DIR = path.resolve(PROJECT_ROOT, 'work');
const DEMO_EVENTS_FILE = path.resolve(__dirname, '../demo/demo-events.jsonl');
const BRAIN_DIR = path.join(os.homedir(), '.gemini', 'antigravity', 'brain');

if (!fs.existsSync(WORK_DIR)) {
  fs.mkdirSync(WORK_DIR, { recursive: true });
}

function safeReadJson(filePath) {
  try {
    if (!fs.existsSync(filePath)) return null;
    const raw = fs.readFileSync(filePath, 'utf8');
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function readTailLines(filePath, maxBytes = 65536) {
  try {
    if (!fs.existsSync(filePath)) return [];
    const stat = fs.statSync(filePath);
    if (stat.size === 0) return [];
    const start = Math.max(0, stat.size - maxBytes);
    const length = stat.size - start;
    const fd = fs.openSync(filePath, 'r');
    try {
      const buf = Buffer.alloc(length);
      fs.readSync(fd, buf, 0, length, start);
      let text = buf.toString('utf8');
      if (start > 0) {
        const firstNewline = text.indexOf('\n');
        if (firstNewline >= 0) text = text.slice(firstNewline + 1);
      }
      return text.split(/\r?\n/).filter(l => l.trim().length > 0);
    } finally {
      fs.closeSync(fd);
    }
  } catch {
    return [];
  }
}

function formatToolCallSummary(call) {
  if (!call || !call.name) return 'Đang xử lý tác vụ';
  const name = call.name;
  const args = call.args || {};
  const fileTarget = args.Path || args.TargetFile || args.File || args.path || '';
  const baseFile = fileTarget ? path.basename(String(fileTarget)) : '';

  switch (name) {
    case 'edit_file':
      return baseFile ? `✏️ Đang sửa ${baseFile}` : '✏️ Đang chỉnh sửa mã nguồn';
    case 'write_file':
      return baseFile ? `📝 Đang ghi ${baseFile}` : '📝 Đang tạo tệp mới';
    case 'read_file':
      return baseFile ? `📖 Đang đọc ${baseFile}` : '📖 Đang đọc mã nguồn';
    case 'run_command': {
      const cmd = String(args.CommandLine || args.toolSummary || '').trim();
      const shortCmd = cmd.length > 48 ? cmd.slice(0, 48) + '…' : cmd;
      return args.toolSummary
        ? `⚙️ ${String(args.toolSummary)}`
        : (shortCmd ? `⚙️ Chạy: ${shortCmd}` : '⚙️ Đang chạy lệnh build/test');
    }
    case 'post_message': {
      const msg = String(args.text || '').replace(/\s+/g, ' ').trim();
      return msg ? `💬 Nhắn Board: ${msg.slice(0, 42)}${msg.length > 42 ? '…' : ''}` : '💬 Đăng tin lên Swarm Board';
    }
    case 'read_board':
      return '📋 Đang đọc Swarm Board';
    case 'swarm_wait':
      return '⏳ Ngủ chờ phân công mới trên Board';
    case 'swarm':
      return `🧭 Điều phối Swarm (${args.action || 'manage'})`;
    case 'invoke_subagent':
      return `🚀 Khởi chạy Subagent (${args.AgentName || 'worker'})`;
    case 'search_web':
      return `🌐 Tra cứu tài liệu: ${String(args.query || '').slice(0, 35)}`;
    default:
      return `🔧 ${name}`;
  }
}

function readTranscriptTelemetry(conversationId) {
  const result = {
    isRunningStep: false,
    lastStepStatus: '',
    lastStepType: '',
    lastActivityAt: '',
    currentAction: '',
    lastToolName: '',
    recentActions: [],
    mtimeMs: 0
  };
  if (!conversationId) return result;

  const logPath = path.join(BRAIN_DIR, conversationId, '.system_generated', 'logs', 'transcript.jsonl');
  if (!fs.existsSync(logPath)) return result;

  try {
    result.mtimeMs = fs.statSync(logPath).mtimeMs;
  } catch { }

  const lines = readTailLines(logPath, 96 * 1024);
  const actions = [];

  for (let i = 0; i < lines.length; i++) {
    try {
      const step = JSON.parse(lines[i]);
      result.lastStepStatus = step.status || result.lastStepStatus;
      result.lastStepType = step.type || result.lastStepType;
      result.lastActivityAt = step.created_at || result.lastActivityAt;

      if (step.tool_calls && Array.isArray(step.tool_calls)) {
        for (const call of step.tool_calls) {
          const summary = formatToolCallSummary(call);
          actions.push({
            ts: step.created_at || '',
            tool: call.name || 'step',
            summary,
            status: step.status || 'DONE'
          });
          result.lastToolName = call.name || '';
          result.currentAction = summary;
        }
      } else if (step.type === 'USER_INPUT' && step.content) {
        const txt = String(step.content).replace(/\s+/g, ' ').trim();
        if (txt && !txt.startsWith('<CONTEXT_SUMMARY>')) {
          actions.push({
            ts: step.created_at || '',
            tool: 'prompt',
            summary: `📩 Nhận nhiệm vụ: ${txt.slice(0, 52)}${txt.length > 52 ? '…' : ''}`,
            status: step.status || 'DONE'
          });
        }
      } else if (step.type === 'PLANNER_RESPONSE' && step.content && (!step.tool_calls || step.tool_calls.length === 0)) {
        const clean = String(step.content).replace(/\s+/g, ' ').trim();
        if (clean) {
          result.currentAction = `💡 ${clean.slice(0, 55)}${clean.length > 55 ? '…' : ''}`;
          actions.push({
            ts: step.created_at || '',
            tool: 'reply',
            summary: result.currentAction,
            status: step.status || 'DONE'
          });
        }
      }
    } catch {
      // Skip malformed partial line while appending
    }
  }

  result.isRunningStep = result.lastStepStatus === 'RUNNING' || result.lastStepStatus === 'PENDING';
  result.recentActions = actions.slice(-6).reverse();
  return result;
}

function readBoardMessages(convDir) {
  const boardPath = path.join(convDir, '.system_generated', 'swarm', 'board.jsonl');
  if (!fs.existsSync(boardPath)) return [];
  const messages = [];
  try {
    const raw = fs.readFileSync(boardPath, 'utf8');
    const lines = raw.split(/\r?\n/);
    for (const line of lines) {
      const t = line.trim();
      if (!t) continue;
      try {
        messages.push(JSON.parse(t));
      } catch { }
    }
  } catch { }
  return messages;
}

function extractMentions(text) {
  if (!text) return [];
  const found = [];
  const regex = /@([a-zA-Z0-9_-]+)/g;
  let match;
  while ((match = regex.exec(text)) !== null) {
    const name = match[1].toLowerCase();
    if (!found.includes(name)) found.push(name);
  }
  return found;
}

function readSwarmSnapshot(convId) {
  const convDir = path.join(BRAIN_DIR, convId);
  if (!fs.existsSync(convDir)) return null;

  const swarmDir = path.join(convDir, '.system_generated', 'swarm');
  const membersData = safeReadJson(path.join(swarmDir, 'members.json'));
  const boardMessages = readBoardMessages(convDir);

  let swarmCompleted = false;
  let doneSeq = 0;
  for (const msg of boardMessages) {
    if (typeof msg.text === 'string' && msg.text.trim().toLowerCase().startsWith('[done]')) {
      swarmCompleted = true;
      doneSeq = msg.seq || 0;
    } else if (msg.seq > doneSeq && typeof msg.text === 'string' && /@\w+/.test(msg.text)) {
      swarmCompleted = false;
    }
  }

  const workersMap = new Map();

  // 1. Official Swarm members
  if (membersData && membersData.members) {
    for (const [memberName, meta] of Object.entries(membersData.members)) {
      const telemetry = readTranscriptTelemetry(meta.conversation_id);
      const isRecentWrite = telemetry.mtimeMs > 0 && (Date.now() - telemetry.mtimeMs) < 12000;

      const memberPosts = boardMessages.filter(m => m.agent === memberName);
      const lastPost = memberPosts.length > 0 ? memberPosts[memberPosts.length - 1] : null;
      const pendingMention = boardMessages.find(
        m => m.seq > (meta.last_read_seq || 0) && extractMentions(m.text).includes(memberName.toLowerCase())
      );

      let status = 'IDLE';
      if (meta.killed) {
        status = 'KILLED';
      } else if (
        meta.wake_mode === 'working' ||
        telemetry.isRunningStep ||
        (isRecentWrite && telemetry.lastToolName !== 'swarm_wait')
      ) {
        status = 'WORK';
      } else if (pendingMention) {
        status = 'WAIT';
      } else if (memberName === 'monitor' && !swarmCompleted) {
        status = 'WAIT';
      }

      if (memberName === 'coordinator' && telemetry.isRunningStep) {
        status = 'WORK';
      }

      let taskSummary = telemetry.currentAction;
      if (status === 'IDLE' && lastPost && !telemetry.currentAction.startsWith('⚙️') && !telemetry.currentAction.startsWith('✏️')) {
        const clean = String(lastPost.text || '').replace(/\s+/g, ' ').trim();
        taskSummary = clean ? `✅ #${lastPost.seq}: ${clean.slice(0, 62)}${clean.length > 62 ? '…' : ''}` : 'Đang sẵn sàng nhận lệnh';
      } else if (!taskSummary && lastPost) {
        taskSummary = `#${lastPost.seq}: ${String(lastPost.text || '').slice(0, 62)}`;
      }
      if (!taskSummary) {
        taskSummary = status === 'WORK' ? 'Đang thực thi tác vụ...' : 'Đang chờ phân công';
      }

      workersMap.set(memberName, {
        id: memberName,
        name: memberName,
        role: meta.role || 'worker',
        conversationId: meta.conversation_id || '',
        model: meta.applied_model || meta.model || 'gemini-3.8-flash-high',
        workspaceMode: meta.workspace_mode || 'inherit',
        wakeMode: meta.wake_mode || '',
        killed: Boolean(meta.killed),
        status,
        currentAction: taskSummary,
        lastToolName: telemetry.lastToolName,
        lastActivityAt: telemetry.lastActivityAt || (lastPost ? lastPost.time : ''),
        lastReadSeq: meta.last_read_seq || 0,
        lastPostSeq: lastPost ? lastPost.seq : 0,
        recentActions: telemetry.recentActions,
        boardPosts: memberPosts.slice(-4).reverse().map(p => ({
          seq: p.seq,
          time: p.time,
          text: String(p.text || '').slice(0, 180)
        }))
      });
    }
  }

  // 2. Standalone subagents (e.g., research, browser, self)
  const subagentsDir = path.join(convDir, '.system_generated', 'subagents');
  if (fs.existsSync(subagentsDir)) {
    try {
      const files = fs.readdirSync(subagentsDir).filter(f => f.endsWith('.json'));
      for (const file of files) {
        const sub = safeReadJson(path.join(subagentsDir, file));
        if (!sub || !sub.conversationId) continue;
        const alreadyInSwarm = Array.from(workersMap.values()).some(w => w.conversationId === sub.conversationId);
        if (alreadyInSwarm) continue;

        const desc = sub.subagentDescriptor || {};
        const subId = `sub-${String(sub.conversationId).slice(0, 6)}`;
        const telemetry = readTranscriptTelemetry(sub.conversationId);
        const isAlive = sub.state === 'SUBAGENT_STATE_ALIVE' && (telemetry.isRunningStep || (Date.now() - telemetry.mtimeMs) < 15000);

        workersMap.set(subId, {
          id: subId,
          name: desc.role || desc.typeName || subId,
          role: desc.typeName || 'subagent',
          conversationId: sub.conversationId,
          model: 'subagent',
          workspaceMode: 'inherit',
          wakeMode: isAlive ? 'working' : 'idle',
          killed: sub.state === 'SUBAGENT_STATE_KILLED',
          status: isAlive ? 'WORK' : 'IDLE',
          currentAction: telemetry.currentAction || (isAlive ? 'Đang chạy subagent...' : 'Đã hoàn thành nhiệm vụ'),
          lastToolName: telemetry.lastToolName,
          lastActivityAt: telemetry.lastActivityAt,
          lastReadSeq: 0,
          lastPostSeq: 0,
          recentActions: telemetry.recentActions,
          boardPosts: []
        });
      }
    } catch { }
  }

  return {
    id: `swarm:${convId}`,
    convId,
    title: (membersData && membersData.title) || `Phiên Antigravity ${convId.slice(0, 8)}`,
    swarmCompleted,
    headSeq: boardMessages.length > 0 ? boardMessages[boardMessages.length - 1].seq : 0,
    workers: Array.from(workersMap.values()),
    board: boardMessages.map(m => ({
      seq: m.seq,
      agent: m.agent,
      time: m.time,
      kind: m.kind || 'post',
      mentions: extractMentions(m.text),
      text: m.text
    }))
  };
}

function getSwarmSources() {
  const sources = [];
  if (!fs.existsSync(BRAIN_DIR)) return sources;

  try {
    const entries = fs.readdirSync(BRAIN_DIR, { withFileTypes: true });
    for (const entry of entries) {
      if (!entry.isDirectory()) continue;
      const convDir = path.join(BRAIN_DIR, entry.name);
      const membersFile = path.join(convDir, '.system_generated', 'swarm', 'members.json');
      const boardFile = path.join(convDir, '.system_generated', 'swarm', 'board.jsonl');
      const subagentsDir = path.join(convDir, '.system_generated', 'subagents');

      const hasSwarm = fs.existsSync(membersFile);
      let hasSubagents = false;
      if (fs.existsSync(subagentsDir)) {
        try {
          hasSubagents = fs.readdirSync(subagentsDir).some(f => f.endsWith('.json'));
        } catch { }
      }
      if (!hasSwarm && !hasSubagents) continue;

      let mtime = 0;
      for (const f of [boardFile, membersFile, path.join(convDir, '.system_generated', 'logs', 'transcript.jsonl')]) {
        if (fs.existsSync(f)) {
          mtime = Math.max(mtime, fs.statSync(f).mtimeMs);
        }
      }

      const membersData = safeReadJson(membersFile);
      const memberCount = membersData && membersData.members ? Object.keys(membersData.members).length : 0;
      const title = (membersData && membersData.title) || `Workspace ${entry.name.slice(0, 8)}`;

      sources.push({
        id: `swarm:${entry.name}`,
        kind: 'swarm',
        convId: entry.name,
        name: `⚡ SWARM TRỰC TIẾP: ${title}`,
        workerCount: memberCount,
        hasEvents: true,
        mtime
      });
    }
  } catch (err) {
    console.error('Error scanning brain directory:', err);
  }

  sources.sort((a, b) => b.mtime - a.mtime);
  return sources;
}

function getWorkTools() {
  const tools = [];
  try {
    if (fs.existsSync(WORK_DIR)) {
      const entries = fs.readdirSync(WORK_DIR, { withFileTypes: true });
      for (const entry of entries) {
        if (!entry.isDirectory()) continue;
        const eventsFile = path.join(WORK_DIR, entry.name, 'events.jsonl');
        let mtime = 0;
        let hasEvents = false;
        if (fs.existsSync(eventsFile)) {
          mtime = fs.statSync(eventsFile).mtimeMs;
          hasEvents = true;
        }
        if (!hasEvents && entry.name === 'hvac_captures') continue;
        tools.push({
          id: entry.name,
          kind: 'work',
          name: `🛠️ TOOL REVIT: ${entry.name.replace(/-/g, ' ').toUpperCase()}`,
          hasEvents,
          mtime
        });
      }
    }
  } catch (err) {
    console.error('Error scanning work directory:', err);
  }
  tools.sort((a, b) => b.mtime - a.mtime);
  return tools;
}

function readToolEvents(toolId) {
  const events = [];
  let filePath = path.join(WORK_DIR, toolId, 'events.jsonl');

  if (toolId === 'demo:office' || !fs.existsSync(filePath)) {
    if (fs.existsSync(DEMO_EVENTS_FILE)) {
      filePath = DEMO_EVENTS_FILE;
    } else {
      return events;
    }
  }

  try {
    const content = fs.readFileSync(filePath, 'utf8');
    const lines = content.split(/\r?\n/);
    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed) continue;
      try {
        events.push(JSON.parse(trimmed));
      } catch { }
    }
  } catch (err) {
    console.error(`Error reading events from ${filePath}:`, err);
  }
  return events;
}

app.get('/api/sources', (req, res) => {
  const swarms = getSwarmSources();
  const workTools = getWorkTools();
  const sources = [
    ...swarms,
    ...workTools,
    { id: 'demo:office', kind: 'demo', name: '🎬 Mô phỏng Demo 12 Agent Revit', hasEvents: true, mtime: 0 }
  ];
  res.json({ sources });
});

app.get('/api/tools', (req, res) => {
  const swarms = getSwarmSources();
  const workTools = getWorkTools();
  res.json({ tools: [...swarms, ...workTools] });
});

app.get('/api/swarm/snapshot', (req, res) => {
  const rawId = String(req.query.id || '');
  const convId = rawId.startsWith('swarm:') ? rawId.slice(6) : rawId;
  const snapshot = readSwarmSnapshot(convId);
  if (!snapshot) {
    return res.status(404).json({ error: 'Swarm not found' });
  }
  res.json({ snapshot });
});

app.get('/api/history', (req, res) => {
  const toolId = String(req.query.tool || 'boc-khoi-luong-ong-gio');
  if (toolId.startsWith('swarm:')) {
    const snapshot = readSwarmSnapshot(toolId.slice(6));
    return res.json({ tool: toolId, snapshot, events: [] });
  }
  const events = readToolEvents(toolId);
  res.json({ tool: toolId, count: events.length, events });
});

app.post('/api/reply', (req, res) => {
  const { tool, detail } = req.body;
  const toolId = tool || 'boc-khoi-luong-ong-gio';
  const filePath = path.join(WORK_DIR, toolId, 'events.jsonl');

  const replyEvent = {
    ts: new Date().toISOString(),
    tool: toolId,
    agent: 'orchestrator',
    event: 'user_reply',
    detail: detail || 'Chủ dự án (Sếp Hồ) đã đồng ý'
  };

  try {
    fs.appendFileSync(filePath, JSON.stringify(replyEvent) + '\n', 'utf8');
    res.json({ ok: true, event: replyEvent });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.get('/api/stream', (req, res) => {
  const toolId = String(req.query.tool || 'boc-khoi-luong-ong-gio');

  res.setHeader('Content-Type', 'text/event-stream');
  res.setHeader('Cache-Control', 'no-cache');
  res.setHeader('Connection', 'keep-alive');
  res.flushHeaders();

  res.write(`data: ${JSON.stringify({ type: 'connected', tool: toolId })}\n\n`);

  if (toolId.startsWith('swarm:')) {
    const convId = toolId.slice(6);
    let lastSig = '';

    const pushIfChanged = () => {
      try {
        const snapshot = readSwarmSnapshot(convId);
        if (!snapshot) return;
        const sig = JSON.stringify({
          headSeq: snapshot.headSeq,
          swarmCompleted: snapshot.swarmCompleted,
          workers: snapshot.workers.map(w => [w.id, w.status, w.currentAction, w.lastReadSeq, w.lastPostSeq, w.lastActivityAt])
        });
        if (sig !== lastSig) {
          lastSig = sig;
          res.write(`data: ${JSON.stringify({ type: 'swarm_snapshot', snapshot })}\n\n`);
        }
      } catch { }
    };

    pushIfChanged();
    const intervalId = setInterval(pushIfChanged, 500);
    req.on('close', () => {
      clearInterval(intervalId);
      res.end();
    });
    return;
  }

  const filePath = path.join(WORK_DIR, toolId, 'events.jsonl');
  let lastSize = fs.existsSync(filePath) ? fs.statSync(filePath).size : 0;

  const intervalId = setInterval(() => {
    if (!fs.existsSync(filePath)) return;
    try {
      const stats = fs.statSync(filePath);
      if (stats.size > lastSize) {
        const stream = fs.createReadStream(filePath, {
          start: lastSize,
          end: stats.size,
          encoding: 'utf8'
        });
        let chunk = '';
        stream.on('data', data => { chunk += data; });
        stream.on('end', () => {
          lastSize = stats.size;
          const lines = chunk.split(/\r?\n/);
          for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed) continue;
            try {
              const event = JSON.parse(trimmed);
              res.write(`data: ${JSON.stringify({ type: 'event', event })}\n\n`);
            } catch { }
          }
        });
      } else if (stats.size < lastSize) {
        lastSize = stats.size;
      }
    } catch { }
  }, 400);

  req.on('close', () => {
    clearInterval(intervalId);
    res.end();
  });
});

const DIST_DIR = path.resolve(__dirname, '../dist');
if (fs.existsSync(DIST_DIR)) {
  app.use(express.static(DIST_DIR));
}

app.listen(PORT, () => {
  console.log(`[Office-3D Bridge Server] Running on http://localhost:${PORT}`);
  console.log(`[Office-3D Bridge Server] Watching Brain Swarms: ${BRAIN_DIR}`);
  console.log(`[Office-3D Bridge Server] Watching Revit Work: ${WORK_DIR}`);
});
