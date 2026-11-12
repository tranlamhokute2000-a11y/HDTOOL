import { AgentStatusInfo } from '../events/director.js';
import { SourceItem } from '../events/bridge.js';
import { TimeOfDay } from '../scene/lights.js';
import { ZoneConfig, ZONES } from '../agents/roles.js';

export class OfficeHUD {
  public onToolChange?: (toolId: string) => void;
  public onTimeOfDayChange?: (tod: TimeOfDay) => void;
  public onDirectorToggle?: (enabled: boolean) => void;
  public onLowGraphicsToggle?: (enabled: boolean) => void;
  public onWalkToggle?: (enabled: boolean) => void;
  public onZoneSelect?: (zone: ZoneConfig) => void;
  public onZoomIn?: () => void;
  public onZoomOut?: () => void;
  public onResetView?: () => void;
  public onReplayClick?: () => void;
  public onBackLiveClick?: () => void;
  public onReplayPlayPause?: (paused: boolean) => void;
  public onReplaySpeedChange?: (speed: number) => void;
  public onUserConfirmReply?: (detail: string) => void;
  public onFlyToAgent?: (agentId: string) => void;

  private todBtn: HTMLButtonElement;
  private lowGfxBtn: HTMLButtonElement;
  private directorBtn: HTMLButtonElement;
  private walkBtn: HTMLButtonElement;
  private replayBtn: HTMLButtonElement;
  private toolSelect: HTMLSelectElement;
  private stageEl: HTMLElement;
  private timerEl: HTMLElement;
  private subtitleEl: HTMLElement;
  private navHintEl: HTMLElement;

  private askBanner: HTMLElement;
  private askDetailEl: HTMLElement;
  private confirmBtn: HTMLButtonElement;
  private countWorkingEl: HTMLElement;
  private countWaitingEl: HTMLElement;
  private countEnabledEl: HTMLElement;

  private rosterPanel: HTMLElement;
  private rosterListEl: HTMLElement;
  private logListEl: HTMLElement;
  private tabWorkersBtn: HTMLButtonElement;
  private tabBoardBtn: HTMLButtonElement;

  private replayBar: HTMLElement;
  private playPauseBtn: HTMLButtonElement;
  private speedBtn: HTMLButtonElement;

  private currentTod: TimeOfDay = 'day';
  private isLowGraphics = false;
  private isWalkMode = false;
  private isReplayPaused = false;
  private replaySpeed = 1;
  private sessionSeconds = 0;

  public isAudioEnabled = false;
  private audioCtx: AudioContext | null = null;

  constructor() {
    this.todBtn = document.getElementById('btn-tod') as HTMLButtonElement;
    this.lowGfxBtn = document.getElementById('btn-low-gfx') as HTMLButtonElement;
    this.directorBtn = document.getElementById('btn-director') as HTMLButtonElement;
    this.walkBtn = document.getElementById('btn-walk') as HTMLButtonElement;
    this.replayBtn = document.getElementById('btn-replay-swarm') as HTMLButtonElement;
    this.toolSelect = document.getElementById('tool-select') as HTMLSelectElement;
    this.stageEl = document.getElementById('current-stage')!;
    this.timerEl = document.getElementById('session-timer')!;
    this.subtitleEl = document.getElementById('header-subtitle')!;
    this.navHintEl = document.getElementById('nav-hint')!;

    this.askBanner = document.getElementById('ask-user-banner')!;
    this.askDetailEl = document.getElementById('ask-user-detail')!;
    this.confirmBtn = document.getElementById('btn-confirm-reply') as HTMLButtonElement;

    this.countWorkingEl = document.getElementById('counter-working')!;
    this.countWaitingEl = document.getElementById('counter-waiting')!;
    this.countEnabledEl = document.getElementById('counter-enabled')!;

    this.rosterPanel = document.getElementById('roster-panel')!;
    this.rosterListEl = document.getElementById('roster-list')!;
    this.logListEl = document.getElementById('log-list')!;
    this.tabWorkersBtn = document.getElementById('tab-btn-workers') as HTMLButtonElement;
    this.tabBoardBtn = document.getElementById('tab-btn-board') as HTMLButtonElement;

    this.replayBar = document.getElementById('replay-bar')!;
    this.playPauseBtn = document.getElementById('btn-play-pause') as HTMLButtonElement;
    this.speedBtn = document.getElementById('btn-speed') as HTMLButtonElement;

    this.renderZoneTabs();
    this.bindEvents();

    setInterval(() => {
      this.sessionSeconds++;
      const hrs = Math.floor(this.sessionSeconds / 3600).toString().padStart(2, '0');
      const mins = Math.floor((this.sessionSeconds % 3600) / 60).toString().padStart(2, '0');
      const secs = (this.sessionSeconds % 60).toString().padStart(2, '0');
      this.timerEl.textContent = `${hrs}:${mins}:${secs}`;
    }, 1000);
  }

  private renderZoneTabs() {
    const container = document.getElementById('zone-tabs');
    if (!container) return;
    container.innerHTML = '';

    ZONES.forEach((zone, idx) => {
      const btn = document.createElement('button');
      btn.className = `zone-tab ${idx === 0 ? 'active' : ''}`;
      btn.textContent = zone.label;
      btn.setAttribute('data-zone', zone.id);
      btn.addEventListener('click', () => {
        this.setActiveZoneTab(zone.id);
        if (this.onZoneSelect) this.onZoneSelect(zone);
      });
      container.appendChild(btn);
    });
  }

  public setActiveZoneTab(zoneId: string) {
    document.querySelectorAll('.zone-tab').forEach(el => {
      el.classList.toggle('active', el.getAttribute('data-zone') === zoneId);
    });
  }

  private bindEvents() {
    // Drawer toggle & counters
    const toggleBtn = document.getElementById('btn-toggle-roster');
    const closeBtn = document.getElementById('btn-close-roster');
    const openDrawer = () => this.rosterPanel.classList.remove('hidden');

    if (toggleBtn) toggleBtn.addEventListener('click', () => this.rosterPanel.classList.toggle('hidden'));
    if (closeBtn) closeBtn.addEventListener('click', () => this.rosterPanel.classList.add('hidden'));

    ['badge-working', 'badge-waiting', 'badge-enabled'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.addEventListener('click', () => {
        this.switchDrawerTab('workers');
        openDrawer();
      });
    });

    this.tabWorkersBtn.addEventListener('click', () => this.switchDrawerTab('workers'));
    this.tabBoardBtn.addEventListener('click', () => this.switchDrawerTab('board'));

    // Low Graphics
    this.lowGfxBtn.addEventListener('click', () => {
      this.isLowGraphics = !this.isLowGraphics;
      this.lowGfxBtn.classList.toggle('active', this.isLowGraphics);
      this.lowGfxBtn.textContent = this.isLowGraphics ? '🧊 Đồ họa nhẹ: BẬT' : '🧊 Đồ họa nhẹ';
      if (this.onLowGraphicsToggle) this.onLowGraphicsToggle(this.isLowGraphics);
    });

    // Director Auto-Follow
    this.directorBtn.addEventListener('click', () => {
      const active = this.directorBtn.classList.toggle('active');
      this.directorBtn.textContent = active ? '👁️ Theo dõi tự động: BẬT' : '👁️ Xem chuyển động';
      if (this.onDirectorToggle) this.onDirectorToggle(active);
    });

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

    // Walk Mode (WASD)
    this.walkBtn.addEventListener('click', () => {
      this.setWalkModeUI(!this.isWalkMode);
      if (this.onWalkToggle) this.onWalkToggle(this.isWalkMode);
    });

    // Replay Swarm button
    this.replayBtn.addEventListener('click', () => {
      if (this.onReplayClick) this.onReplayClick();
    });

    // Zoom toolbar
    document.getElementById('btn-zoom-in')?.addEventListener('click', () => this.onZoomIn && this.onZoomIn());
    document.getElementById('btn-zoom-out')?.addEventListener('click', () => this.onZoomOut && this.onZoomOut());
    document.getElementById('btn-cam-reset')?.addEventListener('click', () => {
      this.setActiveZoneTab('all');
      this.setWalkModeUI(false);
      if (this.onResetView) this.onResetView();
    });
    document.getElementById('btn-fullscreen')?.addEventListener('click', () => {
      if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen?.().catch(() => { });
      } else {
        document.exitFullscreen?.().catch(() => { });
      }
    });

    // Replay Bar controls
    this.playPauseBtn.addEventListener('click', () => {
      this.isReplayPaused = !this.isReplayPaused;
      this.playPauseBtn.textContent = this.isReplayPaused ? '▶ Tiếp tục' : '⏸ Tạm dừng';
      if (this.onReplayPlayPause) this.onReplayPlayPause(this.isReplayPaused);
    });

    this.speedBtn.addEventListener('click', () => {
      this.replaySpeed = this.replaySpeed === 1 ? 2 : this.replaySpeed === 2 ? 4 : 1;
      this.speedBtn.textContent = `x${this.replaySpeed}`;
      if (this.onReplaySpeedChange) this.onReplaySpeedChange(this.replaySpeed);
    });

    document.getElementById('btn-back-live')?.addEventListener('click', () => {
      this.showReplayBar(false);
      if (this.onBackLiveClick) this.onBackLiveClick();
    });

    // Source selector
    this.toolSelect.addEventListener('change', () => {
      if (this.onToolChange) this.onToolChange(this.toolSelect.value);
    });

    // Confirm banner
    this.confirmBtn.addEventListener('click', () => {
      this.hideAskUser();
      if (this.onUserConfirmReply) {
        this.onUserConfirmReply('Chủ dự án (Sếp Hồ) đã phê duyệt xác nhận');
      }
    });
  }

  public setWalkModeUI(enabled: boolean) {
    this.isWalkMode = enabled;
    this.walkBtn.classList.toggle('active', enabled);
    this.walkBtn.textContent = enabled ? '🚶 Thoát đi bộ' : '🚶 Đi bộ (WASD)';
    this.navHintEl.textContent = enabled
      ? 'Nhấn W A S D hoặc phím mũi tên để đi bộ · Kéo chuột để nhìn · Giữ Shift đi nhanh'
      : 'Kéo để xoay 360° · cuộn để phóng to · chuột phải để trượt';
  }

  public switchDrawerTab(tab: 'workers' | 'board') {
    const isWorkers = tab === 'workers';
    this.tabWorkersBtn.classList.toggle('active', isWorkers);
    this.tabBoardBtn.classList.toggle('active', !isWorkers);
    this.rosterListEl.style.display = isWorkers ? 'flex' : 'none';
    this.logListEl.style.display = isWorkers ? 'none' : 'flex';
  }

  public showReplayBar(visible: boolean) {
    this.replayBar.style.display = visible ? 'flex' : 'none';
    this.replayBtn.classList.toggle('active', visible);
    this.replayBtn.textContent = visible ? '⏹ Đang phát quy trình' : '▶ Xem lại quy trình';
    if (!visible) {
      this.isReplayPaused = false;
      this.playPauseBtn.textContent = '⏸ Tạm dừng';
    }
  }

  public setSourcesList(sources: SourceItem[]) {
    this.toolSelect.innerHTML = '';
    sources.forEach(s => {
      const opt = document.createElement('option');
      opt.value = s.id;
      opt.textContent = s.name;
      this.toolSelect.appendChild(opt);
    });
  }

  public setHeaderMeta(statusMode: 'live' | 'replay' | 'demo', stageText: string, subtitle?: string) {
    const st = document.getElementById('status-text');
    if (st) {
      if (statusMode === 'live') st.textContent = 'HADOMEE AGENT WORKSPACE · TRỰC TIẾP';
      else if (statusMode === 'replay') st.textContent = 'HADOMEE WORKSPACE · PHÁT LẠI QUY TRÌNH SWARM';
      else st.textContent = 'HADOMEE WORKSPACE · MÔ PHỎNG DEMO';
    }
    this.stageEl.textContent = stageText;
    if (subtitle) this.subtitleEl.textContent = subtitle;
  }

  public setStage(stage: string) {
    this.stageEl.textContent = stage.toUpperCase();
  }

  public updateAgentCounters(working: number, waiting: number, enabled: number) {
    this.countWorkingEl.textContent = working.toString();
    this.countWaitingEl.textContent = waiting.toString();
    this.countEnabledEl.textContent = enabled.toString();
  }

  public updateAgentRoster(list: AgentStatusInfo[]) {
    this.rosterListEl.innerHTML = '';

    list.forEach(a => {
      const card = document.createElement('div');
      card.className = `worker-card ${a.status === 'WORK' ? 'is-working' : ''}`;

      const top = document.createElement('div');
      top.className = 'worker-card-top';

      const name = document.createElement('div');
      name.className = 'worker-card-name';
      name.innerHTML = `<span style="color:${a.color}">${a.badge}</span> ${a.shortName}`;

      const pill = document.createElement('span');
      pill.className = `card-status-pill pill-${a.status.toLowerCase()}`;
      pill.textContent =
        a.status === 'WORK' ? '● Đang làm' :
        a.status === 'WAIT' ? '● Chờ xử lý' :
        a.status === 'FAIL' ? '● Lỗi' : '● Sẵn sàng';

      top.appendChild(name);
      top.appendChild(pill);

      const meta = document.createElement('div');
      meta.className = 'worker-meta-row';
      meta.innerHTML = `<span class="zone-pill-small">${a.zoneLabel}</span><span>${a.model}</span>`;

      const task = document.createElement('div');
      task.className = 'worker-task-line';
      task.textContent = a.task;

      const flyBtn = document.createElement('button');
      flyBtn.className = 'worker-fly-btn';
      flyBtn.textContent = '👁️ Zoom tới vị trí';
      flyBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        if (this.onFlyToAgent) this.onFlyToAgent(a.id);
      });

      card.appendChild(top);
      card.appendChild(meta);
      card.appendChild(task);
      card.appendChild(flyBtn);

      card.addEventListener('click', () => {
        if (this.onFlyToAgent) this.onFlyToAgent(a.id);
      });

      this.rosterListEl.appendChild(card);
    });
  }

  public showAskUser(detail: string) {
    this.askDetailEl.textContent = detail;
    this.askBanner.style.display = 'flex';
  }

  public hideAskUser() {
    this.askBanner.style.display = 'none';
  }

  public playProceduralSound(type: 'type' | 'ting' | 'thud') {
    if (!this.isAudioEnabled) return;
    try {
      if (!this.audioCtx) {
        this.audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
      }
      const ctx = this.audioCtx;
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain);
      gain.connect(ctx.destination);

      if (type === 'type') {
        osc.type = 'triangle';
        osc.frequency.setValueAtTime(650 + Math.random() * 180, ctx.currentTime);
        gain.gain.setValueAtTime(0.03, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.05);
        osc.start();
        osc.stop(ctx.currentTime + 0.05);
      } else if (type === 'ting') {
        osc.type = 'sine';
        osc.frequency.setValueAtTime(880, ctx.currentTime);
        osc.frequency.exponentialRampToValueAtTime(1760, ctx.currentTime + 0.14);
        gain.gain.setValueAtTime(0.08, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.4);
        osc.start();
        osc.stop(ctx.currentTime + 0.4);
      } else {
        osc.type = 'sine';
        osc.frequency.setValueAtTime(140, ctx.currentTime);
        gain.gain.setValueAtTime(0.12, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.18);
        osc.start();
        osc.stop(ctx.currentTime + 0.18);
      }
    } catch { }
  }
}
