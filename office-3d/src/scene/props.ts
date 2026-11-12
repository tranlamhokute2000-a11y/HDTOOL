import * as THREE from 'three';

export class OfficeProps {
  public group: THREE.Group;
  public testLeds: THREE.Mesh[] = [];
  private deskScreens: Map<string, THREE.MeshBasicMaterial[]> = new Map();

  constructor() {
    this.group = new THREE.Group();
    this.buildWorkstations();
    this.buildCouncilTable();
    this.buildTestServerRack();
    this.buildAuditoriumSeats();
    this.buildReceptionCounter();
    this.buildIndoorPlants();
    this.buildBookshelves();
  }

  private createWorkstation(
    id: string,
    x: number,
    z: number,
    rotY: number,
    hasDual = true,
    deskColor = 0xF8FAFC
  ): THREE.Group {
    const deskGroup = new THREE.Group();
    deskGroup.position.set(x, 0, z);
    deskGroup.rotation.y = rotY;

    const deskMat = new THREE.MeshLambertMaterial({ color: deskColor });
    const legMat = new THREE.MeshLambertMaterial({ color: 0x334155 });
    const bezelMat = new THREE.MeshLambertMaterial({ color: 0x0F172A });
    const screenMats: THREE.MeshBasicMaterial[] = [];

    // Tabletop in front of worker (worker stands/sits at local Z = 0, faces -Z or +Z?
    // Wait: AgentRig faces +Z at rotY = 0! Let's place desk slightly in front of worker OR put chair at z=0.45 and desk at z=-0.45 so if rotY=0 faces +Z, wait: in old code, desk was placed AT role.deskPos with rotY = role.deskRot! Wait: let's put desk cleanly at Z=+0.55 relative to worker facing +Z, OR keep desk AT (x, z) and offset desk table relative to worker cleanly!)
    // Let's make worker stand AT (x, z) facing (sin(rotY), cos(rotY)):
    // Putting tabletop at local (0, 0.72, +0.62) puts monitor in front of agent facing them!
    const top = new THREE.Mesh(new THREE.BoxGeometry(1.55, 0.06, 0.76), deskMat);
    top.position.set(0, 0.72, 0.62);
    top.castShadow = true;
    top.receiveShadow = true;
    deskGroup.add(top);

    const legGeo = new THREE.CylinderGeometry(0.028, 0.028, 0.72, 6);
    [[-0.68, 0.3], [0.68, 0.3], [-0.68, 0.94], [0.68, 0.94]].forEach(([lx, lz]) => {
      const leg = new THREE.Mesh(legGeo, legMat);
      leg.position.set(lx, 0.36, lz);
      deskGroup.add(leg);
    });

    // Monitors facing worker (-Z face towards worker at z=0)
    const addMonitor = (mx: number, ry: number) => {
      const bezel = new THREE.Mesh(new THREE.BoxGeometry(0.56, 0.34, 0.03), bezelMat);
      bezel.position.set(mx, 0.98, 0.8);
      bezel.rotation.y = ry;
      deskGroup.add(bezel);

      const scrMat = new THREE.MeshBasicMaterial({ color: 0x1E293B });
      const scr = new THREE.Mesh(new THREE.PlaneGeometry(0.5, 0.28), scrMat);
      scr.position.set(mx, 0.98, 0.783);
      scr.rotation.y = ry + Math.PI;
      deskGroup.add(scr);
      screenMats.push(scrMat);

      const stand = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.24, 0.05), legMat);
      stand.position.set(mx, 0.84, 0.81);
      deskGroup.add(stand);
    };

    if (hasDual) {
      addMonitor(-0.3, 0.14);
      addMonitor(0.3, -0.14);
    } else {
      addMonitor(0, 0);
    }

    // Keyboard
    const kb = new THREE.Mesh(
      new THREE.BoxGeometry(0.42, 0.02, 0.14),
      new THREE.MeshLambertMaterial({ color: 0x334155 })
    );
    kb.position.set(0, 0.76, 0.45);
    deskGroup.add(kb);

    // Ergonomic Chair behind agent (local z = -0.35)
    const chairMat = new THREE.MeshLambertMaterial({ color: 0x1E293B });
    const seat = new THREE.Mesh(new THREE.BoxGeometry(0.46, 0.07, 0.44), chairMat);
    seat.position.set(0, 0.43, -0.24);
    deskGroup.add(seat);
    const back = new THREE.Mesh(new THREE.BoxGeometry(0.44, 0.48, 0.06), chairMat);
    back.position.set(0, 0.7, -0.45);
    deskGroup.add(back);

    this.deskScreens.set(id, screenMats);
    this.group.add(deskGroup);
    return deskGroup;
  }

  public setDeskActive(workerId: string, status: 'WORK' | 'WAIT' | 'FAIL' | 'IDLE' | 'KILLED') {
    const mats = this.deskScreens.get(workerId);
    if (!mats) return;
    mats.forEach(mat => {
      if (status === 'WORK') mat.color.setHex(0x38BDF8);
      else if (status === 'WAIT') mat.color.setHex(0xF59E0B);
      else if (status === 'FAIL') mat.color.setHex(0xEF4444);
      else mat.color.setHex(0x1E293B);
    });
  }

  private buildWorkstations() {
    // Swarm & Executive desks
    this.createWorkstation('coordinator', -15.2, -9.2, 0, true, 0x92400E);
    this.createWorkstation('orchestrator', -12.0, -6.5, -Math.PI / 4, true, 0xA16207);

    // Architecture & Geometry pods
    this.createWorkstation('geometry', -6.5, -9.2, 0, true, 0xFFFFFF);
    this.createWorkstation('architect', -1.5, -9.2, 0, true, 0xFFFFFF);
    this.createWorkstation('requirements', -6.5, -5.2, Math.PI, false, 0xFFFFFF);
    this.createWorkstation('researcher', -1.5, -5.2, Math.PI, true, 0xFFFFFF);

    // Engineering & UI pods
    this.createWorkstation('ui', 9.2, -9.2, 0, true, 0xFFFFFF);
    this.createWorkstation('api-developer', 14.5, -9.2, 0, true, 0xFFFFFF);
    // Extra engineering pods for spare dynamic workers
    this.createWorkstation('spare-eng-1', 9.2, -5.2, Math.PI, true, 0xFFFFFF);
    this.createWorkstation('spare-eng-2', 14.5, -5.2, Math.PI, true, 0xFFFFFF);

    // Verification & Build pods
    this.createWorkstation('verifier', 12.0, 0.8, Math.PI, true, 0xF8FAFC);
    this.createWorkstation('reviewer', 16.5, 0.8, Math.PI, false, 0xF8FAFC);
    this.createWorkstation('tester', 12.0, 3.8, 0, true, 0xF8FAFC);
    this.createWorkstation('debugger', 16.5, 3.8, 0, false, 0xF8FAFC);

    // Auditorium delivery desks
    this.createWorkstation('packaging', -13.5, 8.8, Math.PI / 6, false, 0xD97706);
    this.createWorkstation('docs-writer', -5.5, 8.8, -Math.PI / 6, false, 0xD97706);
  }

  private buildCouncilTable() {
    // Glass conference table in #HỘI ĐỒNG SWARM (-1, 0, 1.4)
    const tableGroup = new THREE.Group();
    tableGroup.position.set(-1.0, 0, 1.4);

    const top = new THREE.Mesh(
      new THREE.BoxGeometry(5.2, 0.08, 2.2),
      new THREE.MeshLambertMaterial({ color: 0x1E293B })
    );
    top.position.y = 0.74;
    top.castShadow = true;
    top.receiveShadow = true;
    tableGroup.add(top);

    // Center glowing tablet hub
    const hub = new THREE.Mesh(
      new THREE.BoxGeometry(1.4, 0.04, 0.7),
      new THREE.MeshBasicMaterial({ color: 0x38BDF8 })
    );
    hub.position.y = 0.8;
    tableGroup.add(hub);

    const legMat = new THREE.MeshLambertMaterial({ color: 0x64748B });
    [[-2.0, 0], [2.0, 0]].forEach(([lx, lz]) => {
      const leg = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.16, 0.72, 10), legMat);
      leg.position.set(lx, 0.36, lz);
      tableGroup.add(leg);
    });

    // Chairs around table
    const chairMat = new THREE.MeshLambertMaterial({ color: 0x0F172A });
    [-1.7, 0, 1.7].forEach(cx => {
      [-1.35, 1.35].forEach(cz => {
        const ch = new THREE.Mesh(new THREE.BoxGeometry(0.42, 0.48, 0.42), chairMat);
        ch.position.set(cx, 0.24, cz);
        tableGroup.add(ch);
      });
    });

    this.group.add(tableGroup);
  }

  private buildTestServerRack() {
    // Build & QA Server Rack in Verification room
    const rack = new THREE.Group();
    rack.position.set(19.2, 0, 2.2);

    const body = new THREE.Mesh(
      new THREE.BoxGeometry(0.85, 2.1, 1.2),
      new THREE.MeshLambertMaterial({ color: 0x1E293B })
    );
    body.position.y = 1.05;
    body.castShadow = true;
    rack.add(body);

    const ledGeo = new THREE.SphereGeometry(0.045, 6, 6);
    for (let i = 0; i < 8; i++) {
      const ledMat = new THREE.MeshBasicMaterial({ color: 0x10B981 });
      const led = new THREE.Mesh(ledGeo, ledMat);
      led.position.set(-0.44, 0.4 + i * 0.2, (i % 2 === 0 ? -0.25 : 0.25));
      rack.add(led);
      this.testLeds.push(led);
    }

    this.group.add(rack);
  }

  public updateTestLeds(passed: number, failed: number) {
    this.testLeds.forEach((led, idx) => {
      const mat = led.material as THREE.MeshBasicMaterial;
      if (idx < passed) mat.color.setHex(0x10B981);
      else if (idx < passed + failed) mat.color.setHex(0xEF4444);
      else mat.color.setHex(0x38BDF8);
    });
  }

  private buildAuditoriumSeats() {
    // Tiered red seats facing West LED Screen in Hội trường ([-17 .. 2], Z=[6.5 .. 12.5])
    const seatMat = new THREE.MeshLambertMaterial({ color: 0x991B1B });
    const backMat = new THREE.MeshLambertMaterial({ color: 0xB91C1C });

    for (let col = 0; col < 5; col++) {
      for (let row = 0; row < 4; row++) {
        const sx = -11.5 + col * 3.1;
        const sz = 6.8 + row * 1.45;
        if (sx < -13 && sz < 9.8) continue; // Keep space near packaging desk
        const seatGroup = new THREE.Group();
        seatGroup.position.set(sx, 0, sz);

        const base = new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.36, 0.52), seatMat);
        base.position.y = 0.18;
        seatGroup.add(base);

        const back = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.45, 0.52), backMat);
        back.position.set(0.24, 0.46, 0);
        seatGroup.add(back);

        this.group.add(seatGroup);
      }
    }
  }

  private buildReceptionCounter() {
    // Iconic White + Cyan Reception Counter in #LỄ TÂN (12.2, 0, 8.8)
    const rec = new THREE.Group();
    rec.position.set(12.2, 0, 8.6);

    const counterBody = new THREE.Mesh(
      new THREE.BoxGeometry(4.2, 0.95, 0.85),
      new THREE.MeshLambertMaterial({ color: 0x38BDF8 })
    );
    counterBody.position.y = 0.475;
    counterBody.castShadow = true;
    rec.add(counterBody);

    const counterTop = new THREE.Mesh(
      new THREE.BoxGeometry(4.35, 0.08, 0.95),
      new THREE.MeshLambertMaterial({ color: 0xFFFFFF })
    );
    counterTop.position.y = 0.98;
    rec.add(counterTop);

    // Guest Lounge Sofa against East lobby wall
    const sofa = new THREE.Mesh(
      new THREE.BoxGeometry(0.9, 0.55, 2.6),
      new THREE.MeshLambertMaterial({ color: 0x451A03 })
    );
    sofa.position.set(7.0, 0.28, 1.5);
    sofa.castShadow = true;
    rec.add(sofa);

    this.group.add(rec);
  }

  private buildIndoorPlants() {
    const potMat = new THREE.MeshLambertMaterial({ color: 0x475569 });
    const leafMat = new THREE.MeshLambertMaterial({ color: 0x16A34A });

    const spots: Array<[number, number]> = [
      [-19.2, -12.6], [-10.8, -12.6], [3.2, -12.6],
      [19.2, -12.6], [5.2, 12.6], [19.2, 12.6],
      [-9.2, 4.6], [7.2, 4.6]
    ];

    spots.forEach(([px, pz]) => {
      const pot = new THREE.Group();
      pot.position.set(px, 0, pz);
      const vase = new THREE.Mesh(new THREE.CylinderGeometry(0.26, 0.2, 0.48, 8), potMat);
      vase.position.y = 0.24;
      pot.add(vase);

      const bush = new THREE.Mesh(new THREE.DodecahedronGeometry(0.42, 0), leafMat);
      bush.position.y = 0.72;
      bush.castShadow = true;
      pot.add(bush);
      this.group.add(pot);
    });
  }

  private buildBookshelves() {
    // Bookshelf / File cabinet in Điều phối room
    const shelf = new THREE.Mesh(
      new THREE.BoxGeometry(0.65, 2.2, 3.2),
      new THREE.MeshLambertMaterial({ color: 0x78350F })
    );
    shelf.position.set(-19.6, 1.1, -8.5);
    shelf.castShadow = true;
    this.group.add(shelf);
  }
}
