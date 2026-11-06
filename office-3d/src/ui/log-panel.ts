import { OfficeEvent } from '../events/bridge.js';
import { ROLES } from '../agents/roles.js';
import * as THREE from 'three';

export class LogPanel {
  private container: HTMLElement;
  private countEl: HTMLElement;
  private events: OfficeEvent[] = [];
  public onEventClick?: (agentId: string) => void;

  constructor() {
    this.container = document.getElementById('log-list')!;
    this.countEl = document.getElementById('log-count')!;
  }

  public addEvent(ev: OfficeEvent) {
    this.events.unshift(ev);
    if (this.events.length > 50) this.events.pop();

    this.render();
  }

  public clear() {
    this.events = [];
    this.render();
  }

  private render() {
    this.countEl.textContent = `${this.events.length} sự kiện`;
    this.container.innerHTML = '';

    const displayEvents = this.events.slice(0, 14);

    displayEvents.forEach(ev => {
      const role = ROLES[ev.agent];
      const roleColor = role ? role.color : '#8DA2B3';
      const roleBadge = role ? role.badge : '●';
      const roleName = role ? role.shortName : ev.agent;

      const timeStr = this.formatTime(ev.ts);

      const item = document.createElement('div');
      item.className = `log-item ${ev.inferred ? 'inferred' : ''}`;
      item.style.borderLeftColor = roleColor;

      item.innerHTML = `
        <div class="log-item-header">
          <span style="color: ${roleColor}; font-weight:700;">${roleBadge} ${roleName}</span>
          <span>${timeStr}</span>
        </div>
        <div class="log-item-detail">${ev.detail || ev.event}</div>
      `;

      item.addEventListener('click', () => {
        if (this.onEventClick) {
          this.onEventClick(ev.agent);
        }
      });

      this.container.appendChild(item);
    });
  }

  private formatTime(ts: string): string {
    try {
      const date = new Date(ts);
      return date.toTimeString().split(' ')[0];
    } catch {
      return '';
    }
  }
}

