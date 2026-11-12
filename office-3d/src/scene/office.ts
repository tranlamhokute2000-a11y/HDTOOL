import * as THREE from 'three';

export interface SwarmBoardDisplayLine {
  seq: number;
  agent: string;
  text: string;
}

export class OfficeArchitecture {
  public group: THREE.Group;
  public exteriorGroup: THREE.Group;
  public pipelineBoxes: Map<string, THREE.Mesh> = new Map();
  public stageOrder = ['spec', 'design', 'code', 'review', 'test', 'security', 'package', 'docs'];

  private councilScreenMat!: THREE.MeshBasicMaterial;
  private auditoriumScreenMat!: THREE.MeshBasicMaterial;
  private pipelineBannerMat!: THREE.MeshBasicMaterial;

  constructor() {
    this.group = new THREE.Group();
    this.exteriorGroup = new THREE.Group();
    this.group.add(this.exteriorGroup);

    this.buildCampusGround();
    this.buildRoomZones();
    this.buildPartitionWalls();
    this.buildRoomSigns();
    this.buildAuditoriumScreens();
    this.buildCouncilScreen();
    this.buildPipelineBoard();
    this.buildStreetTrees();
    this.buildCitySkyline();
  }

  public setLowGraphics(enabled: boolean) {
    this.exteriorGroup.visible = !enabled;
  }

  private buildCampusGround() {
    // Grass surround
    const grass = new THREE.Mesh(
      new THREE.BoxGeometry(92, 0.2, 68),
      new THREE.MeshLambertMaterial({ color: 0xCBE2C8 })
    );
    grass.position.set(0, -0.35, 0);
    grass.receiveShadow = true;
    this.group.add(grass);

    // Asphalt road around campus
    const road = new THREE.Mesh(
      new THREE.BoxGeometry(62, 0.14, 42),
      new THREE.MeshLambertMaterial({ color: 0x475569 })
    );
    road.position.set(0, -0.22, -1);
    road.receiveShadow = true;
    this.group.add(road);

    // White sidewalk courtyard slab around building
    const plaza = new THREE.Mesh(
      new THREE.BoxGeometry(48, 0.16, 33),
      new THREE.MeshLambertMaterial({ color: 0xE2E8F0 })
    );
    plaza.position.set(0, -0.13, 0);
    plaza.receiveShadow = true;
    this.group.add(plaza);

    // Main interior structural base (41 x 27.5)
    const base = new THREE.Mesh(
      new THREE.BoxGeometry(41, 0.14, 27.5),
      new THREE.MeshLambertMaterial({ color: 0xF1F5F9 })
    );
    base.position.set(0, -0.05, 0);
    base.receiveShadow = true;
    this.group.add(base);
  }

  private addZoneCarpet(x1: number, x2: number, z1: number, z2: number, colorHex: number) {
    const w = x2 - x1;
    const d = z2 - z1;
    const mesh = new THREE.Mesh(
      new THREE.BoxGeometry(w, 0.03, d),
      new THREE.MeshLambertMaterial({ color: colorHex })
    );
    mesh.position.set((x1 + x2) / 2, 0.03, (z1 + z2) / 2);
    mesh.receiveShadow = true;
    this.group.add(mesh);
  }

  private buildRoomZones() {
    // 1. Điều phối & Giám đốc (Top-Left)
    this.addZoneCarpet(-20.2, -10.0, -13.4, -2.2, 0xC69C6D);
    // 2. Hình học & Kiến trúc (Top-Center, pastel lavender rug)
    this.addZoneCarpet(-10.0, 4.0, -13.4, -2.2, 0xDDD6FE);
    // 3. Lập trình & Giao diện (Top-Right, warm ash oak)
    this.addZoneCarpet(4.0, 20.2, -13.4, -2.2, 0xE7C8A0);
    // 4. Hội đồng Swarm (Center glass pod)
    this.addZoneCarpet(-10.0, 8.0, -2.2, 5.4, 0x334155);
    // 5. Kiểm thử & Build (Mid-Right glass pod)
    this.addZoneCarpet(8.0, 20.2, -2.2, 5.4, 0x475569);
    // 6. Hội trường Bàn giao (Bottom-Left crimson hall)
    this.addZoneCarpet(-20.2, 4.0, 5.4, 13.4, 0x7F1D1D);
    // 7. Lễ tân & Chỉ huy (Bottom-Right marble lobby)
    this.addZoneCarpet(4.0, 20.2, 5.4, 13.4, 0xF8FAFC);

    // Subtle interior walkway strip
    const grid = new THREE.GridHelper(40, 20, 0x94A3B8, 0xCBD5E1);
    grid.position.set(0, 0.05, 0);
    (grid.material as THREE.Material).transparent = true;
    (grid.material as THREE.Material).opacity = 0.14;
    this.group.add(grid);
  }

  private addSolidWall(x1: number, z1: number, x2: number, z2: number, height = 2.8, color = 0xFFFFFF) {
    const dx = x2 - x1;
    const dz = z2 - z1;
    const len = Math.hypot(dx, dz);
    if (len < 0.05) return;
    const wall = new THREE.Mesh(
      new THREE.BoxGeometry(len, height, 0.22),
      new THREE.MeshLambertMaterial({ color })
    );
    wall.position.set((x1 + x2) / 2, height / 2, (z1 + z2) / 2);
    wall.rotation.y = -Math.atan2(dz, dx);
    wall.castShadow = true;
    wall.receiveShadow = true;
    this.group.add(wall);
  }

  private addGlassPartition(x1: number, z1: number, x2: number, z2: number, height = 2.45) {
    const dx = x2 - x1;
    const dz = z2 - z1;
    const len = Math.hypot(dx, dz);
    if (len < 0.05) return;
    const cx = (x1 + x2) / 2;
    const cz = (z1 + z2) / 2;
    const angle = -Math.atan2(dz, dx);

    const glassGroup = new THREE.Group();
    glassGroup.position.set(cx, 0, cz);
    glassGroup.rotation.y = angle;

    // Glass sheet
    const glass = new THREE.Mesh(
      new THREE.BoxGeometry(len, height, 0.05),
      new THREE.MeshPhongMaterial({
        color: 0xBAE6FD,
        transparent: true,
        opacity: 0.24,
        shininess: 90
      })
    );
    glass.position.y = height / 2;
    glassGroup.add(glass);

    // Top & bottom aluminum frame
    const frameMat = new THREE.MeshLambertMaterial({ color: 0x1E293B });
    const topRail = new THREE.Mesh(new THREE.BoxGeometry(len, 0.08, 0.09), frameMat);
    topRail.position.y = height;
    glassGroup.add(topRail);

    const botRail = new THREE.Mesh(new THREE.BoxGeometry(len, 0.12, 0.09), frameMat);
    botRail.position.y = 0.06;
    glassGroup.add(botRail);

    // Vertical mullions every ~2.6m
    const divs = Math.max(1, Math.round(len / 2.6));
    for (let i = 0; i <= divs; i++) {
      const mx = -len / 2 + (i / divs) * len;
      const post = new THREE.Mesh(new THREE.BoxGeometry(0.06, height, 0.09), frameMat);
      post.position.set(mx, height / 2, 0);
      glassGroup.add(post);
    }

    this.group.add(glassGroup);
  }

  private buildPartitionWalls() {
    // Exterior white architectural cutaway walls
    this.addSolidWall(-20.4, -13.6, 20.4, -13.6, 3.0, 0xE2E8F0); // North
    this.addSolidWall(-20.4, -13.6, -20.4, 13.6, 2.8, 0xCBD5E1); // West
    this.addSolidWall(20.4, -13.6, 20.4, 13.6, 2.5, 0xCBD5E1);  // East

    // South perimeter with glass entrance on Lobby side
    this.addSolidWall(-20.4, 13.6, 4.0, 13.6, 1.4, 0xCBD5E1);
    this.addGlassPartition(4.0, 13.6, 9.5, 13.6, 2.3);
    // Entrance gap [9.5 .. 14.5]
    this.addGlassPartition(14.5, 13.6, 20.4, 13.6, 2.3);

    // North wing room dividers (with doorways)
    this.addGlassPartition(-10.0, -13.4, -10.0, -6.5);
    this.addGlassPartition(-10.0, -4.0, -10.0, -2.2);
    this.addGlassPartition(4.0, -13.4, 4.0, -6.5);
    this.addGlassPartition(4.0, -4.0, 4.0, -2.2);

    // Mid horizontal partitions (Z = -2.2) with open corridors
    this.addGlassPartition(-20.2, -2.2, -14.0, -2.2);
    this.addGlassPartition(-10.0, -2.2, -4.5, -2.2);
    this.addGlassPartition(-1.5, -2.2, 6.0, -2.2);
    this.addGlassPartition(9.0, -2.2, 16.5, -2.2);

    // Council / Verification divider (X = 8.0)
    this.addGlassPartition(8.0, -2.2, 8.0, 0.8);
    this.addGlassPartition(8.0, 3.2, 8.0, 5.4);
    this.addGlassPartition(-10.0, -2.2, -10.0, 5.4);

    // Auditorium / Lobby horizontal divider (Z = 5.4) with wide doors
    this.addSolidWall(-20.2, 5.4, -13.5, 5.4, 1.8, 0x991B1B);
    this.addGlassPartition(-10.5, 5.4, 4.0, 5.4, 2.2);
    this.addGlassPartition(4.0, 5.4, 9.0, 5.4, 2.2);
    this.addGlassPartition(14.0, 5.4, 20.2, 5.4, 2.2);
    this.addSolidWall(4.0, 5.4, 4.0, 13.4, 2.2, 0xE2E8F0);
  }

  private createPillTagSprite(label: string, accentHex = '#2563EB'): THREE.Sprite {
    const canvas = document.createElement('canvas');
    canvas.width = 460;
    canvas.height = 84;
    const ctx = canvas.getContext('2d')!;

    ctx.fillStyle = 'rgba(255, 255, 255, 0.94)';
    ctx.strokeStyle = 'rgba(148, 163, 184, 0.55)';
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.roundRect(8, 8, 444, 68, 34);
    ctx.fill();
    ctx.stroke();

    // Accent dot
    ctx.fillStyle = accentHex;
    ctx.beginPath();
    ctx.arc(38, 42, 10, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = '#0F172A';
    ctx.font = 'bold 26px "Be Vietnam Pro", sans-serif';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    ctx.fillText(label, 62, 43);

    const tex = new THREE.CanvasTexture(canvas);
    tex.minFilter = THREE.LinearFilter;
    const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true }));
    sprite.scale.set(4.5, 0.82, 1);
    return sprite;
  }

  private buildRoomSigns() {
    const signs: Array<{ text: string; color: string; pos: [number, number, number] }> = [
      { text: 'ĐIỀU PHỐI & GIÁM ĐỐC', color: '#EA580C', pos: [-15.2, 3.6, -7.8] },
      { text: 'HÌNH HỌC & KIẾN TRÚC', color: '#2563EB', pos: [-3.0, 3.6, -7.8] },
      { text: 'LẬP TRÌNH & GIAO DIỆN', color: '#9333EA', pos: [12.0, 3.6, -7.8] },
      { text: 'HỘI ĐỒNG SWARM', color: '#38BDF8', pos: [-1.0, 3.4, 1.5] },
      { text: 'KIỂM THỬ & BUILD', color: '#10B981', pos: [14.0, 3.4, 1.8] },
      { text: 'HỘI TRƯỜNG BÀN GIAO', color: '#DC2626', pos: [-8.5, 3.4, 9.5] },
      { text: 'LỄ TÂN · HADOMEE', color: '#0EA5E9', pos: [12.2, 3.4, 9.5] }
    ];

    signs.forEach(s => {
      const tag = this.createPillTagSprite(s.text, s.color);
      tag.position.set(...s.pos);
      this.group.add(tag);
    });
  }

  private buildAuditoriumScreens() {
    // Large crimson LED screen on West wall of Auditorium
    const canvas = document.createElement('canvas');
    canvas.width = 768;
    canvas.height = 384;
    this.auditoriumScreenMat = new THREE.MeshBasicMaterial({ map: new THREE.CanvasTexture(canvas) });
    this.updateAuditoriumBanner('HDTOOL VERSION 72', 'MÔ PHỎNG VĂN PHÒNG WORKER 3D');

    const frame = new THREE.Mesh(
      new THREE.BoxGeometry(0.18, 3.2, 6.8),
      new THREE.MeshLambertMaterial({ color: 0x1E293B })
    );
    frame.position.set(-20.0, 2.2, 9.4);
    this.group.add(frame);

    const screen = new THREE.Mesh(new THREE.PlaneGeometry(6.4, 2.9), this.auditoriumScreenMat);
    screen.position.set(-19.88, 2.2, 9.4);
    screen.rotation.y = Math.PI / 2;
    this.group.add(screen);
  }

  public updateAuditoriumBanner(title: string, subtitle: string) {
    if (!this.auditoriumScreenMat || !this.auditoriumScreenMat.map) return;
    const canvas = this.auditoriumScreenMat.map.image as HTMLCanvasElement;
    const ctx = canvas.getContext('2d')!;

    const grad = ctx.createLinearGradient(0, 0, canvas.width, canvas.height);
    grad.addColorStop(0, '#991B1B');
    grad.addColorStop(1, '#DC2626');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    ctx.strokeStyle = 'rgba(255,255,255,0.35)';
    ctx.lineWidth = 8;
    ctx.strokeRect(18, 18, canvas.width - 36, canvas.height - 36);

    ctx.fillStyle = '#FDE047';
    ctx.font = 'bold 28px "Be Vietnam Pro", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('★ HỘI TRƯỜNG BÀN GIAO HADOMEE ★', canvas.width / 2, 100);

    ctx.fillStyle = '#FFFFFF';
    ctx.font = 'bold 46px "Be Vietnam Pro", sans-serif';
    ctx.fillText(title.slice(0, 28), canvas.width / 2, 195);

    ctx.fillStyle = '#FECACA';
    ctx.font = 'bold 26px "Be Vietnam Pro", sans-serif';
    ctx.fillText(subtitle.slice(0, 44), canvas.width / 2, 275);

    this.auditoriumScreenMat.map.needsUpdate = true;
  }

  private buildCouncilScreen() {
    const canvas = document.createElement('canvas');
    canvas.width = 768;
    canvas.height = 384;
    this.councilScreenMat = new THREE.MeshBasicMaterial({ map: new THREE.CanvasTexture(canvas) });
    this.updateCouncilBoard([]);

    const tvFrame = new THREE.Mesh(
      new THREE.BoxGeometry(5.6, 2.4, 0.14),
      new THREE.MeshLambertMaterial({ color: 0x0F172A })
    );
    tvFrame.position.set(-1.0, 2.0, -2.02);
    this.group.add(tvFrame);

    const tvScreen = new THREE.Mesh(new THREE.PlaneGeometry(5.3, 2.15), this.councilScreenMat);
    tvScreen.position.set(-1.0, 2.0, -1.93);
    this.group.add(tvScreen);
  }

  public updateCouncilBoard(lines: SwarmBoardDisplayLine[]) {
    if (!this.councilScreenMat || !this.councilScreenMat.map) return;
    const canvas = this.councilScreenMat.map.image as HTMLCanvasElement;
    const ctx = canvas.getContext('2d')!;

    ctx.fillStyle = '#0F172A';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    ctx.fillStyle = '#1E293B';
    ctx.fillRect(0, 0, canvas.width, 64);

    ctx.fillStyle = '#38BDF8';
    ctx.font = 'bold 26px "Be Vietnam Pro", sans-serif';
    ctx.textAlign = 'left';
    ctx.fillText('⚡ BẢNG HỘI ĐỒNG SWARM TRỰC TIẾP', 24, 41);

    const recent = lines.slice(-5);
    if (recent.length === 0) {
      ctx.fillStyle = '#94A3B8';
      ctx.font = '22px "Be Vietnam Pro", sans-serif';
      ctx.fillText('Đang chờ tin nhắn điều phối từ các Worker...', 28, 130);
    } else {
      recent.forEach((item, idx) => {
        const y = 112 + idx * 54;
        ctx.fillStyle = '#F59E0B';
        ctx.font = 'bold 20px "Be Vietnam Pro", sans-serif';
        ctx.fillText(`#${item.seq} ${item.agent}:`, 24, y);

        ctx.fillStyle = '#E2E8F0';
        ctx.font = '20px "Be Vietnam Pro", sans-serif';
        const shortTxt = String(item.text || '').replace(/\s+/g, ' ').slice(0, 52);
        ctx.fillText(shortTxt, 210, y);
      });
    }

    this.councilScreenMat.map.needsUpdate = true;
  }

  private buildPipelineBoard() {
    const boardGroup = new THREE.Group();
    boardGroup.position.set(1.0, 2.4, -13.38);
    this.group.add(boardGroup);

    const backing = new THREE.Mesh(
      new THREE.BoxGeometry(15.2, 2.3, 0.14),
      new THREE.MeshLambertMaterial({ color: 0x0F172A })
    );
    boardGroup.add(backing);

    const canvas = document.createElement('canvas');
    canvas.width = 1024;
    canvas.height = 112;
    this.pipelineBannerMat = new THREE.MeshBasicMaterial({ map: new THREE.CanvasTexture(canvas) });
    this.updatePipelineTitle('HADOMEE WORKSPACE · QUY TRÌNH XỬ LÝ TÁC VỤ');

    const banner = new THREE.Mesh(new THREE.PlaneGeometry(14.5, 0.72), this.pipelineBannerMat);
    banner.position.set(0, 0.72, 0.09);
    boardGroup.add(banner);

    const boxW = 1.58;
    const boxH = 0.88;
    const startX = -5.8;
    const stepX = 1.66;

    const subLabels: Record<string, string> = {
      spec: 'Đặc tả',
      design: 'Kiến trúc',
      code: 'Lập trình',
      review: 'Review',
      test: 'Kiểm thử',
      security: 'Bảo mật',
      package: 'Đóng gói',
      docs: 'Tài liệu'
    };

    this.stageOrder.forEach((stage, idx) => {
      const stageGroup = new THREE.Group();
      stageGroup.position.set(startX + idx * stepX, -0.32, 0.08);

      const boxMesh = new THREE.Mesh(
        new THREE.BoxGeometry(boxW, boxH, 0.08),
        new THREE.MeshLambertMaterial({ color: idx === 0 ? 0x2563EB : 0x334155 })
      );
      stageGroup.add(boxMesh);
      this.pipelineBoxes.set(stage, boxMesh);

      const c = document.createElement('canvas');
      c.width = 256;
      c.height = 144;
      const ctx = c.getContext('2d')!;
      ctx.fillStyle = '#FFFFFF';
      ctx.font = 'bold 28px sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText(`${idx + 1}. ${stage.toUpperCase()}`, 128, 52);
      ctx.font = '24px sans-serif';
      ctx.fillStyle = '#FDE047';
      ctx.fillText(subLabels[stage] || stage, 128, 102);

      const labelMesh = new THREE.Mesh(
        new THREE.PlaneGeometry(boxW * 0.9, boxH * 0.74),
        new THREE.MeshBasicMaterial({ map: new THREE.CanvasTexture(c), transparent: true })
      );
      labelMesh.position.z = 0.05;
      stageGroup.add(labelMesh);
      boardGroup.add(stageGroup);
    });
  }

  public updatePipelineTitle(title: string) {
    if (!this.pipelineBannerMat || !this.pipelineBannerMat.map) return;
    const canvas = this.pipelineBannerMat.map.image as HTMLCanvasElement;
    const ctx = canvas.getContext('2d')!;
    ctx.fillStyle = '#0F172A';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#38BDF8';
    ctx.font = 'bold 34px "Be Vietnam Pro", sans-serif';
    ctx.fillText(`★ ${title.slice(0, 48)}`, 28, 58);
    this.pipelineBannerMat.map.needsUpdate = true;
  }

  public setStageStatus(stage: string, status: 'active' | 'pass' | 'fail' | 'idle') {
    const box = this.pipelineBoxes.get(stage);
    if (!box) return;
    const mat = box.material as THREE.MeshLambertMaterial;
    switch (status) {
      case 'active':
        mat.color.setHex(0x2563EB);
        break;
      case 'pass':
        mat.color.setHex(0x10B981);
        break;
      case 'fail':
        mat.color.setHex(0xEF4444);
        break;
      case 'idle':
        mat.color.setHex(0x334155);
        break;
    }
  }

  private buildStreetTrees() {
    const trunkMat = new THREE.MeshLambertMaterial({ color: 0x78350F });
    const leafMat1 = new THREE.MeshLambertMaterial({ color: 0x22C55E });
    const leafMat2 = new THREE.MeshLambertMaterial({ color: 0x16A34A });

    const treePositions: Array<[number, number]> = [];
    for (let x = -24; x <= 24; x += 6) {
      treePositions.push([x, -16.2]);
      treePositions.push([x, 15.8]);
    }
    for (let z = -12; z <= 12; z += 6) {
      treePositions.push([-23.8, z]);
      treePositions.push([23.8, z]);
    }

    treePositions.forEach(([tx, tz], idx) => {
      const tree = new THREE.Group();
      tree.position.set(tx, 0, tz);

      const trunk = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.24, 1.4, 7), trunkMat);
      trunk.position.y = 0.7;
      trunk.castShadow = true;
      tree.add(trunk);

      const canopy = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.15, 0),
        idx % 2 === 0 ? leafMat1 : leafMat2
      );
      canopy.position.y = 2.1;
      canopy.rotation.y = idx * 0.7;
      canopy.castShadow = true;
      tree.add(canopy);

      this.exteriorGroup.add(tree);
    });
  }

  private buildCitySkyline() {
    // Stylized pastel/colorful surrounding buildings (North, West, East)
    const palette = [0xFCA5A5, 0xFCD34D, 0x93C5FD, 0x86EFAC, 0xC4B5FD, 0xCBD5E1, 0xFDBA74];
    const blocks: Array<[number, number, number, number, number, number]> = [];

    // North row behind avenue
    for (let i = 0; i < 12; i++) {
      const x = -34 + i * 6.2;
      const h = 6 + (i % 4) * 3.2;
      blocks.push([x, -26 - (i % 2) * 2, 4.8, h, 4.8, palette[i % palette.length]]);
    }
    // West flank
    for (let i = 0; i < 7; i++) {
      const z = -22 + i * 6.5;
      const h = 5 + ((i + 1) % 3) * 3;
      blocks.push([-32 - (i % 2) * 2, z, 4.6, h, 4.8, palette[(i + 2) % palette.length]]);
    }
    // East flank
    for (let i = 0; i < 7; i++) {
      const z = -22 + i * 6.5;
      const h = 5.5 + ((i + 2) % 3) * 2.8;
      blocks.push([32 + (i % 2) * 2, z, 4.6, h, 4.8, palette[(i + 4) % palette.length]]);
    }

    blocks.forEach(([bx, bz, bw, bh, bd, col]) => {
      const bldg = new THREE.Mesh(
        new THREE.BoxGeometry(bw, bh, bd),
        new THREE.MeshLambertMaterial({ color: col })
      );
      bldg.position.set(bx, bh / 2 - 0.2, bz);
      bldg.receiveShadow = true;
      this.exteriorGroup.add(bldg);
    });
  }
}
