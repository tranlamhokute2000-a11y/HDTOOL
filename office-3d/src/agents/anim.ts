import * as THREE from 'three';
import { AgentRig } from './rig.js';

export type AgentState =
  | 'IDLE'
  | 'WALK'
  | 'RUN'
  | 'WORK'
  | 'GIVE'
  | 'RECEIVE'
  | 'WAIT'
  | 'FAIL'
  | 'SUCCESS'
  | 'CELEBRATE'
  | 'ASK';

export class AgentAnimator {
  public rig: AgentRig;
  public state: AgentState = 'IDLE';
  public stateTime = 0;
  public workPct = 0;
  public speed = 0;

  constructor(rig: AgentRig) {
    this.rig = rig;
  }

  public setState(newState: AgentState) {
    if (this.state === newState) return;
    this.state = newState;
    this.stateTime = 0;
    this.resetJoints();

    if (newState === 'WORK' || newState === 'RUN' || newState === 'GIVE' || newState === 'RECEIVE') {
      this.rig.setFloorRingActive(true);
    } else {
      this.rig.setFloorRingActive(false);
    }
  }

  public resetJoints() {
    this.rig.leftArm.rotation.set(0, 0, 0);
    this.rig.rightArm.rotation.set(0, 0, 0);
    this.rig.leftForearm.rotation.set(0, 0, 0);
    this.rig.rightForearm.rotation.set(0, 0, 0);
    this.rig.leftLeg.rotation.set(0, 0, 0);
    this.rig.rightLeg.rotation.set(0, 0, 0);
    this.rig.leftShin.rotation.set(0, 0, 0);
    this.rig.rightShin.rotation.set(0, 0, 0);
    this.rig.torsoMesh.position.y = 0.85;
    this.rig.torsoMesh.rotation.set(0, 0, 0);
    this.rig.headGroup.rotation.set(0, 0, 0);
  }

  public update(delta: number) {
    this.stateTime += delta;
    this.rig.update(delta);

    const t = this.stateTime;

    switch (this.state) {
      case 'IDLE':
        this.animateIdle(t);
        break;
      case 'WALK':
        this.animateWalk(t, 6);
        break;
      case 'RUN':
        this.animateWalk(t, 12, 0.25);
        break;
      case 'WORK':
        this.animateWork(t);
        break;
      case 'GIVE':
      case 'RECEIVE':
        this.animateGiveReceive(t);
        break;
      case 'WAIT':
        this.animateWait(t);
        break;
      case 'FAIL':
        this.animateFail(t);
        break;
      case 'SUCCESS':
      case 'CELEBRATE':
        this.animateCelebrate(t);
        break;
      case 'ASK':
        this.animateAsk(t);
        break;
    }
  }

  private animateIdle(t: number) {
    // Breathing
    const breath = Math.sin(t * 2) * 0.015;
    this.rig.torsoMesh.position.y = 0.85 + breath;
    this.rig.headGroup.rotation.y = Math.sin(t * 0.7) * 0.12;

    // Gentle arm sway
    this.rig.leftArm.rotation.x = Math.sin(t * 1.5) * 0.05;
    this.rig.rightArm.rotation.x = -Math.sin(t * 1.5) * 0.05;
  }

  private animateWalk(t: number, speed: number, lean: number = 0) {
    const cycle = t * speed;
    const legSwing = Math.sin(cycle) * 0.6;
    const armSwing = Math.cos(cycle) * 0.5;

    // Legs
    this.rig.leftLeg.rotation.x = legSwing;
    this.rig.rightLeg.rotation.x = -legSwing;
    this.rig.leftShin.rotation.x = Math.max(0, -legSwing * 0.8);
    this.rig.rightShin.rotation.x = Math.max(0, legSwing * 0.8);

    // Arms opposite to legs
    this.rig.leftArm.rotation.x = -armSwing;
    this.rig.rightArm.rotation.x = armSwing;
    this.rig.leftForearm.rotation.x = -0.3;
    this.rig.rightForearm.rotation.x = -0.3;

    // Torso bounce & forward lean
    this.rig.torsoMesh.position.y = 0.85 + Math.abs(Math.sin(cycle)) * 0.06;
    this.rig.torsoMesh.rotation.x = lean;
    this.rig.headGroup.rotation.x = -lean * 0.5;
  }

  private animateWork(t: number) {
    const role = this.rig.config.id;
    const rate = 1 + (this.workPct / 100) * 1.5;

    switch (role) {
      case 'api-developer':
        // Fast keyboard typing
        this.rig.torsoMesh.rotation.x = 0.15; // slouch slightly
        this.rig.leftArm.rotation.x = -0.7;
        this.rig.rightArm.rotation.x = -0.7;
        this.rig.leftForearm.rotation.x = -0.9 + Math.sin(t * 18 * rate) * 0.15;
        this.rig.rightForearm.rotation.x = -0.9 + Math.cos(t * 20 * rate) * 0.15;
        this.rig.headGroup.rotation.x = -0.1 + Math.sin(t * 5) * 0.04;
        break;

      case 'reviewer':
        // Examining document, then stamping
        this.rig.leftArm.rotation.x = -0.6;
        this.rig.leftForearm.rotation.x = -0.5;
        // Right hand stamps down periodically
        const stampCycle = Math.sin(t * 4);
        if (stampCycle > 0.6) {
          this.rig.rightArm.rotation.x = -0.3;
          this.rig.rightForearm.rotation.x = -0.4;
        } else {
          this.rig.rightArm.rotation.x = -0.9;
          this.rig.rightForearm.rotation.x = -0.8;
        }
        break;

      case 'architect':
        // Measuring with T-square, drawing
        this.rig.torsoMesh.rotation.x = 0.2;
        this.rig.leftArm.rotation.x = -0.5 + Math.sin(t * 2) * 0.1;
        this.rig.rightArm.rotation.x = -0.7 + Math.cos(t * 4) * 0.2;
        this.rig.rightForearm.rotation.x = -0.6;
        break;

      case 'requirements':
        // Writing on wall/board, stepping back
        this.rig.rightArm.rotation.x = -1.2 + Math.sin(t * 6) * 0.2;
        this.rig.rightForearm.rotation.x = -0.4;
        this.rig.headGroup.rotation.y = Math.sin(t * 2) * 0.2;
        break;

      case 'tester':
        // Pressing buttons on test bench
        this.rig.leftArm.rotation.x = -0.8 + Math.sin(t * 7) * 0.2;
        this.rig.rightArm.rotation.x = -0.8 + Math.cos(t * 8) * 0.2;
        this.rig.torsoMesh.position.y = 0.85 + Math.sin(t * 6) * 0.03;
        break;

      case 'debugger':
        // Inspecting with flashlight
        this.rig.rightArm.rotation.x = -0.9;
        this.rig.rightForearm.rotation.x = -0.3;
        this.rig.headGroup.rotation.x = 0.2 + Math.sin(t * 3) * 0.1;
        break;

      case 'packaging':
        // Packing boxes, wrapping tape
        this.rig.leftArm.rotation.x = -0.6 + Math.sin(t * 5) * 0.2;
        this.rig.rightArm.rotation.x = -0.6 + Math.cos(t * 5) * 0.2;
        break;

      default:
        // Generic active working
        this.rig.leftArm.rotation.x = -0.6 + Math.sin(t * 3) * 0.1;
        this.rig.rightArm.rotation.x = -0.6 + Math.cos(t * 3) * 0.1;
        this.rig.headGroup.rotation.x = 0.1 + Math.sin(t * 2) * 0.05;
        break;
    }
  }

  private animateGiveReceive(t: number) {
    // Both arms forward, offering/holding paper/prop
    this.rig.leftArm.rotation.x = -0.85;
    this.rig.rightArm.rotation.x = -0.85;
    this.rig.leftForearm.rotation.x = -0.4;
    this.rig.rightForearm.rotation.x = -0.4;
    // Slight bow
    this.rig.torsoMesh.rotation.x = 0.15;
    this.rig.headGroup.rotation.x = 0.1;
  }

  private animateWait(t: number) {
    // Arms crossed or tapping foot
    this.rig.leftArm.rotation.x = -0.7;
    this.rig.leftArm.rotation.z = 0.4;
    this.rig.rightArm.rotation.x = -0.7;
    this.rig.rightArm.rotation.z = -0.4;
    // Foot tapping
    this.rig.rightShin.rotation.x = Math.max(0, Math.sin(t * 8) * 0.25);
  }

  private animateFail(t: number) {
    // Slumped shoulders, head down
    this.rig.torsoMesh.rotation.x = 0.25;
    this.rig.headGroup.rotation.x = 0.35;
    this.rig.leftArm.rotation.x = 0.1;
    this.rig.rightArm.rotation.x = 0.1;
  }

  private animateCelebrate(t: number) {
    // Arms raised in V-shape, jumping
    this.rig.leftArm.rotation.x = -2.4;
    this.rig.leftArm.rotation.z = -0.5;
    this.rig.rightArm.rotation.x = -2.4;
    this.rig.rightArm.rotation.z = 0.5;
    this.rig.torsoMesh.position.y = 0.85 + Math.abs(Math.sin(t * 8)) * 0.25;
    this.rig.headGroup.rotation.x = -0.3;
  }

  private animateAsk(t: number) {
    // One arm raised high
    this.rig.rightArm.rotation.x = -2.8;
    this.rig.rightArm.rotation.z = 0.2;
    this.rig.leftArm.rotation.x = -0.2;
    this.rig.headGroup.rotation.x = -0.15;
  }
}

