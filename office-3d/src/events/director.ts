export interface AgentStatusInfo {
  id: string;
  name: string;
  badge: string;
  status: 'WORK' | 'IDLE' | 'WAIT' | 'FAIL';
  task: string;
}
import * as THREE from 'three';
import { AgentRig } from '../agents/rig.js';
import { AgentAnimator } from '../agents/anim.js';
import { PathNavigator, Waypoint } from '../agents/path.js';
import { OfficeArchitecture } from '../scene/office.js';
import { OfficeProps } from '../scene/props.js';
import { CentralModelTable } from '../scene/model-table.js';
import { OfficeEvent } from './bridge.js';

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
  public scene: THREE.Scene;
  public architecture: OfficeArchitecture;
  public props: OfficeProps;
  public modelTable: CentralModelTable;

  // Active handoff movements
  private activeHandoffs: HandoffTask[] = [];

  // Metrics
  public roundCount = 0;
  public testPassed = 0;
  public testFailed = 0;
  public blockIssues = 0;
  public bugsCaught = 0;
  public currentStage = 'SPEC';

  // Paper airplane for researcher queries
  private paperAirplanes: THREE.Mesh[] = [];

  // Audio callback
  public onPlaySound?: (soundType: 'type' | 'ting' | 'thud') => void;
  public onAgentStatusChanged?: (workingCount: number, idleCount: number, statusList: AgentStatusInfo[]) => void;

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

    public notifyStatusChanged() {
    let working = 0;
    let idle = 0;
    const list: AgentStatusInfo[] = [];

    this.agents.forEach((obj, id) => {
      const isWork = obj.rig.currentStatus === 'WORK';
      if (isWork) working++;
      else idle++;

      list.push({
        id,
        name: obj.rig.config.name,
        badge: obj.rig.config.badge,
        status: obj.rig.currentStatus,
        task: obj.rig.currentTask || (isWork ? 'Đang thực thi nhiệm vụ' : 'Đang nghỉ ngơi')
      });
    });

    if (this.onAgentStatusChanged) {
      this.onAgentStatusChanged(working, idle, list);
    }
  }

  public handleEvent(ev: OfficeEvent, isFastForward = false) {
    const agentObj = this.agents.get(ev.agent);

    switch (ev.event) {
      case 'tool_start':
        this.currentStage = 'SPEC';
        this.architecture.setStageStatus('spec', 'active');
        this.modelTable.setStageUnlocked('spec');
        if (agentObj) { agentObj.anim.setState('WORK'); agentObj.rig.setAgentStatus('WORK', 'Tiếp nhận chỉ đạo từ Sếp'); }
        this.notifyStatusChanged();
        if (this.onPlaySound) this.onPlaySound('ting');
        break;

      case 'start':
        if (agentObj) {
          agentObj.anim.setState('WORK');
          agentObj.rig.setAgentStatus('WORK', ev.detail);
        }
        this.notifyStatusChanged();
        if (this.onPlaySound) this.onPlaySound('type');
        break;

      case 'progress':
        if (agentObj) {
          agentObj.anim.workPct = ev.pct || 0;
          agentObj.rig.setAgentStatus('WORK', ev.detail, ev.pct);
        }
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
            if (this.onPlaySound) this.onPlaySound('ting');
          }
        }
        break;

      case 'issue':
        if (ev.severity === 'block') {
          this.blockIssues++;
        }
        if (agentObj) { agentObj.anim.setState('WAIT'); agentObj.rig.setAgentStatus('WAIT', ev.detail); }
        this.notifyStatusChanged();
        if (this.onPlaySound) this.onPlaySound('thud');
        break;

      case 'loop_back':
        this.roundCount = ev.round || 1;
        // Reviewer runs backward to dev
        if (ev.from && ev.to) {
          this.triggerHandoff(ev.from, ev.to, '03-review.md', 'work', isFastForward);
        }
        break;

      case 'build':
        if (ev.result === 'ok') {
          if (agentObj) agentObj.anim.setState('SUCCESS');
        } else {
          if (agentObj) agentObj.anim.setState('FAIL');
        }
        break;

      case 'test':
        this.testPassed = ev.passed || 0;
        this.testFailed = ev.failed || 0;
        this.props.updateTestLeds(this.testPassed, this.testFailed);
        break;

      case 'debugger':
      case 'done':
        if (agentObj) {
          agentObj.anim.setState('IDLE');
          agentObj.anim.workPct = 0;
          agentObj.rig.setAgentStatus('IDLE');
        }
        this.notifyStatusChanged();
        if (ev.agent === 'debugger') {
          this.bugsCaught++;
        }
        break;

      case 'ask_user':
        if (agentObj) agentObj.anim.setState('ASK');
        break;

      case 'user_reply':
        if (agentObj) agentObj.anim.setState('WORK');
        break;

      case 'tool_done':
        this.modelTable.setStageUnlocked('docs');
        this.modelTable.triggerCelebration(this.scene);
        this.agents.forEach(a => { a.anim.setState('CELEBRATE'); a.rig.setAgentStatus('WORK', 'Ăn mừng hoàn thành tool!'); });
        this.notifyStatusChanged();
        if (this.onPlaySound) this.onPlaySound('ting');
        break;
    }
  }

  private triggerHandoff(
    senderId: string,
    receiverId: string,
    file?: string,
    kind?: string,
    isFastForward?: boolean
  ) {
    const sender = this.agents.get(senderId);
    const receiver = this.agents.get(receiverId);
    if (!sender || !receiver) return;

    if (isFastForward) {
      receiver.anim.setState('WORK');
      return;
    }

    // Distance check
    const startPos = sender.rig.group.position;
    const targetPos = receiver.rig.group.position;
    const dist = startPos.distanceTo(targetPos);

    // If query to researcher and distance > 14m: Throw Paper Airplane!
    if (kind === 'query' && receiverId === 'researcher' && dist > 14) {
      this.throwPaperAirplane(startPos, targetPos);
      receiver.anim.setState('WORK');
      return;
    }

    const waypoints = PathNavigator.planPath(startPos, targetPos);

    sender.anim.setState('RUN');

    // Create small carried document mesh
    const itemGeo = new THREE.BoxGeometry(0.25, 0.02, 0.35);
    const itemMat = new THREE.MeshToonMaterial({ color: 0xEDF1EE });
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

  private throwPaperAirplane(from: THREE.Vector3, to: THREE.Vector3) {
    const planeGeo = new THREE.ConeGeometry(0.15, 0.4, 3);
    planeGeo.rotateX(Math.PI / 2);
    const planeMat = new THREE.MeshBasicMaterial({ color: 0xFFFFFF });
    const plane = new THREE.Mesh(planeGeo, planeMat);
    plane.position.copy(from).add(new THREE.Vector3(0, 1.6, 0));
    plane.lookAt(to);
    this.scene.add(plane);

    let progress = 0;
    const interval = setInterval(() => {
      progress += 0.04;
      plane.position.lerpVectors(from, to, progress);
      plane.position.y += Math.sin(progress * Math.PI) * 0.1;
      if (progress >= 1) {
        clearInterval(interval);
        this.scene.remove(plane);
      }
    }, 30);
  }

  public update(delta: number) {
    // Process active handoff movements
    for (let i = this.activeHandoffs.length - 1; i >= 0; i--) {
      const task = this.activeHandoffs[i];
      const sender = this.agents.get(task.senderId);
      const receiver = this.agents.get(task.receiverId);
      if (!sender || !receiver) {
        this.activeHandoffs.splice(i, 1);
        continue;
      }

      const runSpeed = 5.0; // 5 m/s

      if (task.phase === 'to_receiver') {
        const targetWp = task.waypoints[task.waypointIndex];
        const currentPos = sender.rig.group.position;
        const targetVec = new THREE.Vector3(targetWp.x, 0, targetWp.z);
        const dir = new THREE.Vector3().subVectors(targetVec, currentPos);
        const dist = dir.length();

        if (dist > 0.2) {
          dir.normalize();
          sender.rig.group.position.addScaledVector(dir, runSpeed * delta);
          sender.rig.group.rotation.y = Math.atan2(dir.x, dir.z);
        } else {
          task.waypointIndex++;
          if (task.waypointIndex >= task.waypoints.length) {
            // Reached receiver!
            task.phase = 'handover';
            task.timer = 0;
            sender.anim.setState('GIVE');
            receiver.anim.setState('RECEIVE');
          }
        }
      } else if (task.phase === 'handover') {
        task.timer += delta;
        if (task.timer > 1.2) {
          // Handover complete!
          if (task.itemMesh) {
            sender.rig.handAttachment.remove(task.itemMesh);
          }
          receiver.anim.setState('WORK');

          // Return home
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

        if (dist > 0.2) {
          dir.normalize();
          sender.rig.group.position.addScaledVector(dir, 2.5 * delta);
          sender.rig.group.rotation.y = Math.atan2(dir.x, dir.z);
        } else {
          task.waypointIndex++;
          if (task.waypointIndex >= task.waypoints.length) {
            // Home!
            sender.rig.group.position.set(...sender.rig.config.deskPos);
            sender.rig.group.rotation.y = sender.rig.config.deskRot;
            sender.anim.setState('IDLE');
            this.activeHandoffs.splice(i, 1);
          }
        }
      }
    }
  }
}

