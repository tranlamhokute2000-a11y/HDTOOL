import * as THREE from 'three';

export class CentralModelTable {
  public group: THREE.Group;
  public rotatingModel: THREE.Group;

  // Incremental Building Components
  public gridMesh: THREE.GridHelper;
  public columnsGroup: THREE.Group;
  public floorsGroup: THREE.Group;
  public facadeGroup: THREE.Group;
  public testWindows: THREE.Mesh[] = [];
  public fenceGroup: THREE.Group;
  public roofMesh: THREE.Mesh;
  public flagGroup: THREE.Group;

  // Confetti particles
  public confettiParticles: THREE.Points | null = null;
  public isCelebrating = false;

  constructor() {
    this.group = new THREE.Group();
    this.group.position.set(-4.0, 0, -7.2);
    this.group.scale.setScalar(0.56);

    // 1. Round Display Table (r = 3.2m, height 0.75m)
    const tableTopGeo = new THREE.CylinderGeometry(3.2, 3.2, 0.1, 32);
    const tableTopMat = new THREE.MeshToonMaterial({ color: 0xCDAE83 });
    const tableTop = new THREE.Mesh(tableTopGeo, tableTopMat);
    tableTop.position.y = 0.75;
    tableTop.receiveShadow = true;
    this.group.add(tableTop);

    const tableBaseGeo = new THREE.CylinderGeometry(1.4, 2.0, 0.7, 24);
    const tableBaseMat = new THREE.MeshToonMaterial({ color: 0x334155 });
    const tableBase = new THREE.Mesh(tableBaseGeo, tableBaseMat);
    tableBase.position.y = 0.35;
    this.group.add(tableBase);

    // 2. Rotating Architectural BIM Model Group (Y = 0.8)
    this.rotatingModel = new THREE.Group();
    this.rotatingModel.position.set(0, 0.8, 0);
    this.group.add(this.rotatingModel);

    // Stage 1: Spec - Blue Blueprint Grid
    this.gridMesh = new THREE.GridHelper(3.8, 10, 0x1D5FD1, 0x60A5FA);
    this.gridMesh.position.y = 0.02;
    this.gridMesh.visible = false;
    this.rotatingModel.add(this.gridMesh);

    // Stage 2: Design - Structural Columns & Beams
    this.columnsGroup = new THREE.Group();
    const colGeo = new THREE.BoxGeometry(0.12, 1.8, 0.12);
    const colMat = new THREE.MeshToonMaterial({ color: 0x94A3B8 });
    const colPositions = [
      [-1.2, -1.2], [1.2, -1.2],
      [-1.2, 1.2], [1.2, 1.2],
      [-1.2, 0], [1.2, 0]
    ];
    colPositions.forEach(([cx, cz]) => {
      const col = new THREE.Mesh(colGeo, colMat);
      col.position.set(cx, 0.9, cz);
      this.columnsGroup.add(col);
    });
    this.columnsGroup.visible = false;
    this.rotatingModel.add(this.columnsGroup);

    // Stage 3: Code - Stacked Floor Slabs
    this.floorsGroup = new THREE.Group();
    const slabGeo = new THREE.BoxGeometry(2.8, 0.08, 2.8);
    const slabMat = new THREE.MeshToonMaterial({ color: 0xEDF1EE }); // Canson Paper
    for (let f = 0; f < 3; f++) {
      const slab = new THREE.Mesh(slabGeo, slabMat);
      slab.position.y = 0.1 + f * 0.7;
      this.floorsGroup.add(slab);
    }
    this.floorsGroup.visible = false;
    this.rotatingModel.add(this.floorsGroup);

    // Stage 4: Review - Exterior Facade & Curtain Wall Panels
    this.facadeGroup = new THREE.Group();
    const panelGeo = new THREE.BoxGeometry(2.76, 2.0, 0.04);
    const panelMat = new THREE.MeshBasicMaterial({ color: 0x60A5FA, transparent: true, opacity: 0.35 });
    const pFront = new THREE.Mesh(panelGeo, panelMat);
    pFront.position.set(0, 1.0, 1.38);
    this.facadeGroup.add(pFront);
    const pBack = new THREE.Mesh(panelGeo, panelMat);
    pBack.position.set(0, 1.0, -1.38);
    this.facadeGroup.add(pBack);
    this.facadeGroup.visible = false;
    this.rotatingModel.add(this.facadeGroup);

    // Stage 5: Test - Window Lighting (12 windows)
    const winGeo = new THREE.BoxGeometry(0.3, 0.35, 0.06);
    for (let w = 0; w < 12; w++) {
      const winMat = new THREE.MeshBasicMaterial({ color: 0x10B981 });
      const win = new THREE.Mesh(winGeo, winMat);
      const row = Math.floor(w / 4);
      const col = w % 4;
      win.position.set(-1.0 + col * 0.65, 0.4 + row * 0.7, 1.4);
      this.rotatingModel.add(win);
      win.visible = false;
      this.testWindows.push(win);
    }

    // Stage 6: Security - Perimeter Fence & Glowing Lock
    this.fenceGroup = new THREE.Group();
    const fenceMat = new THREE.MeshBasicMaterial({ color: 0x3B82F6 });
    const postGeo = new THREE.CylinderGeometry(0.04, 0.04, 0.5, 8);
    for (let i = 0; i < 8; i++) {
      const angle = (i / 8) * Math.PI * 2;
      const post = new THREE.Mesh(postGeo, fenceMat);
      post.position.set(Math.cos(angle) * 2.2, 0.25, Math.sin(angle) * 2.2);
      this.fenceGroup.add(post);
    }
    this.fenceGroup.visible = false;
    this.rotatingModel.add(this.fenceGroup);

    // Stage 7: Package - Roof Slab & Gift Box Ribbon
    const roofGeo = new THREE.ConeGeometry(2.2, 0.8, 4);
    roofGeo.rotateY(Math.PI / 4);
    const roofMat = new THREE.MeshToonMaterial({ color: 0x334155 });
    this.roofMesh = new THREE.Mesh(roofGeo, roofMat);
    this.roofMesh.position.y = 2.5;
    this.roofMesh.visible = false;
    this.rotatingModel.add(this.roofMesh);

    // Stage 8: Docs - Tool Nameplate & Victory Flag
    this.flagGroup = new THREE.Group();
    this.flagGroup.position.set(0, 2.9, 0);
    const poleGeo = new THREE.CylinderGeometry(0.02, 0.02, 0.8, 8);
    const poleMat = new THREE.MeshBasicMaterial({ color: 0xF59E0B });
    const pole = new THREE.Mesh(poleGeo, poleMat);
    this.flagGroup.add(pole);

    const flagGeo = new THREE.BoxGeometry(0.4, 0.25, 0.02);
    const flagMat = new THREE.MeshBasicMaterial({ color: 0xEF4444 });
    const flag = new THREE.Mesh(flagGeo, flagMat);
    flag.position.set(0.2, 0.3, 0);
    this.flagGroup.add(flag);
    this.flagGroup.visible = false;
    this.rotatingModel.add(this.flagGroup);
  }

  public setStageUnlocked(stage: string) {
    switch (stage) {
      case 'spec':
        this.gridMesh.visible = true;
        break;
      case 'design':
        this.gridMesh.visible = true;
        this.columnsGroup.visible = true;
        break;
      case 'code':
        this.gridMesh.visible = true;
        this.columnsGroup.visible = true;
        this.floorsGroup.visible = true;
        break;
      case 'review':
        this.facadeGroup.visible = true;
        break;
      case 'test':
        this.testWindows.forEach(w => { w.visible = true; });
        break;
      case 'security':
        this.fenceGroup.visible = true;
        break;
      case 'package':
        this.roofMesh.visible = true;
        break;
      case 'docs':
        this.flagGroup.visible = true;
        break;
    }
  }

  public triggerCelebration(scene: THREE.Scene) {
    this.isCelebrating = true;

    // Confetti particles
    const particleCount = 200;
    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(particleCount * 3);
    const colors = new Float32Array(particleCount * 3);

    const palette = [
      new THREE.Color(0xFF8A1F),
      new THREE.Color(0x2DBE70),
      new THREE.Color(0x1D5FD1),
      new THREE.Color(0xF5B700),
      new THREE.Color(0xEF4444)
    ];

    for (let i = 0; i < particleCount; i++) {
      positions[i * 3] = (Math.random() - 0.5) * 5;
      positions[i * 3 + 1] = 1.5 + Math.random() * 4;
      positions[i * 3 + 2] = (Math.random() - 0.5) * 5;

      const col = palette[Math.floor(Math.random() * palette.length)];
      colors[i * 3] = col.r;
      colors[i * 3 + 1] = col.g;
      colors[i * 3 + 2] = col.b;
    }

    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

    const material = new THREE.PointsMaterial({
      size: 0.15,
      vertexColors: true,
      transparent: true,
      opacity: 0.9
    });

    this.confettiParticles = new THREE.Points(geometry, material);
    this.confettiParticles.position.copy(this.group.position);
    scene.add(this.confettiParticles);
  }

  public update(delta: number) {
    // 1 rotation every 90 seconds = (2 * PI) / 90 rad/s ~ 0.07 rad/s
    this.rotatingModel.rotation.y += delta * 0.07;

    if (this.isCelebrating && this.confettiParticles) {
      const pos = this.confettiParticles.geometry.attributes.position.array as Float32Array;
      for (let i = 1; i < pos.length; i += 3) {
        pos[i] -= delta * 0.8;
        if (pos[i] < 0.8) pos[i] = 4.5;
      }
      this.confettiParticles.geometry.attributes.position.needsUpdate = true;
    }
  }
}

