import { ROLES, RoleConfig } from '../agents/roles.js';
import { OfficeEvent, SwarmWorkerTelemetry } from '../events/bridge.js';

export class AgentCardUI {
  private cardEl: HTMLElement;
  private nameEl: HTMLElement;
  private roleEl: HTMLElement;
  private zoneBadgeEl: HTMLElement;
  private statusBadgeEl: HTMLElement;
  private actionEl: HTMLElement;
  private recentListEl: HTMLElement;

  constructor() {
    this.cardEl = document.getElementById('agent-card')!;
    this.nameEl = document.getElementById('card-agent-name')!;
    this.roleEl = document.getElementById('card-agent-role')!;
    this.zoneBadgeEl = document.getElementById('card-agent-zone')!;
    this.statusBadgeEl = document.getElementById('card-agent-status-pill')!;
    this.actionEl = document.getElementById('card-agent-action')!;
    this.recentListEl = document.getElementById('card-recent-events')!;

    const closeBtn = document.getElementById('btn-close-card');
    if (closeBtn) {
      closeBtn.addEventListener('click', () => this.hide());
    }
  }

  public show(
    agentId: string,
    roleCfg?: RoleConfig,
    currentAction?: string,
    telemetry?: SwarmWorkerTelemetry,
    recentEvents: OfficeEvent[] = []
  ) {
    const role = roleCfg || ROLES[agentId];
    const displayTitle = role ? `${role.badge} ${role.shortName}` : agentId;
    const displaySub = role ? role.name : 'Worker';
    const color = role ? role.color : '#2563EB';

    this.nameEl.textContent = displayTitle;
    this.nameEl.style.color = color;
    this.roleEl.textContent = telemetry && telemetry.model ? `${displaySub} · ${telemetry.model}` : displaySub;
    this.zoneBadgeEl.textContent = role ? role.zoneLabel : 'Phòng ban';

    const status = telemetry ? telemetry.status : 'IDLE';
    this.statusBadgeEl.className = `card-status-pill pill-${status.toLowerCase()}`;
    this.statusBadgeEl.textContent =
      status === 'WORK' ? '● Đang làm' :
      status === 'WAIT' ? '● Chờ xử lý' :
      status === 'FAIL' ? '● Gặp lỗi' : '● Sẵn sàng';

    this.actionEl.textContent =
      (telemetry && telemetry.currentAction) ||
      currentAction ||
      'Đang sẵn sàng tại bàn làm việc';

    this.recentListEl.innerHTML = '';

    // Combine real transcript actions + recent board/tool events
    const rows: string[] = [];
    if (telemetry && telemetry.recentActions && telemetry.recentActions.length > 0) {
      telemetry.recentActions.forEach(a => rows.push(a.summary));
    }
    if (telemetry && telemetry.boardPosts && telemetry.boardPosts.length > 0) {
      telemetry.boardPosts.forEach(p => rows.push(`💬 #${p.seq}: ${p.text.slice(0, 85)}${p.text.length > 85 ? '…' : ''}`));
    }
    if (rows.length === 0) {
      recentEvents
        .filter(e => e.agent === agentId)
        .slice(0, 6)
        .forEach(e => rows.push(`• ${e.detail}`));
    }

    if (rows.length === 0) {
      this.recentListEl.innerHTML = '<div class="empty-recent">Chưa ghi nhận hoạt động mới</div>';
    } else {
      rows.slice(0, 6).forEach(line => {
        const div = document.createElement('div');
        div.className = 'card-recent-row';
        div.textContent = line;
        this.recentListEl.appendChild(div);
      });
    }

    this.cardEl.style.display = 'flex';
  }

  public hide() {
    this.cardEl.style.display = 'none';
  }
}
