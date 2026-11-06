import express from 'express';
import cors from 'cors';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = 5174;

app.use(cors());
app.use(express.json());

// Thư mục work nằm cùng cấp với office-3d (tức là ../work)
const PROJECT_ROOT = path.resolve(__dirname, '../../');
const WORK_DIR = path.resolve(PROJECT_ROOT, 'work');
const DEMO_EVENTS_FILE = path.resolve(__dirname, '../demo/demo-events.jsonl');

// Đảm bảo thư mục tồn tại
if (!fs.existsSync(WORK_DIR)) {
  fs.mkdirSync(WORK_DIR, { recursive: true });
}

// Hàm lấy danh sách tool
function getTools() {
  const tools = [];
  try {
    if (fs.existsSync(WORK_DIR)) {
      const entries = fs.readdirSync(WORK_DIR, { withFileTypes: true });
      for (const entry of entries) {
        if (entry.isDirectory()) {
          const toolDir = path.join(WORK_DIR, entry.name);
          const eventsFile = path.join(toolDir, 'events.jsonl');
          let mtime = 0;
          let hasEvents = false;
          if (fs.existsSync(eventsFile)) {
            mtime = fs.statSync(eventsFile).mtimeMs;
            hasEvents = true;
          }
          tools.push({
            id: entry.name,
            name: entry.name.replace(/-/g, ' ').toUpperCase(),
            hasEvents,
            mtime
          });
        }
      }
    }
  } catch (err) {
    console.error('Error scanning work directory:', err);
  }

  // Sắp xếp theo mtime mới nhất
  tools.sort((a, b) => b.mtime - a.mtime);
  return tools;
}

// Đọc toàn bộ sự kiện của một tool
function readToolEvents(toolId) {
  const events = [];
  let filePath = path.join(WORK_DIR, toolId, 'events.jsonl');

  if (!fs.existsSync(filePath)) {
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
        const parsed = JSON.parse(trimmed);
        events.push(parsed);
      } catch (e) {
        // Bỏ qua dòng json lỗi
      }
    }
  } catch (err) {
    console.error(`Error reading events from ${filePath}:`, err);
  }

  return events;
}

// API: Lấy danh sách tools
app.get('/api/tools', (req, res) => {
  const tools = getTools();
  res.json({ tools });
});

// API: Lấy toàn bộ lịch sử sự kiện
app.get('/api/history', (req, res) => {
  const toolId = req.query.tool || 'boc-khoi-luong-ong-gio';
  const events = readToolEvents(toolId);
  res.json({ tool: toolId, count: events.length, events });
});

// API: Phản hồi từ Sếp Hồ (ask_user -> user_reply)
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

// API: Server-Sent Events (SSE) theo dõi sự kiện của tool
app.get('/api/stream', (req, res) => {
  const toolId = req.query.tool || 'boc-khoi-luong-ong-gio';
  const filePath = path.join(WORK_DIR, toolId, 'events.jsonl');

  res.setHeader('Content-Type', 'text/event-stream');
  res.setHeader('Cache-Control', 'no-cache');
  res.setHeader('Connection', 'keep-alive');
  res.flushHeaders();

  res.write(`data: ${JSON.stringify({ type: 'connected', tool: toolId })}\n\n`);

  let lastSize = 0;
  if (fs.existsSync(filePath)) {
    lastSize = fs.statSync(filePath).size;
  }

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
            } catch (e) { }
          }
        });
      } else if (stats.size < lastSize) {
        lastSize = stats.size;
      }
    } catch (e) { }
  }, 400);

  req.on('close', () => {
    clearInterval(intervalId);
    res.end();
  });
});

app.listen(PORT, () => {
  console.log(`[Bridge Server] Running on http://localhost:${PORT}`);
  console.log(`[Bridge Server] Monitoring work directory: ${WORK_DIR}`);
});
