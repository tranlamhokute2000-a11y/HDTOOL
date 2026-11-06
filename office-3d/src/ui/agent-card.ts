import { ROLES, RoleConfig } from '../agents/roles.js';
import { OfficeEvent } from '../events/bridge.js';

export class AgentCardUI {
  private cardEl: HTMLElement;
  private nameEl: HTMLElement;
  private roleEl: HTMLElement;
  private actionEl: HTMLElement;
  private recentListEl: HTMLElement;

  constructor() {
    this.cardEl = document.getElementById('agent-card')!;
    this.nameEl = document.getElementById('card-agent-name')!;
    this.roleEl = document.getElementById('card-agent-role')!;
    this.actionEl = document.getElementById('card-agent-action')!;
    this.recentListEl = document.getElementById('card-recent-events')!;

    const closeBtn = document.getElementById('btn-close-card');
    if (closeBtn) {
      closeBtn.addEventListener('click', () => this.hide());
    }
  }

  public show(agentId: string, currentAction: string, recentEvents: OfficeEvent[] = []) {
    const role: RoleConfig | undefined = ROLES[agentId];
    if (!role) return;

    this.nameEl.textContent = `${role.badge} ${role.shortName}`;
    this.nameEl.style.color = role.color;
    this.roleEl.textContent = role.name;
    this.actionEl.textContent = currentAction || 'Đang sẵn sàng tại bàn làm việc';

    this.recentListEl.innerHTML = '';
    const agentEvents = recentEvents.filter(e => e.agent === agentId).slice(0, 5);
    if (agentEvents.length === 0) {
      this.recentListEl.innerHTML = '<span style="color:#64748B;">Chưa có sự kiện gần đây</span>';
    } else {
      agentEvents.forEach(e => {
        const row = document.createElement('div');
        row.style.cssText = 'color:#CBD5E1; line-height:1.3; border-bottom:1px solid rgba(255,255,255,0.08); padding-bottom:3px;';
        row.textContent = `• ${e.detail}`;
        this.recentListEl.appendChild(row);
      });
    }

    this.cardEl.style.display = 'flex';
  }

  public hide() {
    this.cardEl.style.display = 'none';
  }
}

