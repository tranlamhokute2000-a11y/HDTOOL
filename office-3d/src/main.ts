import * as THREE from 'three';
import { OfficeLights } from './scene/lights.js';
import { OfficeArchitecture } from './scene/office.js';
import { OfficeProps } from './scene/props.js';
import { CentralModelTable } from './scene/model-table.js';
import { ROLES, ZONES } from './agents/roles.js';
import { AgentRig } from './agents/rig.js';
import { AgentAnimator } from './agents/anim.js';
import { CameraManager } from './ui/camera.js';
import { OfficeDirector } from './events/director.js';
import { LogPanel } from './ui/log-panel.js';
import { AgentCardUI } from './ui/agent-card.js';
import { OfficeHUD } from './ui/hud.js';
import { EventBridge, OfficeEvent, SwarmSnapshot } from './events/bridge.js';
import { DemoRunner } from './events/demo.js';

class App {
  private scene: THREE.Scene;
  private camera: THREE.PerspectiveCamera;
  private renderer: THREE.WebGLRenderer;

  private lights: OfficeLights;
  private architecture: OfficeArchitecture;
  private props: OfficeProps;
  private modelTable: CentralModelTable;

  private agents: Map<string, { rig: AgentRig; anim: AgentAnimator }> = new Map();
  private cameraManager: CameraManager;
  private director: OfficeDirector;
  private logPanel: LogPanel;
  private agentCard: AgentCardUI;
  private hud: OfficeHUD;
  private bridge: EventBridge;
  private demo: DemoRunner;

  private clock = new THREE.Clock();
  private raycaster = new THREE.Raycaster();
  private mouse = new THREE.Vector2();

  private activeSourceId = '';
  private latestSnapshot: SwarmSnapshot | null = null;
  private allHistoryEvents: OfficeEvent[] = [];
  private selectedAgentId: string | null = null;

  constructor() {
    const container = document.getElementById('canvas-container')!;

    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 0.1, 220);

    this.renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(this.renderer.domElement);

    this.lights = new OfficeLights(this.scene);
    this.architecture = new OfficeArchitecture();
    this.scene.add(this.architecture.group);

    this.props = new OfficeProps();
    this.scene.add(this.props.group);

    this.modelTable = new CentralModelTable();
    this.modelTable.setStageUnlocked('code');
    this.scene.add(this.modelTable.group);

    // Instantiate all office & swarm agent desks cleanly
    Object.values(ROLES).forEach(role => {
      const rig = new AgentRig(role);
      const anim = new AgentAnimator(rig);
      this.agents.set(role.id, { rig, anim });
      this.scene.add(rig.group);
    });

    this.cameraManager = new CameraManager(this.camera, this.renderer.domElement);
    this.logPanel = new LogPanel();
    this.agentCard = new AgentCardUI();
    this.hud = new OfficeHUD();

    this.director = new OfficeDirector(
      this.scene,
      this.agents,
      this.architecture,
      this.props,
      this.modelTable
    );

    this.director.onPlaySound = (type) => this.hud.playProceduralSound(type);
    this.director.onAgentStatusChanged = (working, waiting, enabled, list) => {
      this.hud.updateAgentCounters(working, waiting, enabled);
      this.hud.updateAgentRoster(list);
      if (this.selectedAgentId) {
        this.refreshSelectedAgentCard(this.selectedAgentId);
      }
    };

    this.bridge = new EventBridge();
    this.demo = new DemoRunner((ev) => this.processEvent(ev));
    this.demo.onComplete = () => {
      this.hud.setHeaderMeta('replay', 'HOÀN TẤT PHÁT LẠI');
    };

    this.setupInteractions();
    this.setupEventListeners();
    this.initPipeline();
    this.director.notifyStatusChanged();

    this.animate();
  }

  private focusAgent(agentId: string) {
    const agent = this.agents.get(agentId);
    if (!agent) return;
    this.selectedAgentId = agentId;
    this.cameraManager.flyTo(agent.rig.group.position);
    this.hud.setActiveZoneTab(agent.rig.config.zone);
    this.refreshSelectedAgentCard(agentId);
  }

  private refreshSelectedAgentCard(agentId: string) {
    const agent = this.agents.get(agentId);
    if (!agent) return;
    const tel = this.director.telemetryByWorker.get(agentId);
    this.agentCard.show(
      agentId,
      agent.rig.config,
      agent.rig.currentTask,
      tel,
      this.allHistoryEvents
    );
  }

  private setupInteractions() {
    this.hud.onZoneSelect = (zone) => {
      this.cameraManager.focusZone(zone);
    };

    this.hud.onZoomIn = () => this.cameraManager.zoomIn();
    this.hud.onZoomOut = () => this.cameraManager.zoomOut();
    this.hud.onResetView = () => {
      this.selectedAgentId = null;
      this.agentCard.hide();
      this.cameraManager.focusZone(ZONES[0]);
    };

    this.hud.onWalkToggle = (enabled) => {
      this.cameraManager.setWalkMode(enabled);
    };

    this.hud.onLowGraphicsToggle = (low) => {
      this.architecture.setLowGraphics(low);
      this.renderer.shadowMap.enabled = !low;
      this.renderer.setPixelRatio(low ? 1 : Math.min(window.devicePixelRatio, 2));
    };

    this.hud.onDirectorToggle = (enabled) => {
      this.cameraManager.isDirectorMode = enabled;
    };

    this.hud.onTimeOfDayChange = (tod) => {
      this.lights.setTimeOfDay(tod, this.scene);
    };

    this.hud.onFlyToAgent = (agentId) => {
      this.focusAgent(agentId);
    };

    this.logPanel.onEventClick = (agentId) => {
      this.focusAgent(agentId);
    };

    this.hud.onToolChange = (sourceId) => {
      this.switchSource(sourceId);
    };

    this.hud.onUserConfirmReply = (detail) => {
      this.bridge.sendUserReply(this.activeSourceId, detail);
    };

    // Replay Swarm History in 3D animation!
    this.hud.onReplayClick = () => {
      this.startReplayCurrentSource();
    };

    this.hud.onBackLiveClick = () => {
      this.demo.stop();
      this.hud.showReplayBar(false);
      this.switchSource(this.activeSourceId);
    };

    this.hud.onReplayPlayPause = (paused) => {
      if (paused) this.demo.stop();
      else this.demo.start(1.0, false);
    };

    this.hud.onReplaySpeedChange = (speed) => {
      this.demo.setSpeed(speed);
    };

    // 3D Raycast click on workers
    let downX = 0;
    let downY = 0;
    this.renderer.domElement.addEventListener('pointerdown', (e) => {
      downX = e.clientX;
      downY = e.clientY;
    });

    this.renderer.domElement.addEventListener('click', (e) => {
      if (Math.hypot(e.clientX - downX, e.clientY - downY) > 6) return;

      this.mouse.x = (e.clientX / window.innerWidth) * 2 - 1;
      this.mouse.y = -(e.clientY / window.innerHeight) * 2 + 1;

      this.raycaster.setFromCamera(this.mouse, this.camera);
      const agentRoots = Array.from(this.agents.values()).map(a => a.rig.group);
      const intersects = this.raycaster.intersectObjects(agentRoots, true);

      if (intersects.length > 0) {
        let obj: THREE.Object3D | null = intersects[0].object;
        while (obj && !obj.name.startsWith('agent_') && obj.parent) {
          obj = obj.parent;
        }
        if (obj && obj.name.startsWith('agent_')) {
          const agentId = obj.name.replace('agent_', '');
          this.focusAgent(agentId);
        }
      }
    });

    window.addEventListener('resize', () => {
      this.camera.aspect = window.innerWidth / window.innerHeight;
      this.camera.updateProjectionMatrix();
      this.renderer.setSize(window.innerWidth, window.innerHeight);
    });
  }

  private setupEventListeners() {
    this.bridge.onEvent((ev) => {
      this.processEvent(ev);
    });

    this.bridge.onSwarmSnapshot((snapshot) => {
      if (!this.activeSourceId.startsWith('swarm:')) return;
      this.latestSnapshot = snapshot;
      this.director.applySwarmSnapshot(snapshot, true);
      this.logPanel.setBoardMessages(snapshot.board);
      this.hud.setHeaderMeta(
        'live',
        snapshot.swarmCompleted ? 'HOÀN TẤT' : 'ĐANG CHẠY',
        `${snapshot.title} · Chọn nhân vật để xem việc đang làm & Hội đồng Swarm.`
      );
    });
  }

  private async initPipeline() {
    const sources = await this.bridge.fetchSources();
    if (sources.length > 0) {
      this.hud.setSourcesList(sources);
      await this.switchSource(sources[0].id);
    } else {
      await this.switchSource('demo:office');
    }

    // Periodically refresh available sources every 12s so newly started Swarms appear automatically
    setInterval(async () => {
      const fresh = await this.bridge.fetchSources();
      if (fresh.length > 0) {
        const selectEl = document.getElementById('tool-select') as HTMLSelectElement;
        const currentVal = selectEl?.value;
        this.hud.setSourcesList(fresh);
        if (currentVal && Array.from(selectEl.options).some(o => o.value === currentVal)) {
          selectEl.value = currentVal;
        }
      }
    }, 12000);
  }

  private async switchSource(sourceId: string) {
    this.activeSourceId = sourceId;
    this.demo.stop();
    this.hud.showReplayBar(false);
    this.logPanel.clear();
    this.allHistoryEvents = [];
    this.director.resetAllAgentsToIdle();

    if (sourceId.startsWith('swarm:')) {
      const snapshot = await this.bridge.fetchSwarmSnapshot(sourceId);
      if (snapshot) {
        this.latestSnapshot = snapshot;
        this.director.applySwarmSnapshot(snapshot, false);
        this.logPanel.setBoardMessages(snapshot.board);
        this.hud.setHeaderMeta(
          'live',
          snapshot.swarmCompleted ? 'HOÀN TẤT' : 'ĐANG CHẠY',
          `${snapshot.title} · Theo dõi các Worker Swarm theo thời gian thực.`
        );
      }
      this.bridge.connect(sourceId);
      return;
    }

    this.latestSnapshot = null;
    if (sourceId === 'demo:office') {
      this.hud.setHeaderMeta('demo', 'DEMO 12 AGENT', 'Đang chạy kịch bản mô phỏng 12 kỹ sư Revit phát triển công cụ.');
      this.hud.showReplayBar(true);
      const loaded = await this.demo.loadEvents('demo:office');
      if (loaded) this.demo.start(1.0, true);
      return;
    }

    const { events } = await this.bridge.fetchHistory(sourceId);
    if (events.length > 0) {
      this.hud.setHeaderMeta('live', 'TRỰC TIẾP', `Đang theo dõi luồng sự kiện tool: ${sourceId}`);
      events.forEach(ev => {
        this.director.handleEvent(ev, true);
        this.logPanel.addEvent(ev);
        this.allHistoryEvents.push(ev);
      });
    } else {
      this.hud.setHeaderMeta('demo', 'CHỜ SỰ KIỆN', `Tool ${sourceId} chưa có events mới.`);
    }
    this.bridge.connect(sourceId);
  }

  private async startReplayCurrentSource() {
    this.demo.stop();
    this.director.resetAllAgentsToIdle();
    this.logPanel.clear();
    this.allHistoryEvents = [];
    this.hud.showReplayBar(true);

    if (this.latestSnapshot) {
      const events = EventBridge.buildReplayEventsFromSwarm(this.latestSnapshot);
      this.hud.setHeaderMeta('replay', 'PHÁT LẠI SWARM', `Đang phát lại ${events.length} bước phối hợp của đội Worker.`);
      this.demo.setEvents(events);
      this.demo.start(1.0, false);
    } else {
      const loaded = await this.demo.loadEvents(this.activeSourceId || 'demo:office');
      this.hud.setHeaderMeta('replay', 'PHÁT LẠI', 'Đang phát lại luồng công việc 3D từng bước.');
      if (loaded) this.demo.start(1.0, false);
    }
  }

  private processEvent(ev: OfficeEvent) {
    this.allHistoryEvents.push(ev);
    this.director.handleEvent(ev, false);
    this.logPanel.addEvent(ev);
    this.hud.setStage(this.director.currentStage);

    const agent = this.agents.get(ev.agent);
    if (agent) {
      this.cameraManager.trackAgent(agent.rig.group.position);
    }

    if (ev.event === 'ask_user') {
      this.hud.showAskUser(ev.detail);
    } else if (ev.event === 'user_reply') {
      this.hud.hideAskUser();
    }
  }

  private animate = () => {
    requestAnimationFrame(this.animate);
    if (document.hidden) return;

    const delta = Math.min(this.clock.getDelta(), 0.1);
    this.agents.forEach(a => a.anim.update(delta));
    this.director.update(delta);
    this.modelTable.update(delta);
    this.cameraManager.update(delta);

    this.renderer.render(this.scene, this.camera);
  };
}

window.addEventListener('DOMContentLoaded', () => {
  new App();
});
