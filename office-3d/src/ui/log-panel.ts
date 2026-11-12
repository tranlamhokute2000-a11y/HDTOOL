import { OfficeEvent, SwarmBoardMessage } from '../events/bridge.js';
import { ROLES } from '../agents/roles.js';

export class LogPanel {
  private container: HTMLElement;
  private countEl: HTMLElement;
  private events: Array<{ agent: string; badge: string; color: string; time: string; detail: string; seq?: number }> = [];
  public onEventClick?: (agentId: string) => void;

  constructor() {
    this.container = document.getElementById('log-list')!;
    this.countEl = document.getElementById('log-count')!;
  }

  public setBoardMessages(messages: SwarmBoardMessage[]) {
    this.events = [];
    for (let i = messages.length - 1; i >= 0; i--) {
      const m = messages[i];
      const role = ROLES[m.agent];
      this.events.push({
        agent: m.agent,
        badge: role ? role.badge : '⚡',
        color: role ? role.color : '#2563EB',
        time: m.time || '',
        detail: `#${m.seq}: ${m.text}`,
        seq: m.seq
      });
    }
    this.render();
  }

  public addEvent(ev: OfficeEvent) {
    const role = ROLES[ev.agent];
    this.events.unshift({
      agent: ev.agent,
      badge: role ? role.badge : '●',
      color: role ? role.color : '#64748B',
      time: this.formatTime(ev.ts),
      detail: ev.detail || ev.event
    });
    if (this.events.length > 60) this.events.pop();
    this.render();
  }

  public clear() {
    this.events = [];
    this.render();
  }

  private render() {
    if (!this.container || !this.countEl) return;
    this.countEl.textContent = `${this.events.length} mục`;
    this.container.innerHTML = '';

    this.events.slice(0, 30).forEach(ev => {
      const item = document.createElement('div');
      item.className = 'board-log-item';
      item.style.borderLeftColor = ev.color;

      const header = document.createElement('div');
      header.className = 'board-log-header';

      const who = document.createElement('span');
      who.style.color = ev.color;
      who.style.fontWeight = '700';
      who.textContent = `${ev.badge} ${ev.agent}`;

      const time = document.createElement('span');
      time.textContent = ev.time;

      header.appendChild(who);
      header.appendChild(time);

      const body = document.createElement('div');
      body.className = 'board-log-detail';
      body.textContent = ev.detail.length > 180 ? ev.detail.slice(0, 179) + '…' : ev.detail;

      item.appendChild(header);
      item.appendChild(body);

      item.addEventListener('click', () => {
        if (this.onEventClick) this.onEventClick(ev.agent);
      });

      this.container.appendChild(item);
    });
  }

  private formatTime(ts: string): string {
    try {
      if (!ts) return '';
      if (/^\d{4}-\d{2}-\d{2}/.test(ts)) {
        const d = new Date(ts);
        return isNaN(d.getTime()) ? ts : d.toTimeString().split(' ')[0];
      }
      return ts;
    } catch {
      return '';
    }
  }
}
