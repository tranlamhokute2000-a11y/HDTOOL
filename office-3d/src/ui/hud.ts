import { AgentStatusInfo } from '../events/director.js';
import { TimeOfDay } from '../scene/lights.js';

export class OfficeHUD {
  public onToolChange?: (toolId: string) => void;
  public onTimeOfDayChange?: (tod: TimeOfDay) => void;
  public onDirectorToggle?: (enabled: boolean) => void;
  public onUserConfirmReply?: (detail: string) => void;
  public onFlyToAgent?: (agentId: string) => void;

  private todBtn: HTMLButtonElement;
  private directorBtn: HTMLButtonElement;
  private audioBtn: HTMLButtonElement;
  private toolSelect: HTMLSelectElement;
  private stageEl: HTMLElement;
  private timerEl: HTMLElement;

  private askBanner: HTMLElement;
  private askDetailEl: HTMLElement;
  private confirmBtn: HTMLButtonElement;
  private countWorkingEl: HTMLElement;
  private countIdleEl: HTMLElement;
  private rosterPanel: HTMLElement;
  private rosterListEl: HTMLElement;
  private toggleRosterBtn: HTMLButtonElement;
  private closeRosterBtn: HTMLButtonElement;

  private currentTod: TimeOfDay = 'day';
  public isAudioEnabled = false;
  private audioCtx: AudioContext | null = null;
  private sessionSeconds = 0;

  constructor() {
    this.todBtn = document.getElementById('btn-tod') as HTMLButtonElement;
    this.directorBtn = document.getElementById('btn-director') as HTMLButtonElement;
    this.audioBtn = document.getElementById('btn-audio') as HTMLButtonElement;
    this.toolSelect = document.getElementById('tool-select') as HTMLSelectElement;
    this.stageEl = document.getElementById('current-stage')!;
    this.timerEl = document.getElementById('session-timer')!;

    this.askBanner = document.getElementById('ask-user-banner')!;
    this.askDetailEl = document.getElementById('ask-user-detail')!;
    this.confirmBtn = document.getElementById('btn-confirm-reply') as HTMLButtonElement;
    this.countWorkingEl = document.getElementById('counter-working')!;
    this.countIdleEl = document.getElementById('counter-idle')!;
    this.rosterPanel = document.getElementById('roster-panel')!;
    this.rosterListEl = document.getElementById('roster-list')!;
    this.toggleRosterBtn = document.getElementById('btn-toggle-roster') as HTMLButtonElement;
    this.closeRosterBtn = document.getElementById('btn-close-roster') as HTMLButtonElement;

    // Toggle Roster Drawer
    if (this.toggleRosterBtn && this.rosterPanel) {
      this.toggleRosterBtn.addEventListener('click', () => {
        this.rosterPanel.classList.toggle('hidden');
      });
    }
    if (this.closeRosterBtn && this.rosterPanel) {
      this.closeRosterBtn.addEventListener('click', () => {
        this.rosterPanel.classList.add('hidden');
      });
    }
    const badgeWork = document.getElementById('badge-working');
    if (badgeWork && this.rosterPanel) {
      badgeWork.addEventListener('click', () => {
        this.rosterPanel.classList.remove('hidden');
      });
    }
    const badgeIdle = document.getElementById('badge-idle');
    if (badgeIdle && this.rosterPanel) {
      badgeIdle.addEventListener('click', () => {
        this.rosterPanel.classList.remove('hidden');
      });
    }

    // Time of Day
    this.todBtn.addEventListener('click', () => {
      if (this.currentTod === 'day') {
        this.currentTod = 'sunset';
        this.todBtn.textContent = '🌅 Hoàng hôn';
      } else if (this.currentTod === 'sunset') {
        this.currentTod = 'night';
        this.todBtn.textContent = '🌙 Đêm';
      } else {
        this.currentTod = 'day';
        this.todBtn.textContent = '☀️ Ngày';
      }
      if (this.onTimeOfDayChange) this.onTimeOfDayChange(this.currentTod);
    });

    // Director Mode
    this.directorBtn.addEventListener('click', () => {
      const isActive = this.directorBtn.classList.toggle('active');
      this.directorBtn.textContent = isActive ? '🎬 Đạo diễn: BẬT' : '🎬 Đạo diễn: TẮT';
      if (this.onDirectorToggle) this.onDirectorToggle(isActive);
    });

    // Audio
    this.audioBtn.addEventListener('click', () => {
      this.isAudioEnabled = !this.isAudioEnabled;
      this.audioBtn.textContent = this.isAudioEnabled ? '🔊 Âm thanh' : '🔇 Tắt tiếng';
      if (this.isAudioEnabled && !this.audioCtx) {
        this.audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
      }
    });

    // Tool Selector
    this.toolSelect.addEventListener('change', () => {
      if (this.onToolChange) this.onToolChange(this.toolSelect.value);
    });

    // Ask User Confirmation
    this.confirmBtn.addEventListener('click', () => {
      this.hideAskUser();
      if (this.onUserConfirmReply) {
        this.onUserConfirmReply('Chủ dự án (Sếp Hồ) đã phê duyệt xác nhận');
      }
    });

    // Toggle Log Panel
    const toggleLogBtn = document.getElementById('btn-toggle-log');
    const logPanel = document.getElementById('log-panel');
    if (toggleLogBtn && logPanel) {
      toggleLogBtn.addEventListener('click', () => {
        logPanel.classList.toggle('collapsed');
      });
    }

    // Session Timer
    setInterval(() => {
      this.sessionSeconds++;
      const hrs = Math.floor(this.sessionSeconds / 3600).toString().padStart(2, '0');
      const mins = Math.floor((this.sessionSeconds % 3600) / 60).toString().padStart(2, '0');
      const secs = (this.sessionSeconds % 60).toString().padStart(2, '0');
      this.timerEl.textContent = `${hrs}:${mins}:${secs}`;
    }, 1000);
  }

  
  public updateAgentCounters(working: number, idle: number) {
    if (this.countWorkingEl) this.countWorkingEl.textContent = working.toString();
    if (this.countIdleEl) this.countIdleEl.textContent = idle.toString();
  }

  public updateAgentRoster(list: AgentStatusInfo[]) {
    if (!this.rosterListEl) return;
    this.rosterListEl.innerHTML = '';

    list.forEach(a => {
      const isWork = a.status === 'WORK';
      const isWait = a.status === 'WAIT';

      const card = document.createElement('div');
      card.className = `roster-card ${isWork ? 'working' : ''}`;

      const top = document.createElement('div');
      top.className = 'roster-card-top';

      const nameEl = document.createElement('div');
      nameEl.className = 'roster-agent-name';
      nameEl.textContent = `${a.badge} ${a.name}`;

      const tag = document.createElement('span');
      tag.className = `roster-tag ${isWork ? 'tag-work' : isWait ? 'tag-wait' : 'tag-idle'}`;
      tag.textContent = isWork ? '🟢 ĐANG LÀM' : isWait ? '⏳ CHỜ' : '💤 NGHỈ';

      top.appendChild(nameEl);
      top.appendChild(tag);

      const task = document.createElement('div');
      task.className = 'roster-task-detail';
      task.textContent = isWork ? a.task : 'Đang nghỉ ngơi, chờ lệnh mới';

      const btnZoom = document.createElement('button');
      btnZoom.className = 'btn-zoom-agent';
      btnZoom.textContent = '👁️ Đến bàn làm việc';
      btnZoom.addEventListener('click', (e) => {
        e.stopPropagation();
        if (this.onFlyToAgent) this.onFlyToAgent(a.id);
      });

      card.appendChild(top);
      card.appendChild(task);
      card.appendChild(btnZoom);

      card.addEventListener('click', () => {
        if (this.onFlyToAgent) this.onFlyToAgent(a.id);
      });

      this.rosterListEl.appendChild(card);
    });
  }

  public setToolsList(tools: Array<{ id: string; name: string }>) {
    this.toolSelect.innerHTML = '';
    tools.forEach(t => {
      const opt = document.createElement('option');
      opt.value = t.id;
      opt.textContent = t.name;
      this.toolSelect.appendChild(opt);
    });
  }

  public setStatus(status: 'live' | 'demo' | 'catching') {
    const badge = document.getElementById('status-badge')!;
    const text = document.getElementById('status-text')!;
    badge.className = `status-badge status-${status}`;
    if (status === 'live') text.textContent = 'TRỰC TIẾP';
    else if (status === 'demo') text.textContent = 'DEMO';
    else text.textContent = 'BẮT KỊP...';
  }

  public updateMetrics(round: number, pass: number, fail: number, block: number, bugs: number) {
    document.getElementById('metric-round')!.textContent = `${round}/2`;
    document.getElementById('metric-test-pass')!.textContent = pass.toString();
    document.getElementById('metric-test-fail')!.textContent = fail.toString();
    document.getElementById('metric-block')!.textContent = block.toString();
    document.getElementById('metric-bugs')!.textContent = bugs.toString();
  }

  public setStage(stage: string) {
    this.stageEl.textContent = stage.toUpperCase();
  }

  public showAskUser(detail: string) {
    this.askDetailEl.textContent = detail;
    this.askBanner.style.display = 'flex';
  }

  public hideAskUser() {
    this.askBanner.style.display = 'none';
  }

  public playProceduralSound(type: 'type' | 'ting' | 'thud') {
    if (!this.isAudioEnabled || !this.audioCtx) return;

    try {
      const ctx = this.audioCtx;
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain);
      gain.connect(ctx.destination);

      if (type === 'type') {
        // Soft click
        osc.type = 'triangle';
        osc.frequency.setValueAtTime(600 + Math.random() * 200, ctx.currentTime);
        gain.gain.setValueAtTime(0.04, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.05);
        osc.start();
        osc.stop(ctx.currentTime + 0.05);
      } else if (type === 'ting') {
        // Gate pass / Bell ting
        osc.type = 'sine';
        osc.frequency.setValueAtTime(880, ctx.currentTime);
        osc.frequency.exponentialRampToValueAtTime(1760, ctx.currentTime + 0.15);
        gain.gain.setValueAtTime(0.12, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.5);
        osc.start();
        osc.stop(ctx.currentTime + 0.5);
      } else if (type === 'thud') {
        // Stamp thud / Block issue
        osc.type = 'sine';
        osc.frequency.setValueAtTime(140, ctx.currentTime);
        osc.frequency.exponentialRampToValueAtTime(50, ctx.currentTime + 0.12);
        gain.gain.setValueAtTime(0.2, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.2);
        osc.start();
        osc.stop(ctx.currentTime + 0.2);
      }
    } catch { }
  }
}

