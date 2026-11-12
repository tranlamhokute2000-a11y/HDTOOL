import * as THREE from 'three';
import { AgentRig, AgentVisualStatus } from '../agents/rig.js';
import { AgentAnimator } from '../agents/anim.js';
import { PathNavigator, Waypoint } from '../agents/path.js';
import { resolveDynamicRole, RoleConfig } from '../agents/roles.js';
import { OfficeArchitecture } from '../scene/office.js';
import { OfficeProps } from '../scene/props.js';
import { CentralModelTable } from '../scene/model-table.js';
import { OfficeEvent, SwarmSnapshot, SwarmWorkerTelemetry } from './bridge.js';

export interface AgentStatusInfo {
  id: string;
  name: string;
  shortName: string;
  badge: string;
  zone: string;
  zoneLabel: string;
  color: string;
  model: string;
  status: AgentVisualStatus;
  task: string;
  lastActivityAt?: string;
}

interface HandoffTask {
  senderId: string;
  receiverId: string;
  file?: string;
  kind?: string;
  waypoints: Waypoint[];
  waypointIndex: number;
  phase: 'to_receiver' | 'handover' | 'return_home';
  timer: number;
  itemMesh?: THREE.Mesh;
}

export class OfficeDirector {
  public agents: Map<string, { rig: AgentRig; anim: AgentAnimator }> = new Map();
  public telemetryByWorker: Map<string, SwarmWorkerTelemetry> = new Map();
  public scene: THREE.Scene;
  public architecture: OfficeArchitecture;
  public props: OfficeProps;
  public modelTable: CentralModelTable;

  private activeHandoffs: HandoffTask[] = [];
  private spareIndex = 0;
  private lastBoardSeqSeen = 0;

  public roundCount = 0;
  public testPassed = 0;
  public testFailed = 0;
  public blockIssues = 0;
  public bugsCaught = 0;
  public currentStage = 'TRỰC TIẾP';

  public onPlaySound?: (soundType: 'type' | 'ting' | 'thud') => void;
  public onAgentStatusChanged?: (
    workingCount: number,
    waitingCount: number,
    totalEnabled: number,
    statusList: AgentStatusInfo[]
  ) => void;

  constructor(
    scene: THREE.Scene,
    agents: Map<string, { rig: AgentRig; anim: AgentAnimator }>,
    architecture: OfficeArchitecture,
    props: OfficeProps,
    modelTable: CentralModelTable
  ) {
    this.scene = scene;
    this.agents = agents;
    this.architecture = architecture;
    this.props = props;
    this.modelTable = modelTable;
  }

  public ensureAgentExists(workerId: string, displayName?: string): { rig: AgentRig; anim: AgentAnimator } {
    const existing = this.agents.get(workerId);
    if (existing) return existing;

    const cfg: RoleConfig = resolveDynamicRole(workerId, this.spareIndex++, displayName);
    const rig = new AgentRig(cfg);
    const anim = new AgentAnimator(rig);
    const pair = { rig, anim };
    this.agents.set(workerId, pair);
    this.scene.add(rig.group);
    return pair;
  }

  public resetAllAgentsToIdle() {
    this.activeHandoffs.forEach(task => {
      if (task.itemMesh) {
        const sender = this.agents.get(task.senderId);
        if (sender) sender.rig.handAttachment.remove(task.itemMesh);
      }
    });
    this.activeHandoffs = [];
    this.lastBoardSeqSeen = 0;

    this.agents.forEach((obj, id) => {
      obj.rig.group.position.set(...obj.rig.config.deskPos);
      obj.rig.group.rotation.y = obj.rig.config.deskRot;
      obj.anim.setState('IDLE');
      obj.rig.setAgentStatus('IDLE', '');
      this.props.setDeskActive(id, 'IDLE');
    });
    this.notifyStatusChanged();
  }

  public notifyStatusChanged() {
    let working = 0;
    let waiting = 0;
    let enabled = 0;
    const list: AgentStatusInfo[] = [];

    this.agents.forEach((obj, id) => {
      if (obj.rig.currentStatus === 'KILLED') return;
      enabled++;
      if (obj.rig.currentStatus === 'WORK') working++;
      else if (obj.rig.currentStatus === 'WAIT' || obj.rig.currentStatus === 'FAIL') waiting++;

      const tel = this.telemetryByWorker.get(id);
      list.push({
        id,
        name: obj.rig.config.name,
        shortName: obj.rig.config.shortName,
        badge: obj.rig.config.badge,
        zone: obj.rig.config.zone,
        zoneLabel: obj.rig.config.zoneLabel,
        color: obj.rig.config.color,
        model: (tel && tel.model) || 'gemini-3.8-flash-high',
        status: obj.rig.currentStatus,
        task: obj.rig.currentTask || (obj.rig.currentStatus === 'WORK' ? 'Đang thực thi...' : 'Đang sẵn sàng'),
        lastActivityAt: tel ? tel.lastActivityAt : ''
      });
    });

    // Put active Swarm workers & WORK/WAIT agents at the top of list
    const statusWeight = (s: AgentVisualStatus) => (s === 'WORK' ? 0 : s === 'FAIL' ? 1 : s === 'WAIT' ? 2 : 3);
    list.sort((a, b) => {
      const wa = statusWeight(a.status);
      const wb = statusWeight(b.status);
      if (wa !== wb) return wa - wb;
      const aSwarm = this.telemetryByWorker.has(a.id) ? 0 : 1;
      const bSwarm = this.telemetryByWorker.has(b.id) ? 0 : 1;
      if (aSwarm !== bSwarm) return aSwarm - bSwarm;
      return a.shortName.localeCompare(b.shortName);
    });

    if (this.onAgentStatusChanged) {
      this.onAgentStatusChanged(working, waiting, enabled, list);
    }
  }

  public applySwarmSnapshot(snapshot: SwarmSnapshot, animateNewPosts = true) {
    this.telemetryByWorker.clear();
    const activeIds = new Set<string>();

    this.architecture.updatePipelineTitle(`SWARM TRỰC TIẾP: ${snapshot.title}`);
    this.architecture.updateAuditoriumBanner(
      snapshot.title.slice(0, 26),
      snapshot.swarmCompleted ? '✅ ĐÃ HOÀN TẤT NHIỆM VỤ' : `⚡ ĐANG CHẠY (${snapshot.workers.length} WORKERS)`
    );
    this.architecture.updateCouncilBoard(
      snapshot.board.map(b => ({ seq: b.seq, agent: b.agent, text: b.text }))
    );

    this.currentStage = snapshot.swarmCompleted ? 'HOÀN TẤT' : 'ĐANG CHẠY';

    snapshot.workers.forEach(w => {
      activeIds.add(w.id);
      this.telemetryByWorker.set(w.id, w);
      const pair = this.ensureAgentExists(w.id, w.name);

      const isHandoffRunning = this.activeHandoffs.some(h => h.senderId === w.id);
      if (!isHandoffRunning) {
        if (w.status === 'WORK') pair.anim.setState('WORK');
        else if (w.status === 'WAIT') pair.anim.setState('WAIT');
        else if (w.status === 'FAIL') pair.anim.setState('FAIL');
        else pair.anim.setState('IDLE');
      }

      pair.rig.setAgentStatus(w.status, w.currentAction);
      this.props.setDeskActive(w.id, w.status);
    });

    // Update pipeline lights from swarm completion / board hints
    if (snapshot.swarmCompleted) {
      this.architecture.stageOrder.forEach(s => {
        this.architecture.setStageStatus(s, 'pass');
        this.modelTable.setStageUnlocked(s);
      });
    } else {
      this.architecture.setStageStatus('spec', 'pass');
      this.architecture.setStageStatus('design', 'pass');
      this.architecture.setStageStatus('code', 'active');
    }

    // Trigger live walking animation ONLY on newly posted board messages after initial catchup
    if (animateNewPosts && this.lastBoardSeqSeen > 0 && snapshot.headSeq > this.lastBoardSeqSeen) {
      const freshPosts = snapshot.board.filter(b => b.seq > this.lastBoardSeqSeen);
      for (const msg of freshPosts) {
        if (msg.mentions && msg.mentions.length > 0 && msg.mentions[0] !== 'all') {
          this.triggerHandoff(msg.agent, msg.mentions[0], `#${msg.seq}`, 'work', false);
        }
        if (msg.text && msg.text.trim().toLowerCase().startsWith('[done]')) {
          this.modelTable.triggerCelebration(this.scene);
          if (this.onPlaySound) this.onPlaySound('ting');
        }
      }
    }
    this.lastBoardSeqSeen = Math.max(this.lastBoardSeqSeen, snapshot.headSeq);

    this.notifyStatusChanged();
  }

  public handleEvent(ev: OfficeEvent, isFastForward = false) {
    const agentObj = this.ensureAgentExists(ev.agent || 'orchestrator');

    switch (ev.event) {
      case 'tool_start':
        this.currentStage = 'SPEC';
        this.architecture.setStageStatus('spec', 'active');
        this.modelTable.setStageUnlocked('spec');
        agentObj.anim.setState('WORK');
        agentObj.rig.setAgentStatus('WORK', ev.detail || 'Khởi chạy tác vụ mới');
        this.props.setDeskActive(ev.agent, 'WORK');
        this.notifyStatusChanged();
        if (!isFastForward && this.onPlaySound) this.onPlaySound('ting');
        break;

      case 'start':
        agentObj.anim.setState('WORK');
        agentObj.rig.setAgentStatus('WORK', ev.detail);
        this.props.setDeskActive(ev.agent, 'WORK');
        this.notifyStatusChanged();
        if (!isFastForward && this.onPlaySound) this.onPlaySound('type');
        break;

      case 'progress':
        agentObj.anim.workPct = ev.pct || 0;
        agentObj.anim.setState('WORK');
        agentObj.rig.setAgentStatus('WORK', ev.detail, ev.pct);
        this.props.setDeskActive(ev.agent, 'WORK');
        this.notifyStatusChanged();
        break;

      case 'handoff':
        if (ev.agent && ev.to) {
          this.triggerHandoff(ev.agent, ev.to, ev.file, ev.kind, isFastForward);
        }
        break;

      case 'gate':
        if (ev.stage) {
          const pass = ev.result === 'pass';
          this.architecture.setStageStatus(ev.stage, pass ? 'pass' : 'fail');
          if (pass) {
            this.modelTable.setStageUnlocked(ev.stage);
            this.currentStage = ev.stage.toUpperCase();
            if (!isFastForward && this.onPlaySound) this.onPlaySound('ting');
          }
        }
        break;

      case 'issue':
        if (ev.severity === 'block') this.blockIssues++;
        agentObj.anim.setState('WAIT');
        agentObj.rig.setAgentStatus('WAIT', ev.detail);
        this.props.setDeskActive(ev.agent, 'WAIT');
        this.notifyStatusChanged();
        if (!isFastForward && this.onPlaySound) this.onPlaySound('thud');
        break;

      case 'loop_back':
        this.roundCount = ev.round || 1;
        if (ev.from && ev.to) {
          this.triggerHandoff(ev.from, ev.to, '03-review.md', 'work', isFastForward);
        }
        break;

      case 'build':
        if (ev.result === 'ok') {
          agentObj.anim.setState('SUCCESS');
          agentObj.rig.setAgentStatus('WORK', '✅ Biên dịch 0 lỗi!');
          this.props.setDeskActive(ev.agent, 'WORK');
        } else {
          agentObj.anim.setState('FAIL');
          agentObj.rig.setAgentStatus('FAIL', ev.detail || '❌ Lỗi biên dịch');
          this.props.setDeskActive(ev.agent, 'FAIL');
        }
        this.notifyStatusChanged();
        break;

      case 'test':
        this.testPassed = ev.passed || 0;
        this.testFailed = ev.failed || 0;
        this.props.updateTestLeds(this.testPassed, this.testFailed);
        break;

      case 'done':
        agentObj.anim.setState('IDLE');
        agentObj.anim.workPct = 0;
        agentObj.rig.setAgentStatus('IDLE', ev.detail ? `✅ ${ev.detail}` : '✅ Hoàn thành');
        this.props.setDeskActive(ev.agent, 'IDLE');
        this.notifyStatusChanged();
        if (ev.agent === 'debugger') this.bugsCaught++;
        break;

      case 'ask_user':
        agentObj.anim.setState('ASK');
        agentObj.rig.setAgentStatus('WAIT', ev.detail);
        this.notifyStatusChanged();
        break;

      case 'user_reply':
        agentObj.anim.setState('WORK');
        agentObj.rig.setAgentStatus('WORK', ev.detail);
        this.notifyStatusChanged();
        break;

      case 'tool_done':
        this.currentStage = 'HOÀN TẤT';
        this.modelTable.setStageUnlocked('docs');
        if (!isFastForward) this.modelTable.triggerCelebration(this.scene);
        this.agents.forEach((a, id) => {
          a.anim.setState(isFastForward ? 'IDLE' : 'CELEBRATE');
          a.rig.setAgentStatus('IDLE', `✅ ${ev.detail || 'Hoàn thành nhiệm vụ!'}`);
          this.props.setDeskActive(id, 'IDLE');
        });
        this.notifyStatusChanged();
        if (!isFastForward && this.onPlaySound) this.onPlaySound('ting');
        break;
    }
  }

  public triggerHandoff(
    senderId: string,
    receiverId: string,
    file?: string,
    kind?: string,
    isFastForward?: boolean
  ) {
    const sender = this.agents.get(senderId);
    const receiver = this.agents.get(receiverId);
    if (!sender || !receiver || sender === receiver) return;

    if (isFastForward) {
      receiver.anim.setState('WORK');
      return;
    }

    const startPos = sender.rig.group.position;
    const targetPos = receiver.rig.group.position;
    const waypoints = PathNavigator.planPath(startPos, targetPos);

    sender.anim.setState('RUN');
    sender.rig.setSpeechBubble(`🏃 Giao việc → @${receiverId}`);

    const itemGeo = new THREE.BoxGeometry(0.28, 0.04, 0.36);
    const itemMat = new THREE.MeshBasicMaterial({ color: 0x38BDF8 });
    const itemMesh = new THREE.Mesh(itemGeo, itemMat);
    sender.rig.handAttachment.add(itemMesh);

    this.activeHandoffs.push({
      senderId,
      receiverId,
      file,
      kind,
      waypoints,
      waypointIndex: 0,
      phase: 'to_receiver',
      timer: 0,
      itemMesh
    });
  }

  public update(delta: number) {
    for (let i = this.activeHandoffs.length - 1; i >= 0; i--) {
      const task = this.activeHandoffs[i];
      const sender = this.agents.get(task.senderId);
      const receiver = this.agents.get(task.receiverId);
      if (!sender || !receiver) {
        this.activeHandoffs.splice(i, 1);
        continue;
      }

      if (task.phase === 'to_receiver') {
        const targetWp = task.waypoints[task.waypointIndex];
        const currentPos = sender.rig.group.position;
        const targetVec = new THREE.Vector3(targetWp.x, 0, targetWp.z);
        const dir = new THREE.Vector3().subVectors(targetVec, currentPos);
        const dist = dir.length();

        if (dist > 0.45) {
          dir.normalize();
          sender.rig.group.position.addScaledVector(dir, 5.5 * delta);
          sender.rig.group.rotation.y = Math.atan2(dir.x, dir.z);
        } else {
          task.waypointIndex++;
          if (task.waypointIndex >= task.waypoints.length) {
            task.phase = 'handover';
            task.timer = 0;
            sender.anim.setState('GIVE');
            receiver.anim.setState('RECEIVE');
            receiver.rig.setSpeechBubble(`📥 Nhận việc từ @${task.senderId}`);
          }
        }
      } else if (task.phase === 'handover') {
        task.timer += delta;
        if (task.timer > 1.0) {
          if (task.itemMesh) sender.rig.handAttachment.remove(task.itemMesh);
          receiver.anim.setState('WORK');
          receiver.rig.setAgentStatus('WORK', task.file ? `Đang xử lý ${task.file}` : 'Đang nhận bàn giao...');

          task.phase = 'return_home';
          const homePos = new THREE.Vector3(...sender.rig.config.deskPos);
          task.waypoints = PathNavigator.planPath(sender.rig.group.position, homePos);
          task.waypointIndex = 0;
          sender.anim.setState('WALK');
        }
      } else if (task.phase === 'return_home') {
        const targetWp = task.waypoints[task.waypointIndex];
        const currentPos = sender.rig.group.position;
        const targetVec = new THREE.Vector3(targetWp.x, 0, targetWp.z);
        const dir = new THREE.Vector3().subVectors(targetVec, currentPos);
        const dist = dir.length();

        if (dist > 0.25) {
          dir.normalize();
          sender.rig.group.position.addScaledVector(dir, 3.6 * delta);
          sender.rig.group.rotation.y = Math.atan2(dir.x, dir.z);
        } else {
          task.waypointIndex++;
          if (task.waypointIndex >= task.waypoints.length) {
            sender.rig.group.position.set(...sender.rig.config.deskPos);
            sender.rig.group.rotation.y = sender.rig.config.deskRot;
            sender.anim.setState(sender.rig.currentStatus === 'WORK' ? 'WORK' : 'IDLE');
            this.activeHandoffs.splice(i, 1);
          }
        }
      }
    }
  }
}
