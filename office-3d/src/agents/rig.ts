import * as THREE from 'three';
import { RoleConfig } from './roles.js';

export class AgentRig {
  public group: THREE.Group;
  public config: RoleConfig;

  // Joints and parts for procedural animation
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

  // Floating UI and attachments
  public nameplate: THREE.Sprite;
  public floorRing: THREE.Mesh;
  public handAttachment: THREE.Group; // for carrying items
  public propMesh: THREE.Object3D | null = null;
  public speechBubble: THREE.Sprite | null = null;

  // Face elements
  public leftEye: THREE.Mesh;
  public rightEye: THREE.Mesh;

  // Animation values
  public blinkTimer = 0;
  public isBlinking = false;
  public quoteTimer = 0;
  public currentStatus: 'WORK' | 'IDLE' | 'WAIT' | 'FAIL' = 'IDLE';
  public currentTask = '';
  public animTime = 0;

  constructor(config: RoleConfig) {
    this.config = config;
    this.group = new THREE.Group();
    this.group.name = `agent_${config.id}`;
    this.group.position.set(...config.deskPos);
    this.group.rotation.y = config.deskRot;

    // Materials (MeshToonMaterial for clean stylized BIM look)
    const skinMat = new THREE.MeshToonMaterial({ color: 0xFCD34D });
    const eyeMat = new THREE.MeshBasicMaterial({ color: 0x1E293B });
    const bodyMat = new THREE.MeshToonMaterial({ color: config.outfit.bodyColor });
    const vestMat = new THREE.MeshToonMaterial({ color: config.outfit.vestColor });
    const stripeMat = new THREE.MeshBasicMaterial({ color: 0xFFFFFF });
    const shoeMat = new THREE.MeshToonMaterial({ color: 0x334155 });
    const hatMat = new THREE.MeshToonMaterial({ color: config.outfit.hatColor });

    // 1. Torso (Capsule / Cylinder)
    const torsoGeo = new THREE.CylinderGeometry(0.28, 0.25, 0.65, 12);
    this.torsoMesh = new THREE.Mesh(torsoGeo, bodyMat);
    this.torsoMesh.position.y = 0.85;
    this.torsoMesh.castShadow = true;
    this.group.add(this.torsoMesh);

    // Reflective vest layer
    const vestGeo = new THREE.CylinderGeometry(0.29, 0.26, 0.5, 12);
    const vestMesh = new THREE.Mesh(vestGeo, vestMat);
    vestMesh.position.y = 0.05;
    this.torsoMesh.add(vestMesh);

    // Reflective horizontal stripes
    const stripeGeo = new THREE.CylinderGeometry(0.295, 0.265, 0.06, 12);
    const stripeMesh = new THREE.Mesh(stripeGeo, stripeMat);
    stripeMesh.position.y = 0;
    vestMesh.add(stripeMesh);

    // 2. Head Group
    this.headGroup = new THREE.Group();
    this.headGroup.position.set(0, 0.52, 0);
    this.torsoMesh.add(this.headGroup);

    // Head sphere (r = 0.38)
    const headGeo = new THREE.SphereGeometry(0.38, 16, 16);
    const headMesh = new THREE.Mesh(headGeo, skinMat);
    headMesh.castShadow = true;
    this.headGroup.add(headMesh);

    // Eyes
    const eyeGeo = new THREE.SphereGeometry(0.045, 8, 8);
    eyeGeo.scale(1, 1.4, 0.6);
    this.leftEye = new THREE.Mesh(eyeGeo, eyeMat);
    this.leftEye.position.set(-0.13, 0.04, 0.34);
    this.rightEye = new THREE.Mesh(eyeGeo, eyeMat);
    this.rightEye.position.set(0.13, 0.04, 0.34);
    this.headGroup.add(this.leftEye);
    this.headGroup.add(this.rightEye);

    // Hat / Helmet
    if (config.outfit.hatType === 'helmet') {
      const helmetGeo = new THREE.SphereGeometry(0.40, 16, 12, 0, Math.PI * 2, 0, Math.PI * 0.55);
      const helmet = new THREE.Mesh(helmetGeo, hatMat);
      helmet.position.y = 0.08;
      this.headGroup.add(helmet);

      // Helmet brim
      const brimGeo = new THREE.CylinderGeometry(0.44, 0.44, 0.04, 16);
      const brim = new THREE.Mesh(brimGeo, hatMat);
      brim.position.set(0, 0.06, 0.08);
      this.headGroup.add(brim);
    } else if (config.outfit.hatType === 'beret') {
      const beretGeo = new THREE.CylinderGeometry(0.42, 0.35, 0.12, 12);
      const beret = new THREE.Mesh(beretGeo, hatMat);
      beret.position.set(0.05, 0.32, -0.05);
      beret.rotation.z = -0.2;
      this.headGroup.add(beret);
    } else if (config.outfit.hatType === 'miner') {
      const helmetGeo = new THREE.SphereGeometry(0.40, 16, 12, 0, Math.PI * 2, 0, Math.PI * 0.55);
      const helmet = new THREE.Mesh(helmetGeo, hatMat);
      helmet.position.y = 0.08;
      this.headGroup.add(helmet);

      // Flashlight lamp on forehead
      const lampGeo = new THREE.CylinderGeometry(0.06, 0.08, 0.1, 8);
      const lampMat = new THREE.MeshBasicMaterial({ color: 0xFEF08A });
      const lamp = new THREE.Mesh(lampGeo, lampMat);
      lamp.rotation.x = Math.PI / 2;
      lamp.position.set(0, 0.28, 0.38);
      this.headGroup.add(lamp);
    }

    // 3. Arms
    const armGeo = new THREE.CylinderGeometry(0.07, 0.065, 0.32, 8);
    const forearmGeo = new THREE.CylinderGeometry(0.065, 0.06, 0.28, 8);
    const handGeo = new THREE.SphereGeometry(0.075, 8, 8);

    // Left Arm
    this.leftArm = new THREE.Group();
    this.leftArm.position.set(-0.35, 0.22, 0);
    this.torsoMesh.add(this.leftArm);
    const leftArmMesh = new THREE.Mesh(armGeo, vestMat);
    leftArmMesh.position.y = -0.14;
    this.leftArm.add(leftArmMesh);

    this.leftForearm = new THREE.Group();
    this.leftForearm.position.set(0, -0.3, 0);
    this.leftArm.add(this.leftForearm);
    const leftForearmMesh = new THREE.Mesh(forearmGeo, skinMat);
    leftForearmMesh.position.y = -0.12;
    this.leftForearm.add(leftForearmMesh);
    const leftHand = new THREE.Mesh(handGeo, skinMat);
    leftHand.position.y = -0.26;
    this.leftForearm.add(leftHand);

    // Right Arm
    this.rightArm = new THREE.Group();
    this.rightArm.position.set(0.35, 0.22, 0);
    this.torsoMesh.add(this.rightArm);
    const rightArmMesh = new THREE.Mesh(armGeo, vestMat);
    rightArmMesh.position.y = -0.14;
    this.rightArm.add(rightArmMesh);

    this.rightForearm = new THREE.Group();
    this.rightForearm.position.set(0, -0.3, 0);
    this.rightArm.add(this.rightForearm);
    const rightForearmMesh = new THREE.Mesh(forearmGeo, skinMat);
    rightForearmMesh.position.y = -0.12;
    this.rightForearm.add(rightForearmMesh);
    const rightHand = new THREE.Mesh(handGeo, skinMat);
    rightHand.position.y = -0.26;
    this.rightForearm.add(rightHand);

    // Hand Attachment (for giving/receiving documents/props)
    this.handAttachment = new THREE.Group();
    this.handAttachment.position.set(0, -0.28, 0.1);
    this.rightForearm.add(this.handAttachment);

    // 4. Legs
    const thighGeo = new THREE.CylinderGeometry(0.09, 0.08, 0.35, 8);
    const shinGeo = new THREE.CylinderGeometry(0.08, 0.075, 0.32, 8);
    const footGeo = new THREE.BoxGeometry(0.14, 0.1, 0.22);

    // Left Leg
    this.leftLeg = new THREE.Group();
    this.leftLeg.position.set(-0.14, 0.55, 0);
    this.group.add(this.leftLeg);
    const leftThighMesh = new THREE.Mesh(thighGeo, bodyMat);
    leftThighMesh.position.y = -0.16;
    this.leftLeg.add(leftThighMesh);

    this.leftShin = new THREE.Group();
    this.leftShin.position.set(0, -0.32, 0);
    this.leftLeg.add(this.leftShin);
    const leftShinMesh = new THREE.Mesh(shinGeo, bodyMat);
    leftShinMesh.position.y = -0.14;
    this.leftShin.add(leftShinMesh);
    const leftFoot = new THREE.Mesh(footGeo, shoeMat);
    leftFoot.position.set(0, -0.28, 0.04);
    this.leftShin.add(leftFoot);

    // Right Leg
    this.rightLeg = new THREE.Group();
    this.rightLeg.position.set(0.14, 0.55, 0);
    this.group.add(this.rightLeg);
    const rightThighMesh = new THREE.Mesh(thighGeo, bodyMat);
    rightThighMesh.position.y = -0.16;
    this.rightLeg.add(rightThighMesh);

    this.rightShin = new THREE.Group();
    this.rightShin.position.set(0, -0.32, 0);
    this.rightLeg.add(this.rightShin);
    const rightShinMesh = new THREE.Mesh(shinGeo, bodyMat);
    rightShinMesh.position.y = -0.14;
    this.rightShin.add(rightShinMesh);
    const rightFoot = new THREE.Mesh(footGeo, shoeMat);
    rightFoot.position.set(0, -0.28, 0.04);
    this.rightShin.add(rightFoot);

    // 5. Floor Ring (Activity Indicator)
    const ringGeo = new THREE.RingGeometry(0.55, 0.65, 24);
    ringGeo.rotateX(-Math.PI / 2);
    const ringMat = new THREE.MeshBasicMaterial({
      color: config.colorHex,
      side: THREE.DoubleSide,
      transparent: true,
      opacity: 0.25
    });
    this.floorRing = new THREE.Mesh(ringGeo, ringMat);
    this.floorRing.position.y = 0.02;
    this.group.add(this.floorRing);

    // 6. Floating Nameplate (Canvas Texture)
    this.nameplate = this.createNameplateSprite(config.badge, config.shortName, config.color);
    this.nameplate.position.set(0, 1.95, 0);
    this.group.add(this.nameplate);
  }

  private createNameplateSprite(badge: string, name: string, color: string): THREE.Sprite {
    const canvas = document.createElement('canvas');
    canvas.width = 320;
    canvas.height = 96;
    const ctx = canvas.getContext('2d')!;
    this.drawNameplateCanvas(ctx, 320, 96, 'IDLE');

    const texture = new THREE.CanvasTexture(canvas);
    texture.minFilter = THREE.LinearFilter;
    const spriteMat = new THREE.SpriteMaterial({ map: texture, transparent: true });
    const sprite = new THREE.Sprite(spriteMat);
    sprite.scale.set(1.8, 0.54, 1);
    return sprite;
  }

  private drawNameplateCanvas(ctx: CanvasRenderingContext2D, w: number, h: number, status: 'WORK' | 'IDLE' | 'WAIT' | 'FAIL', pct?: number) {
    ctx.clearRect(0, 0, w, h);

    const isWork = status === 'WORK';
    const isWait = status === 'WAIT';
    const isFail = status === 'FAIL';

    // 1. Outer Box / Pill
    ctx.fillStyle = isWork ? 'rgba(6, 78, 59, 0.95)' : isWait ? 'rgba(120, 53, 15, 0.95)' : isFail ? 'rgba(127, 29, 29, 0.95)' : 'rgba(15, 23, 42, 0.92)';
    ctx.strokeStyle = isWork ? '#10B981' : isWait ? '#F59E0B' : isFail ? '#EF4444' : '#475569';
    ctx.lineWidth = isWork ? 5 : 2;

    ctx.beginPath();
    ctx.roundRect(8, 6, w - 16, h - 12, 16);
    ctx.fill();
    ctx.stroke();

    // 2. Line 1: Role Name
    ctx.font = 'bold 22px "Be Vietnam Pro", sans-serif';
    ctx.fillStyle = '#FFFFFF';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(`${this.config.badge} ${this.config.shortName}`, w / 2, 28);

    // 3. Line 2: Status Tag
    let statusText = '💤 ĐANG NGHỈ';
    let statusBg = '#334155';
    let statusFg = '#CBD5E1';

    if (isWork) {
      statusText = pct !== undefined && pct > 0 ? `🟢 ĐANG LÀM (${pct}%)` : '🟢 ĐANG LÀM VIỆC';
      statusBg = '#047857';
      statusFg = '#A7F3D0';
    } else if (isWait) {
      statusText = '⏳ ĐANG CHỜ PHẢN HỒI';
      statusBg = '#B45309';
      statusFg = '#FDE68A';
    } else if (isFail) {
      statusText = '❌ GẶP SỰ CỐ / LỖI';
      statusBg = '#B91C1C';
      statusFg = '#FECACA';
    }

    // Status pill background
    const pillW = 200;
    const pillH = 28;
    const pillX = (w - pillW) / 2;
    const pillY = 54;
    ctx.fillStyle = statusBg;
    ctx.beginPath();
    ctx.roundRect(pillX, pillY, pillW, pillH, 14);
    ctx.fill();

    // Status text
    ctx.font = 'bold 15px "Be Vietnam Pro", sans-serif';
    ctx.fillStyle = statusFg;
    ctx.fillText(statusText, w / 2, pillY + pillH / 2);
  }

  public setAgentStatus(status: 'WORK' | 'IDLE' | 'WAIT' | 'FAIL', taskDetail?: string, pct?: number) {
    this.currentStatus = status;
    if (taskDetail) this.currentTask = taskDetail;

    const canvas = document.createElement('canvas');
    canvas.width = 320;
    canvas.height = 96;
    const ctx = canvas.getContext('2d')!;
    this.drawNameplateCanvas(ctx, 320, 96, status, pct);

    const texture = new THREE.CanvasTexture(canvas);
    texture.minFilter = THREE.LinearFilter;
    this.nameplate.material.map = texture;
    this.nameplate.material.needsUpdate = true;

    // Floor ring update
    const ringMat = this.floorRing.material as THREE.MeshBasicMaterial;
    if (status === 'WORK') {
      ringMat.color.setHex(0x10B981);
      ringMat.opacity = 0.95;
    } else if (status === 'WAIT') {
      ringMat.color.setHex(0xF59E0B);
      ringMat.opacity = 0.8;
    } else if (status === 'FAIL') {
      ringMat.color.setHex(0xEF4444);
      ringMat.opacity = 0.8;
    } else {
      ringMat.color.setHex(this.config.colorHex);
      ringMat.opacity = 0.15;
    }
  }

  public updateNameplate(detail?: string, pct?: number) {
    this.setAgentStatus(this.currentStatus, detail, pct);
  }

  public setFloorRingActive(active: boolean) {
    const mat = this.floorRing.material as THREE.MeshBasicMaterial;
    mat.opacity = active ? 0.9 : 0.2;
  }

  public update(delta: number) {
    // Blinking
    this.animTime += delta;
    if (this.currentStatus === 'WORK') {
      // Pulse effect on floor ring
      const pulse = 1.0 + Math.sin(this.animTime * 5) * 0.12;
      this.floorRing.scale.set(pulse, pulse, pulse);
    } else {
      this.floorRing.scale.set(1, 1, 1);
    }
    this.blinkTimer += delta;
    if (!this.isBlinking && this.blinkTimer > 2.5 + Math.random() * 2) {
      this.isBlinking = true;
      this.leftEye.scale.y = 0.1;
      this.rightEye.scale.y = 0.1;
    } else if (this.isBlinking && this.blinkTimer > 2.8) {
      this.isBlinking = false;
      this.leftEye.scale.y = 1.4;
      this.rightEye.scale.y = 1.4;
      this.blinkTimer = 0;
    }
  }
}

