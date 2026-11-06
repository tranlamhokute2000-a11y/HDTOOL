import * as THREE from 'three';
import { OfficeLights } from './scene/lights.js';
import { OfficeArchitecture } from './scene/office.js';
import { OfficeProps } from './scene/props.js';
import { CentralModelTable } from './scene/model-table.js';
import { ROLES } from './agents/roles.js';
import { AgentRig } from './agents/rig.js';
import { AgentAnimator } from './agents/anim.js';
import { CameraManager } from './ui/camera.js';
import { OfficeDirector } from './events/director.js';
import { LogPanel } from './ui/log-panel.js';
import { AgentCardUI } from './ui/agent-card.js';
import { TimelineUI } from './ui/timeline.js';
import { OfficeHUD } from './ui/hud.js';
import { EventBridge, OfficeEvent } from './events/bridge.js';
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
  private timeline: TimelineUI;
  private hud: OfficeHUD;
  private bridge: EventBridge;
  private demo: DemoRunner;

  private clock = new THREE.Clock();
  private raycaster = new THREE.Raycaster();
  private mouse = new THREE.Vector2();
  private allHistoryEvents: OfficeEvent[] = [];

  constructor() {
    const container = document.getElementById('canvas-container')!;

    // 1. Scene & Camera
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x0F2236);

    this.camera = new THREE.PerspectiveCamera(
      45,
      window.innerWidth / window.innerHeight,
      0.1,
      150
    );

    // 2. Renderer
    this.renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(this.renderer.domElement);

    // 3. Scene Elements
    this.lights = new OfficeLights(this.scene);
    this.architecture = new OfficeArchitecture();
    this.scene.add(this.architecture.group);

    this.props = new OfficeProps();
    this.scene.add(this.props.group);

    this.modelTable = new CentralModelTable();
    this.scene.add(this.modelTable.group);

    // 4. Instantiate 12 Agents + Owner
    Object.values(ROLES).forEach(role => {
      const rig = new AgentRig(role);
      const anim = new AgentAnimator(rig);
      this.agents.set(role.id, { rig, anim });
      this.scene.add(rig.group);
    });

    // 5. Camera & UI
    this.cameraManager = new CameraManager(this.camera, this.renderer.domElement);
    this.logPanel = new LogPanel();
    this.agentCard = new AgentCardUI();
    this.timeline = new TimelineUI();
    this.hud = new OfficeHUD();

    // 6. Director & Bridge
    this.director = new OfficeDirector(
      this.scene,
      this.agents,
      this.architecture,
      this.props,
      this.modelTable
    );

    this.director.onPlaySound = (type) => this.hud.playProceduralSound(type);

    this.director.onAgentStatusChanged = (working, idle, list) => {
      this.hud.updateAgentCounters(working, idle);
      this.hud.updateAgentRoster(list);
    };

    this.hud.onFlyToAgent = (agentId) => {
      const a = this.agents.get(agentId);
      if (a) {
        this.cameraManager.flyTo(a.rig.group.position);
        this.agentCard.show(agentId, a.rig.config.idleQuotes[0], this.allHistoryEvents);
      }
    };

    this.bridge = new EventBridge();
    this.demo = new DemoRunner((ev) => this.processEvent(ev));

    this.setupInteractions();
    this.setupEventListeners();
    this.initPipeline();
    this.director.notifyStatusChanged();

    // Render loop
    this.animate();
  }

  private setupInteractions() {
    // Camera Presets
    document.querySelectorAll('.btn-cam').forEach(btn => {
      btn.addEventListener('click', (e) => {
        const camIdx = parseInt((e.target as HTMLElement).getAttribute('data-cam') || '1');
        this.cameraManager.setPreset(camIdx);
      });
    });

    // Log item click -> fly camera to agent
    this.logPanel.onEventClick = (agentId) => {
      const agent = this.agents.get(agentId);
      if (agent) {
        this.cameraManager.flyTo(agent.rig.group.position);
        this.agentCard.show(agentId, agent.rig.config.idleQuotes[0], this.allHistoryEvents);
      }
    };

    // HUD Time of Day
    this.hud.onTimeOfDayChange = (tod) => this.lights.setTimeOfDay(tod, this.scene);

    // HUD Director toggle
    this.hud.onDirectorToggle = (enabled) => {
      this.cameraManager.isDirectorMode = enabled;
    };

    // Ask User Confirmation
    this.hud.onUserConfirmReply = (detail) => {
      this.bridge.sendUserReply('danh-so-phong', detail);
    };

    // Tool switch
    this.hud.onToolChange = (toolId) => {
      this.switchTool(toolId);
    };

    // Timeline scrubber
    this.timeline.onSpeedChange = (speed) => {
      this.demo.setSpeed(speed);
    };
    this.timeline.onPlayPause = (paused) => {
      if (paused) this.demo.stop();
      else this.demo.start(this.timeline.speed);
    };

    // Raycasting click to select agents
    window.addEventListener('click', (e) => {
      // Ignore clicks on HUD overlay
      if ((e.target as HTMLElement).closest('#hud-overlay')) return;

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
          const agent = this.agents.get(agentId);
          if (agent) {
            this.cameraManager.flyTo(agent.rig.group.position);
            this.agentCard.show(agentId, agent.rig.config.idleQuotes[0], this.allHistoryEvents);
          }
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
  }

  private async initPipeline() {
    // 1. Populate tool list
    const tools = await this.bridge.fetchTools();
    if (tools.length > 0) {
      this.hud.setToolsList(tools);
    }

    // 2. Fetch history (uu tien tool moi nhat, mac dinh boc-khoi-luong-ong-gio)
    const initialTool = tools.length > 0 ? tools[0].id : 'boc-khoi-luong-ong-gio';
    const history = await this.bridge.fetchHistory(initialTool);
    if (history && history.length > 0) {
      this.hud.setStatus('catching');
      // Fast forward catch up
      history.forEach(ev => {
        this.director.handleEvent(ev, true);
        this.logPanel.addEvent(ev);
        this.allHistoryEvents.push(ev);
      });
      this.syncMetrics();
      this.hud.setStatus('live');
    } else {
      // No live events -> run Demo mode!
      this.hud.setStatus('demo');
      const loaded = await this.demo.loadEvents();
      if (loaded) {
        this.demo.start(1.0);
      }
    }

    // Connect SSE
    this.bridge.connect(initialTool);
  }

  private processEvent(ev: OfficeEvent) {
    this.allHistoryEvents.push(ev);
    this.director.handleEvent(ev);
    this.logPanel.addEvent(ev);

    // Track in director camera mode
    const agent = this.agents.get(ev.agent);
    if (agent) {
      this.cameraManager.trackAgent(agent.rig.group.position);
    }

    // Check ask_user
    if (ev.event === 'ask_user') {
      this.hud.showAskUser(ev.detail);
    } else if (ev.event === 'user_reply') {
      this.hud.hideAskUser();
    }

    this.syncMetrics();
  }

  private syncMetrics() {
    this.hud.updateMetrics(
      this.director.roundCount,
      this.director.testPassed,
      this.director.testFailed,
      this.director.blockIssues,
      this.director.bugsCaught
    );
    this.hud.setStage(this.director.currentStage);
  }

  private async switchTool(toolId: string) {
    this.logPanel.clear();
    this.allHistoryEvents = [];
    this.demo.stop();

    const history = await this.bridge.fetchHistory(toolId);
    if (history.length > 0) {
      this.hud.setStatus('live');
      history.forEach(ev => {
        this.director.handleEvent(ev, true);
        this.logPanel.addEvent(ev);
        this.allHistoryEvents.push(ev);
      });
      this.syncMetrics();
    } else {
      this.hud.setStatus('demo');
      this.demo.start(1.0);
    }

    this.bridge.connect(toolId);
  }

  private animate = () => {
    requestAnimationFrame(this.animate);

    // Don't render if document hidden to save GPU
    if (document.hidden) return;

    const delta = Math.min(this.clock.getDelta(), 0.1);

    // Update animations
    this.agents.forEach(a => a.anim.update(delta));
    this.director.update(delta);
    this.modelTable.update(delta);
    this.cameraManager.update(delta);

    this.renderer.render(this.scene, this.camera);
  };
}

// Start application
window.addEventListener('DOMContentLoaded', () => {
  new App();
});

