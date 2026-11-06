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

export type EventCallback = (event: OfficeEvent) => void;

export class EventBridge {
  private eventSource: EventSource | null = null;
  private currentTool = 'danh-so-phong';
  private listeners: EventCallback[] = [];
  public isConnected = false;

  constructor(toolId: string = 'danh-so-phong') {
    this.currentTool = toolId;
  }

  public onEvent(callback: EventCallback) {
    this.listeners.push(callback);
  }

  public async fetchHistory(toolId?: string): Promise<OfficeEvent[]> {
    const tool = toolId || this.currentTool;
    try {
      const res = await fetch(`/api/history?tool=${tool}`);
      if (!res.ok) return [];
      const data = await res.json();
      return data.events || [];
    } catch (e) {
      console.warn('Failed to fetch history:', e);
      return [];
    }
  }

  public async fetchTools(): Promise<Array<{ id: string; name: string }>> {
    try {
      const res = await fetch('/api/tools');
      if (!res.ok) return [];
      const data = await res.json();
      return data.tools || [];
    } catch (e) {
      return [{ id: 'danh-so-phong', name: 'ĐÁNH SỐ PHÒNG TỰ ĐỘNG' }];
    }
  }

  public connect(toolId?: string) {
    if (toolId) this.currentTool = toolId;
    if (this.eventSource) {
      this.eventSource.close();
    }

    try {
      this.eventSource = new EventSource(`/api/stream?tool=${this.currentTool}`);

      this.eventSource.onopen = () => {
        this.isConnected = true;
      };

      this.eventSource.onmessage = (msg) => {
        try {
          const parsed = JSON.parse(msg.data);
          if (parsed.type === 'event' && parsed.event) {
            this.notifyListeners(parsed.event);
          }
        } catch (e) { }
      };

      this.eventSource.onerror = () => {
        this.isConnected = false;
        // Auto-reconnect after 3s
        setTimeout(() => {
          if (!this.isConnected) this.connect();
        }, 3000);
      };
    } catch (e) {
      this.isConnected = false;
    }
  }

  private notifyListeners(event: OfficeEvent) {
    this.listeners.forEach(cb => cb(event));
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
}

