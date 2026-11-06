import * as THREE from 'three';

export class OfficeArchitecture {
  public group: THREE.Group;
  public pipelineBoxes: Map<string, THREE.Mesh> = new Map();
  public stageOrder = ['spec', 'design', 'code', 'review', 'test', 'security', 'package', 'docs'];

  constructor() {
    this.group = new THREE.Group();
    this.buildFloor();
    this.buildWalls();
    this.buildPipelineBoard();
    this.buildCitySkyline();
  }

  private buildFloor() {
    // 1. Concrete Base Floor (38 x 24)
    const floorGeo = new THREE.BoxGeometry(38, 0.4, 24);
    const concreteMat = new THREE.MeshToonMaterial({ color: 0x8DA2B3 });
    const floor = new THREE.Mesh(floorGeo, concreteMat);
    floor.position.y = -0.2;
    floor.receiveShadow = true;
    this.group.add(floor);

    // 2. Ash Wood Parquet Island (34 x 20)
    const woodGeo = new THREE.BoxGeometry(34, 0.04, 20);
    const woodMat = new THREE.MeshToonMaterial({ color: 0xCDAE83 });
    const woodFloor = new THREE.Mesh(woodGeo, woodMat);
    woodFloor.position.y = 0.02;
    woodFloor.receiveShadow = true;
    this.group.add(woodFloor);

    // Subtle BIM Grid Lines
    const gridHelper = new THREE.GridHelper(34, 17, 0x1D5FD1, 0xCBD5E1);
    gridHelper.position.y = 0.05;
    (gridHelper.material as THREE.Material).transparent = true;
    (gridHelper.material as THREE.Material).opacity = 0.22;
    this.group.add(gridHelper);
  }

  private buildWalls() {
    const wallMat = new THREE.MeshToonMaterial({ color: 0x1E293B });
    const borderMat = new THREE.MeshToonMaterial({ color: 0x8DA2B3 });

    // North Wall (Z = -12)
    const northWallGeo = new THREE.BoxGeometry(38, 4.5, 0.6);
    const northWall = new THREE.Mesh(northWallGeo, wallMat);
    northWall.position.set(0, 2.25, -12);
    northWall.receiveShadow = true;
    this.group.add(northWall);

    // South Wall with entrance gap (Z = 12)
    const southWallLeftGeo = new THREE.BoxGeometry(16, 3.5, 0.6);
    const southWallLeft = new THREE.Mesh(southWallLeftGeo, wallMat);
    southWallLeft.position.set(-11, 1.75, 12);
    this.group.add(southWallLeft);

    const southWallRight = new THREE.Mesh(southWallLeftGeo, wallMat);
    southWallRight.position.set(11, 1.75, 12);
    this.group.add(southWallRight);

    // Entrance Glass Door Frame (0, 0, 12)
    const doorFrameGeo = new THREE.BoxGeometry(6, 3.2, 0.2);
    const glassMat = new THREE.MeshBasicMaterial({ color: 0x60A5FA, transparent: true, opacity: 0.25 });
    const doorGlass = new THREE.Mesh(doorFrameGeo, glassMat);
    doorGlass.position.set(0, 1.6, 12);
    this.group.add(doorGlass);

    // West Wall (X = -19)
    const westWallGeo = new THREE.BoxGeometry(0.6, 3.5, 24);
    const westWall = new THREE.Mesh(westWallGeo, wallMat);
    westWall.position.set(-19, 1.75, 0);
    this.group.add(westWall);

    // East Wall with Large Windows (X = 19)
    const eastWallGeo = new THREE.BoxGeometry(0.6, 1.2, 24);
    const eastWallBottom = new THREE.Mesh(eastWallGeo, wallMat);
    eastWallBottom.position.set(19, 0.6, 0);
    this.group.add(eastWallBottom);

    const eastWindowGeo = new THREE.BoxGeometry(0.2, 2.3, 22);
    const eastWindow = new THREE.Mesh(eastWindowGeo, glassMat);
    eastWindow.position.set(19, 2.35, 0);
    this.group.add(eastWindow);
  }

  private buildPipelineBoard() {
    // 3D Wall Pipeline Board on North Wall (16m x 3.6m at Z = -11.65)
    const boardGroup = new THREE.Group();
    boardGroup.position.set(0, 2.6, -11.65);
    this.group.add(boardGroup);

    // Board Backing
    const backingGeo = new THREE.BoxGeometry(16, 2.8, 0.15);
    const backingMat = new THREE.MeshToonMaterial({ color: 0x0F2236 });
    const backing = new THREE.Mesh(backingGeo, backingMat);
    boardGroup.add(backing);

    // Title Banner on Board
    const bannerCanvas = document.createElement('canvas');
    bannerCanvas.width = 1024;
    bannerCanvas.height = 128;
    const bCtx = bannerCanvas.getContext('2d')!;
    bCtx.fillStyle = '#0F2236';
    bCtx.fillRect(0, 0, 1024, 128);
    bCtx.fillStyle = '#FF8A1F';
    bCtx.font = 'bold 36px "Be Vietnam Pro", sans-serif';
    bCtx.fillText('★ HadoMEE REVIT PIPELINE ★  TOOL: ĐÁNH SỐ PHÒNG TỰ ĐỘNG', 40, 50);
    bCtx.fillStyle = '#8DA2B3';
    bCtx.font = '24px "Be Vietnam Pro", sans-serif';
    bCtx.fillText('QUY TRÌNH PHÁT TRIỂN ADD-IN REVIT CHUẨN 8 GIAI ĐOẠN', 40, 95);

    const bannerTex = new THREE.CanvasTexture(bannerCanvas);
    const bannerGeo = new THREE.PlaneGeometry(15, 0.85);
    const bannerMat = new THREE.MeshBasicMaterial({ map: bannerTex });
    const bannerMesh = new THREE.Mesh(bannerGeo, bannerMat);
    bannerMesh.position.set(0, 0.9, 0.09);
    boardGroup.add(bannerMesh);

    // 8 Pipeline Stage Boxes
    const boxW = 1.6;
    const boxH = 1.0;
    const startX = -6.0;
    const stepX = 1.72;

    this.stageOrder.forEach((stage, idx) => {
      const stageGroup = new THREE.Group();
      stageGroup.position.set(startX + idx * stepX, -0.2, 0.08);

      // Box Mesh
      const boxGeo = new THREE.BoxGeometry(boxW, boxH, 0.1);
      const boxMat = new THREE.MeshToonMaterial({
        color: idx === 0 ? 0x1D5FD1 : 0x334155
      });
      const boxMesh = new THREE.Mesh(boxGeo, boxMat);
      stageGroup.add(boxMesh);
      this.pipelineBoxes.set(stage, boxMesh);

      // Label sprite on box
      const canvas = document.createElement('canvas');
      canvas.width = 256;
      canvas.height = 160;
      const ctx = canvas.getContext('2d')!;
      ctx.fillStyle = '#FFFFFF';
      ctx.font = 'bold 32px sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText(`${idx + 1}. ${stage.toUpperCase()}`, 128, 60);

      const subLabels: Record<string, string> = {
        spec: 'Đặc tả',
        design: 'Thiết kế',
        code: 'Lập trình',
        review: 'Thẩm định',
        test: 'Kiểm thử',
        security: 'Bảo mật',
        package: 'Đóng gói',
        docs: 'Tài liệu'
      };
      ctx.font = '26px sans-serif';
      ctx.fillStyle = '#FCD34D';
      ctx.fillText(subLabels[stage] || stage, 128, 110);

      const tex = new THREE.CanvasTexture(canvas);
      const labelGeo = new THREE.PlaneGeometry(boxW * 0.9, boxH * 0.7);
      const labelMat = new THREE.MeshBasicMaterial({ map: tex, transparent: true });
      const labelMesh = new THREE.Mesh(labelGeo, labelMat);
      labelMesh.position.z = 0.06;
      stageGroup.add(labelMesh);

      boardGroup.add(stageGroup);
    });
  }

  public setStageStatus(stage: string, status: 'active' | 'pass' | 'fail' | 'idle') {
    const box = this.pipelineBoxes.get(stage);
    if (!box) return;
    const mat = box.material as THREE.MeshToonMaterial;
    switch (status) {
      case 'active':
        mat.color.setHex(0x1D5FD1); // Blue
        break;
      case 'pass':
        mat.color.setHex(0x2DBE70); // Green
        break;
      case 'fail':
        mat.color.setHex(0xE5484D); // Red
        break;
      case 'idle':
        mat.color.setHex(0x334155);
        break;
    }
  }

  private buildCitySkyline() {
    // Stylized low-poly buildings outside East window
    const buildingMat = new THREE.MeshToonMaterial({ color: 0x475569 });
    const winMat = new THREE.MeshBasicMaterial({ color: 0xFDE047, transparent: true, opacity: 0.7 });

    for (let i = 0; i < 9; i++) {
      const h = 8 + (i % 4) * 4;
      const bGeo = new THREE.BoxGeometry(4, h, 4);
      const bMesh = new THREE.Mesh(bGeo, buildingMat);
      bMesh.position.set(28, h / 2 - 2, -14 + i * 4);
      this.group.add(bMesh);

      // Light windows
      const wGeo = new THREE.BoxGeometry(4.1, 0.4, 0.8);
      for (let j = 0; j < 3; j++) {
        const wMesh = new THREE.Mesh(wGeo, winMat);
        wMesh.position.set(0, h * 0.3 + j * 2 - h / 2, 0);
        bMesh.add(wMesh);
      }
    }
  }
}

