import * as THREE from 'three';
import { RoleConfig } from './roles.js';

export type AgentVisualStatus = 'WORK' | 'IDLE' | 'WAIT' | 'FAIL' | 'KILLED';

export class AgentRig {
  public group: THREE.Group;
  public config: RoleConfig;

  public headGroup: THREE.Group;
  public torsoMesh: THREE.Mesh;
  public leftArm: THREE.Group;
  public rightArm: THREE.Group;
  public leftForearm: THREE.Group;
  public rightForearm: THREE.Group;
  public leftLeg: THREE.Group;
  public rightLeg: THREE.Group;
  public leftShin: THREE.Group;
  public rightShin: THREE.Group;

  public nameplate: THREE.Sprite;
  public speechBubble: THREE.Sprite;
  public floorRing: THREE.Mesh;
  public handAttachment: THREE.Group;

  public leftEye: THREE.Mesh;
  public rightEye: THREE.Mesh;

  public blinkTimer = 0;
  public isBlinking = false;
  public currentStatus: AgentVisualStatus = 'IDLE';
  public currentTask = '';
  public animTime = 0;

  private nameplateTex: THREE.CanvasTexture | null = null;
  private bubbleTex: THREE.CanvasTexture | null = null;

  constructor(config: RoleConfig) {
    this.config = config;
    this.group = new THREE.Group();
    this.group.name = `agent_${config.id}`;
    this.group.position.set(...config.deskPos);
    this.group.rotation.y = config.deskRot;

    const skinMat = new THREE.MeshLambertMaterial({ color: 0xFCD9B6 });
    const eyeMat = new THREE.MeshBasicMaterial({ color: 0x0F172A });
    const bodyMat = new THREE.MeshLambertMaterial({ color: config.outfit.bodyColor });
    const vestMat = new THREE.MeshLambertMaterial({ color: config.outfit.vestColor });
    const shirtStripeMat = new THREE.MeshBasicMaterial({ color: 0xFFFFFF });
    const shoeMat = new THREE.MeshLambertMaterial({ color: 0x1E293B });
    const hatMat = new THREE.MeshLambertMaterial({ color: config.outfit.hatColor });
    const hairMat = new THREE.MeshLambertMaterial({ color: 0x1E293B });

    // 1. Torso
    this.torsoMesh = new THREE.Mesh(new THREE.CylinderGeometry(0.26, 0.24, 0.62, 12), bodyMat);
    this.torsoMesh.position.y = 0.85;
    this.torsoMesh.castShadow = true;
    this.group.add(this.torsoMesh);

    const vestMesh = new THREE.Mesh(new THREE.CylinderGeometry(0.275, 0.25, 0.48, 12), vestMat);
    vestMesh.position.y = 0.04;
    vestMesh.castShadow = true;
    this.torsoMesh.add(vestMesh);

    const collar = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.22, 0.08, 12), shirtStripeMat);
    collar.position.y = 0.28;
    this.torsoMesh.add(collar);

    // 2. Head Group
    this.headGroup = new THREE.Group();
    this.headGroup.position.set(0, 0.50, 0);
    this.torsoMesh.add(this.headGroup);

    const headMesh = new THREE.Mesh(new THREE.SphereGeometry(0.34, 16, 14), skinMat);
    headMesh.castShadow = true;
    this.headGroup.add(headMesh);

    // Hair base
    const hair = new THREE.Mesh(
      new THREE.SphereGeometry(0.35, 14, 10, 0, Math.PI * 2, 0, Math.PI * 0.52),
      hairMat
    );
    hair.rotation.x = -0.25;
    hair.position.y = 0.04;
    this.headGroup.add(hair);

    // Eyes
    const eyeGeo = new THREE.SphereGeometry(0.04, 8, 8);
    eyeGeo.scale(1, 1.35, 0.6);
    this.leftEye = new THREE.Mesh(eyeGeo, eyeMat);
    this.leftEye.position.set(-0.11, 0.03, 0.31);
    this.rightEye = new THREE.Mesh(eyeGeo, eyeMat);
    this.rightEye.position.set(0.11, 0.03, 0.31);
    this.headGroup.add(this.leftEye);
    this.headGroup.add(this.rightEye);

    if (config.outfit.hasGlasses) {
      const glassFrameMat = new THREE.MeshBasicMaterial({ color: 0x0F172A });
      const leftRing = new THREE.Mesh(new THREE.TorusGeometry(0.07, 0.014, 6, 12), glassFrameMat);
      leftRing.position.set(-0.11, 0.03, 0.34);
      const rightRing = new THREE.Mesh(new THREE.TorusGeometry(0.07, 0.014, 6, 12), glassFrameMat);
      rightRing.position.set(0.11, 0.03, 0.34);
      const bridge = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.015, 0.015), glassFrameMat);
      bridge.position.set(0, 0.03, 0.34);
      this.headGroup.add(leftRing, rightRing, bridge);
    }

    // Hat / Accessory
    if (config.outfit.hatType === 'helmet') {
      const helmet = new THREE.Mesh(
        new THREE.SphereGeometry(0.37, 16, 10, 0, Math.PI * 2, 0, Math.PI * 0.54),
        hatMat
      );
      helmet.position.y = 0.06;
      this.headGroup.add(helmet);
      const brim = new THREE.Mesh(new THREE.CylinderGeometry(0.41, 0.41, 0.035, 16), hatMat);
      brim.position.set(0, 0.05, 0.06);
      this.headGroup.add(brim);
    } else if (config.outfit.hatType === 'beret') {
      const beret = new THREE.Mesh(new THREE.CylinderGeometry(0.38, 0.32, 0.11, 12), hatMat);
      beret.position.set(0.04, 0.28, -0.04);
      beret.rotation.z = -0.18;
      this.headGroup.add(beret);
    } else if (config.outfit.hatType === 'cap') {
      const dome = new THREE.Mesh(
        new THREE.SphereGeometry(0.36, 14, 8, 0, Math.PI * 2, 0, Math.PI * 0.5),
        hatMat
      );
      dome.position.y = 0.07;
      this.headGroup.add(dome);
      const visor = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.03, 0.26), hatMat);
      visor.position.set(0, 0.09, 0.28);
      this.headGroup.add(visor);
    } else if (config.outfit.hatType === 'headset') {
      const band = new THREE.Mesh(new THREE.TorusGeometry(0.36, 0.025, 6, 16, Math.PI), hatMat);
      band.position.y = 0.04;
      this.headGroup.add(band);
      const earL = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.09, 0.05, 10), hatMat);
      earL.rotation.z = Math.PI / 2;
      earL.position.set(-0.36, 0.02, 0);
      const earR = earL.clone();
      earR.position.x = 0.36;
      this.headGroup.add(earL, earR);
    }

    // 3. Arms
    const armGeo = new THREE.CylinderGeometry(0.065, 0.06, 0.30, 8);
    const forearmGeo = new THREE.CylinderGeometry(0.06, 0.055, 0.26, 8);
    const handGeo = new THREE.SphereGeometry(0.07, 8, 8);

    this.leftArm = new THREE.Group();
    this.leftArm.position.set(-0.33, 0.20, 0);
    this.torsoMesh.add(this.leftArm);
    const lArmMesh = new THREE.Mesh(armGeo, vestMat);
    lArmMesh.position.y = -0.13;
    this.leftArm.add(lArmMesh);

    this.leftForearm = new THREE.Group();
    this.leftForearm.position.set(0, -0.28, 0);
    this.leftArm.add(this.leftForearm);
    const lForearmMesh = new THREE.Mesh(forearmGeo, skinMat);
    lForearmMesh.position.y = -0.11;
    this.leftForearm.add(lForearmMesh);
    const lHand = new THREE.Mesh(handGeo, skinMat);
    lHand.position.y = -0.24;
    this.leftForearm.add(lHand);

    this.rightArm = new THREE.Group();
    this.rightArm.position.set(0.33, 0.20, 0);
    this.torsoMesh.add(this.rightArm);
    const rArmMesh = new THREE.Mesh(armGeo, vestMat);
    rArmMesh.position.y = -0.13;
    this.rightArm.add(rArmMesh);

    this.rightForearm = new THREE.Group();
    this.rightForearm.position.set(0, -0.28, 0);
    this.rightArm.add(this.rightForearm);
    const rForearmMesh = new THREE.Mesh(forearmGeo, skinMat);
    rForearmMesh.position.y = -0.11;
    this.rightForearm.add(rForearmMesh);
    const rHand = new THREE.Mesh(handGeo, skinMat);
    rHand.position.y = -0.24;
    this.rightForearm.add(rHand);

    this.handAttachment = new THREE.Group();
    this.handAttachment.position.set(0, -0.26, 0.1);
    this.rightForearm.add(this.handAttachment);

    // 4. Legs
    const thighGeo = new THREE.CylinderGeometry(0.085, 0.075, 0.34, 8);
    const shinGeo = new THREE.CylinderGeometry(0.075, 0.07, 0.30, 8);
    const footGeo = new THREE.BoxGeometry(0.14, 0.09, 0.21);

    this.leftLeg = new THREE.Group();
    this.leftLeg.position.set(-0.13, 0.54, 0);
    this.group.add(this.leftLeg);
    const lThigh = new THREE.Mesh(thighGeo, bodyMat);
    lThigh.position.y = -0.15;
    this.leftLeg.add(lThigh);
    this.leftShin = new THREE.Group();
    this.leftShin.position.set(0, -0.30, 0);
    this.leftLeg.add(this.leftShin);
    const lShin = new THREE.Mesh(shinGeo, bodyMat);
    lShin.position.y = -0.14;
    this.leftShin.add(lShin);
    const lFoot = new THREE.Mesh(footGeo, shoeMat);
    lFoot.position.set(0, -0.26, 0.04);
    this.leftShin.add(lFoot);

    this.rightLeg = new THREE.Group();
    this.rightLeg.position.set(0.13, 0.54, 0);
    this.group.add(this.rightLeg);
    const rThigh = new THREE.Mesh(thighGeo, bodyMat);
    rThigh.position.y = -0.15;
    this.rightLeg.add(rThigh);
    this.rightShin = new THREE.Group();
    this.rightShin.position.set(0, -0.30, 0);
    this.rightLeg.add(this.rightShin);
    const rShin = new THREE.Mesh(shinGeo, bodyMat);
    rShin.position.y = -0.14;
    this.rightShin.add(rShin);
    const rFoot = new THREE.Mesh(footGeo, shoeMat);
    rFoot.position.set(0, -0.26, 0.04);
    this.rightShin.add(rFoot);

    // 5. Floor Glow Ring
    const ringGeo = new THREE.RingGeometry(0.52, 0.64, 24);
    ringGeo.rotateX(-Math.PI / 2);
    const ringMat = new THREE.MeshBasicMaterial({
      color: config.colorHex,
      side: THREE.DoubleSide,
      transparent: true,
      opacity: 0.35
    });
    this.floorRing = new THREE.Mesh(ringGeo, ringMat);
    this.floorRing.position.y = 0.06;
    this.group.add(this.floorRing);

    // 6. Floating Nameplate + Speech Bubble
    this.nameplate = this.createSprite(360, 88, 2.0, 0.49);
    this.nameplate.position.set(0, 1.98, 0);
    this.group.add(this.nameplate);

    this.speechBubble = this.createSprite(440, 84, 2.55, 0.49);
    this.speechBubble.position.set(0, 2.55, 0);
    this.speechBubble.visible = false;
    this.group.add(this.speechBubble);

    this.setAgentStatus('IDLE');
  }

  private createSprite(w: number, h: number, scaleX: number, scaleY: number): THREE.Sprite {
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const texture = new THREE.CanvasTexture(canvas);
    texture.minFilter = THREE.LinearFilter;
    const mat = new THREE.SpriteMaterial({ map: texture, transparent: true });
    const sprite = new THREE.Sprite(mat);
    sprite.scale.set(scaleX, scaleY, 1);
    return sprite;
  }

  private renderNameplate(status: AgentVisualStatus, pct?: number) {
    const canvas = document.createElement('canvas');
    canvas.width = 360;
    canvas.height = 88;
    const ctx = canvas.getContext('2d')!;

    const isWork = status === 'WORK';
    const isWait = status === 'WAIT';
    const isFail = status === 'FAIL';
    const isKilled = status === 'KILLED';

    ctx.fillStyle = isWork ? 'rgba(255, 255, 255, 0.96)' : 'rgba(248, 250, 252, 0.92)';
    ctx.strokeStyle = isWork ? '#10B981' : isWait ? '#F59E0B' : isFail ? '#EF4444' : '#94A3B8';
    ctx.lineWidth = isWork ? 5 : 3;

    ctx.beginPath();
    ctx.roundRect(6, 6, 348, 76, 38);
    ctx.fill();
    ctx.stroke();

    // Dot
    ctx.fillStyle = isWork ? '#10B981' : isWait ? '#F59E0B' : isFail ? '#EF4444' : isKilled ? '#64748B' : '#3B82F6';
    ctx.beginPath();
    ctx.arc(34, 44, 11, 0, Math.PI * 2);
    ctx.fill();

    // Name
    ctx.font = 'bold 24px "Be Vietnam Pro", sans-serif';
    ctx.fillStyle = '#0F172A';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    ctx.fillText(`${this.config.badge} ${this.config.shortName}`, 54, 31);

    // Status sublabel
    let statusText = 'Sẵn sàng';
    let statusColor = '#64748B';
    if (isWork) {
      statusText = pct && pct > 0 ? `Đang làm (${pct}%)` : 'Đang làm việc';
      statusColor = '#059669';
    } else if (isWait) {
      statusText = 'Chờ xử lý';
      statusColor = '#D97706';
    } else if (isFail) {
      statusText = 'Gặp lỗi / Cản trở';
      statusColor = '#DC2626';
    } else if (isKilled) {
      statusText = 'Đã dừng';
      statusColor = '#64748B';
    }

    ctx.font = 'bold 17px "Be Vietnam Pro", sans-serif';
    ctx.fillStyle = statusColor;
    ctx.fillText(statusText, 56, 60);

    if (this.nameplateTex) this.nameplateTex.dispose();
    this.nameplateTex = new THREE.CanvasTexture(canvas);
    this.nameplateTex.minFilter = THREE.LinearFilter;
    this.nameplate.material.map = this.nameplateTex;
    this.nameplate.material.needsUpdate = true;
  }

  public setSpeechBubble(text: string | null) {
    if (!text || !text.trim()) {
      this.speechBubble.visible = false;
      return;
    }

    const canvas = document.createElement('canvas');
    canvas.width = 440;
    canvas.height = 84;
    const ctx = canvas.getContext('2d')!;

    ctx.fillStyle = 'rgba(15, 23, 42, 0.90)';
    ctx.strokeStyle = this.config.color;
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.roundRect(6, 6, 428, 62, 20);
    ctx.fill();
    ctx.stroke();

    // Pointer triangle
    ctx.fillStyle = 'rgba(15, 23, 42, 0.90)';
    ctx.beginPath();
    ctx.moveTo(208, 68);
    ctx.lineTo(220, 82);
    ctx.lineTo(232, 68);
    ctx.closePath();
    ctx.fill();

    const clean = text.replace(/\s+/g, ' ').trim();
    const display = clean.length > 36 ? clean.slice(0, 35) + '…' : clean;
    ctx.fillStyle = '#F8FAFC';
    ctx.font = 'bold 19px "Be Vietnam Pro", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(display, 220, 37);

    if (this.bubbleTex) this.bubbleTex.dispose();
    this.bubbleTex = new THREE.CanvasTexture(canvas);
    this.bubbleTex.minFilter = THREE.LinearFilter;
    this.speechBubble.material.map = this.bubbleTex;
    this.speechBubble.material.needsUpdate = true;
    this.speechBubble.visible = true;
  }

  public setAgentStatus(status: AgentVisualStatus, taskDetail?: string, pct?: number) {
    const changedStatus = this.currentStatus !== status;
    const changedTask = taskDetail !== undefined && taskDetail !== this.currentTask;

    this.currentStatus = status;
    if (taskDetail !== undefined) this.currentTask = taskDetail;

    if (changedStatus || pct !== undefined) {
      this.renderNameplate(status, pct);
    }
    if (changedTask || changedStatus) {
      if (status === 'WORK' || status === 'WAIT' || status === 'FAIL') {
        this.setSpeechBubble(this.currentTask || null);
      } else if (this.currentTask && this.currentTask.startsWith('✅')) {
        this.setSpeechBubble(this.currentTask);
      } else {
        this.setSpeechBubble(null);
      }
    }

    const ringMat = this.floorRing.material as THREE.MeshBasicMaterial;
    if (status === 'WORK') {
      ringMat.color.setHex(0x10B981);
      ringMat.opacity = 0.9;
    } else if (status === 'WAIT') {
      ringMat.color.setHex(0xF59E0B);
      ringMat.opacity = 0.8;
    } else if (status === 'FAIL') {
      ringMat.color.setHex(0xEF4444);
      ringMat.opacity = 0.9;
    } else {
      ringMat.color.setHex(this.config.colorHex);
      ringMat.opacity = 0.25;
    }
  }

  public setFloorRingActive(active: boolean) {
    const mat = this.floorRing.material as THREE.MeshBasicMaterial;
    mat.opacity = active ? 0.9 : 0.25;
  }

  public update(delta: number) {
    this.animTime += delta;
    if (this.currentStatus === 'WORK') {
      const pulse = 1.0 + Math.sin(this.animTime * 5) * 0.14;
      this.floorRing.scale.set(pulse, pulse, pulse);
    } else {
      this.floorRing.scale.set(1, 1, 1);
    }

    if (this.speechBubble.visible) {
      this.speechBubble.position.y = 2.55 + Math.sin(this.animTime * 3) * 0.04;
    }

    this.blinkTimer += delta;
    if (!this.isBlinking && this.blinkTimer > 2.8) {
      this.isBlinking = true;
      this.leftEye.scale.y = 0.1;
      this.rightEye.scale.y = 0.1;
    } else if (this.isBlinking && this.blinkTimer > 2.95) {
      this.isBlinking = false;
      this.leftEye.scale.y = 1.35;
      this.rightEye.scale.y = 1.35;
      this.blinkTimer = Math.random() * -1.2;
    }
  }
}
