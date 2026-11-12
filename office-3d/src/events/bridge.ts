export interface OfficeEvent {
  ts: string;
  tool: string;
  agent: string;
  event: string;
  detail: string;
  stage?: string;
  result?: string;
  pct?: number;
  to?: string;
  from?: string;
  file?: string;
  kind?: string;
  severity?: string;
  round?: number;
  errors?: number;
  passed?: number;
  failed?: number;
  inferred?: boolean;
}

export interface SourceItem {
  id: string;
  kind: 'swarm' | 'work' | 'demo';
  name: string;
  convId?: string;
  workerCount?: number;
}

export interface WorkerActionEntry {
  ts: string;
  tool: string;
  summary: string;
  status: string;
}

export interface SwarmWorkerTelemetry {
  id: string;
  name: string;
  role: string;
  conversationId: string;
  model: string;
  workspaceMode: string;
  wakeMode: string;
  killed: boolean;
  status: 'WORK' | 'WAIT' | 'IDLE' | 'FAIL' | 'KILLED';
  currentAction: string;
  lastToolName: string;
  lastActivityAt: string;
  lastReadSeq: number;
  lastPostSeq: number;
  recentActions: WorkerActionEntry[];
  boardPosts: Array<{ seq: number; time: string; text: string }>;
}

export interface SwarmBoardMessage {
  seq: number;
  agent: string;
  time: string;
  kind: string;
  mentions: string[];
  text: string;
}

export interface SwarmSnapshot {
  id: string;
  convId: string;
  title: string;
  swarmCompleted: boolean;
  headSeq: number;
  workers: SwarmWorkerTelemetry[];
  board: SwarmBoardMessage[];
}

export type EventCallback = (event: OfficeEvent) => void;
export type SwarmCallback = (snapshot: SwarmSnapshot) => void;

export class EventBridge {
  private eventSource: EventSource | null = null;
  private currentTool = '';
  private eventListeners: EventCallback[] = [];
  private swarmListeners: SwarmCallback[] = [];
  private reconnectTimer: number | null = null;
  public isConnected = false;

  public onEvent(callback: EventCallback) {
    this.eventListeners.push(callback);
  }

  public onSwarmSnapshot(callback: SwarmCallback) {
    this.swarmListeners.push(callback);
  }

  public async fetchSources(): Promise<SourceItem[]> {
    try {
      const res = await fetch('/api/sources');
      if (!res.ok) return [];
      const data = await res.json();
      return data.sources || [];
    } catch (e) {
      console.warn('Failed to fetch sources:', e);
      return [{ id: 'demo:office', kind: 'demo', name: '🎬 Mô phỏng Demo 12 Agent Revit' }];
    }
  }

  public async fetchSwarmSnapshot(sourceId: string): Promise<SwarmSnapshot | null> {
    try {
      const res = await fetch(`/api/swarm/snapshot?id=${encodeURIComponent(sourceId)}`);
      if (!res.ok) return null;
      const data = await res.json();
      return data.snapshot || null;
    } catch {
      return null;
    }
  }

  public async fetchHistory(toolId: string): Promise<{ events: OfficeEvent[]; snapshot?: SwarmSnapshot }> {
    try {
      const res = await fetch(`/api/history?tool=${encodeURIComponent(toolId)}`);
      if (!res.ok) return { events: [] };
      const data = await res.json();
      return { events: data.events || [], snapshot: data.snapshot };
    } catch (e) {
      console.warn('Failed to fetch history:', e);
      return { events: [] };
    }
  }

  public connect(toolId: string) {
    this.currentTool = toolId;
    if (this.reconnectTimer !== null) {
      window.clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    if (this.eventSource) {
      this.eventSource.close();
      this.eventSource = null;
    }
    if (!toolId || toolId === 'demo:office') return;

    try {
      const target = toolId;
      this.eventSource = new EventSource(`/api/stream?tool=${encodeURIComponent(target)}`);

      this.eventSource.onopen = () => {
        this.isConnected = true;
      };

      this.eventSource.onmessage = (msg) => {
        try {
          const parsed = JSON.parse(msg.data);
          if (parsed.type === 'event' && parsed.event) {
            this.eventListeners.forEach(cb => cb(parsed.event));
          } else if (parsed.type === 'swarm_snapshot' && parsed.snapshot) {
            this.swarmListeners.forEach(cb => cb(parsed.snapshot));
          }
        } catch { }
      };

      this.eventSource.onerror = () => {
        this.isConnected = false;
        if (this.reconnectTimer === null) {
          this.reconnectTimer = window.setTimeout(() => {
            this.reconnectTimer = null;
            if (this.currentTool === target) {
              this.connect(target);
            }
          }, 3000);
        }
      };
    } catch {
      this.isConnected = false;
    }
  }

  public async sendUserReply(tool: string, detail: string) {
    try {
      await fetch('/api/reply', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tool, detail })
      });
    } catch (e) {
      console.error('Failed to send reply:', e);
    }
  }

  public static buildReplayEventsFromSwarm(snapshot: SwarmSnapshot): OfficeEvent[] {
    const out: OfficeEvent[] = [];
    const title = snapshot.title || 'Swarm Session';

    out.push({
      ts: new Date().toISOString(),
      tool: title,
      agent: 'coordinator',
      event: 'tool_start',
      detail: `Khởi chạy Swarm: ${title}`
    });

    for (const msg of snapshot.board) {
      const textClean = String(msg.text || '').replace(/\s+/g, ' ').trim();
      const short = textClean.length > 74 ? textClean.slice(0, 73) + '…' : textClean;
      const lower = textClean.toLowerCase();

      if (lower.startsWith('[done]')) {
        out.push({
          ts: msg.time || new Date().toISOString(),
          tool: title,
          agent: msg.agent,
          event: 'tool_done',
          detail: short
        });
        continue;
      }

      if (lower.includes('failed') || lower.includes('bugs found') || lower.includes('error cs')) {
        out.push({
          ts: msg.time || new Date().toISOString(),
          tool: title,
          agent: msg.agent,
          event: 'issue',
          severity: 'warn',
          detail: `#${msg.seq}: ${short}`
        });
      } else {
        out.push({
          ts: msg.time || new Date().toISOString(),
          tool: title,
          agent: msg.agent,
          event: 'start',
          detail: `#${msg.seq}: ${short}`
        });
      }

      if (msg.mentions && msg.mentions.length > 0) {
        const target = msg.mentions[0];
        if (target !== 'all') {
          out.push({
            ts: msg.time || new Date().toISOString(),
            tool: title,
            agent: msg.agent,
            event: 'handoff',
            to: target,
            kind: 'work',
            detail: `#${msg.seq} → @${target}`
          });
        }
      }
    }

    return out;
  }
}
