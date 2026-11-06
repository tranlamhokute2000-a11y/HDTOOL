import * as THREE from 'three';

export class OfficeProps {
  public group: THREE.Group;
  public testLeds: THREE.Mesh[] = [];

  constructor() {
    this.group = new THREE.Group();
    this.buildDesks();
    this.buildWhiteboard();
    this.buildLibrary();
    this.buildConveyor();
    this.buildTestBench();
    this.buildPantry();
  }

  private createStandardDesk(x: number, z: number, rotY: number, hasDualScreens = false): THREE.Group {
    const deskGroup = new THREE.Group();
    deskGroup.position.set(x, 0, z);
    deskGroup.rotation.y = rotY;

    const deskMat = new THREE.MeshToonMaterial({ color: 0xCDAE83 }); // Ash wood
    const legMat = new THREE.MeshToonMaterial({ color: 0x334155 });
    const screenMat = new THREE.MeshBasicMaterial({ color: 0x1E293B });
    const keyboardMat = new THREE.MeshToonMaterial({ color: 0x475569 });

    // Tabletop (1.6m x 0.8m x 0.05m at Y = 0.72)
    const topGeo = new THREE.BoxGeometry(1.6, 0.06, 0.8);
    const top = new THREE.Mesh(topGeo, deskMat);
    top.position.y = 0.72;
    top.castShadow = true;
    top.receiveShadow = true;
    deskGroup.add(top);

    // Legs
    const legGeo = new THREE.CylinderGeometry(0.03, 0.03, 0.72, 8);
    const offsets = [
      [-0.7, -0.3], [0.7, -0.3],
      [-0.7, 0.3], [0.7, 0.3]
    ];
    offsets.forEach(([ox, oz]) => {
      const leg = new THREE.Mesh(legGeo, legMat);
      leg.position.set(ox, 0.36, oz);
      leg.castShadow = true;
      deskGroup.add(leg);
    });

    // Monitor 1
    const screenGeo = new THREE.BoxGeometry(0.55, 0.35, 0.03);
    const screen = new THREE.Mesh(screenGeo, screenMat);
    screen.position.set(hasDualScreens ? -0.3 : 0, 1.0, -0.2);
    deskGroup.add(screen);

    // Monitor 2 (if dual)
    if (hasDualScreens) {
      const screen2 = new THREE.Mesh(screenGeo, screenMat);
      screen2.position.set(0.3, 1.0, -0.2);
      screen2.rotation.y = -0.15;
      deskGroup.add(screen2);
    }

    // Keyboard
    const kbGeo = new THREE.BoxGeometry(0.4, 0.02, 0.15);
    const kb = new THREE.Mesh(kbGeo, keyboardMat);
    kb.position.set(0, 0.76, 0.1);
    deskGroup.add(kb);

    // Office Chair
    const chairGroup = new THREE.Group();
    chairGroup.position.set(0, 0, 0.5);
    const seatGeo = new THREE.BoxGeometry(0.45, 0.08, 0.45);
    const chairMat = new THREE.MeshToonMaterial({ color: 0x1E293B });
    const seat = new THREE.Mesh(seatGeo, chairMat);
    seat.position.y = 0.45;
    chairGroup.add(seat);
    const backGeo = new THREE.BoxGeometry(0.45, 0.45, 0.06);
    const back = new THREE.Mesh(backGeo, chairMat);
    back.position.set(0, 0.72, 0.2);
    chairGroup.add(back);
    deskGroup.add(chairGroup);

    return deskGroup;
  }

  private buildDesks() {
    // 1. Orchestrator L-Shaped Desk (0, 0, -9.5)
    const orcDesk = this.createStandardDesk(0, -9.5, 0, true);
    // Add L-side table
    const sideTop = new THREE.Mesh(
      new THREE.BoxGeometry(0.8, 0.06, 1.2),
      new THREE.MeshToonMaterial({ color: 0xCDAE83 })
    );
    sideTop.position.set(1.1, 0.72, 0.2);
    orcDesk.add(sideTop);
    this.group.add(orcDesk);

    // 2. Architect Drafting Desk (-7, 0, -6)
    const archDesk = this.createStandardDesk(-7, -6, Math.PI / 6);
    // Tilt tabletop
    archDesk.children[0].rotation.x = -0.2;
    this.group.add(archDesk);

    // 3. API Developer Dual-Screen Station (7, 0, -6)
    const devDesk = this.createStandardDesk(7, -6, -Math.PI / 6, true);
    // Add coffee cup
    const cup = new THREE.Mesh(
      new THREE.CylinderGeometry(0.04, 0.035, 0.08, 8),
      new THREE.MeshToonMaterial({ color: 0xFFFFFF })
    );
    cup.position.set(0.5, 0.78, 0.1);
    devDesk.add(cup);
    this.group.add(devDesk);

    // 4. UI Desk (13, 0, -3)
    this.group.add(this.createStandardDesk(13, -3, -Math.PI / 4));

    // 5. Reviewer Desk (11, 0, 4)
    this.group.add(this.createStandardDesk(11, 4, -Math.PI * 0.75));

    // 6. Security Station Desk (-4.5, 0, 8.5)
    this.group.add(this.createStandardDesk(-4.5, 8.5, Math.PI));

    // 7. Debugger Desk (1.5, 0, 8.5)
    this.group.add(this.createStandardDesk(1.5, 8.5, Math.PI));

    // 8. Docs Writer Desk (-15, 0, 1)
    this.group.add(this.createStandardDesk(-15, 1, Math.PI / 2));

    // 9. Project Owner (Sếp Hồ) Reception Desk (0, 0, 10.5)
    const ownerDesk = this.createStandardDesk(0, 10.5, Math.PI);
    // Gold badge on desk
    const badgeGeo = new THREE.BoxGeometry(0.4, 0.05, 0.08);
    const badgeMat = new THREE.MeshToonMaterial({ color: 0xF59E0B });
    const badge = new THREE.Mesh(badgeGeo, badgeMat);
    badge.position.set(0, 0.77, 0.2);
    ownerDesk.add(badge);
    this.group.add(ownerDesk);
  }

  private buildWhiteboard() {
    // Requirements Whiteboard (-15, 1.8, -3)
    const wbGroup = new THREE.Group();
    wbGroup.position.set(-15, 1.8, -3);
    wbGroup.rotation.y = Math.PI / 2;

    const boardGeo = new THREE.BoxGeometry(3.2, 1.8, 0.08);
    const boardMat = new THREE.MeshBasicMaterial({ color: 0xF8FAFC });
    const board = new THREE.Mesh(boardGeo, boardMat);
    wbGroup.add(board);

    // Colorful sticky notes
    const colors = [0xF87171, 0xFBBF24, 0x34D399, 0x60A5FA, 0xA78BFA];
    for (let i = 0; i < 12; i++) {
      const noteGeo = new THREE.BoxGeometry(0.2, 0.2, 0.01);
      const noteMat = new THREE.MeshBasicMaterial({ color: colors[i % colors.length] });
      const note = new THREE.Mesh(noteGeo, noteMat);
      note.position.set(-1.2 + (i % 5) * 0.5, 0.5 - Math.floor(i / 5) * 0.45, 0.05);
      wbGroup.add(note);
    }
    this.group.add(wbGroup);
  }

  private buildLibrary() {
    // Bookshelves at North-West (-17, 0, -9)
    const libGroup = new THREE.Group();
    libGroup.position.set(-17, 0, -9);

    const shelfMat = new THREE.MeshToonMaterial({ color: 0x451A03 });
    const shelfGeo = new THREE.BoxGeometry(3.5, 3.8, 0.6);
    const shelf = new THREE.Mesh(shelfGeo, shelfMat);
    shelf.position.y = 1.9;
    libGroup.add(shelf);

    // Books rows
    const bookColors = [0xDC2626, 0x2563EB, 0x16A34A, 0xD97706, 0x7C3AED];
    for (let row = 0; row < 4; row++) {
      for (let b = 0; b < 10; b++) {
        const bookGeo = new THREE.BoxGeometry(0.06, 0.45, 0.4);
        const bookMat = new THREE.MeshToonMaterial({ color: bookColors[(row * 10 + b) % bookColors.length] });
        const book = new THREE.Mesh(bookGeo, bookMat);
        book.position.set(-1.4 + b * 0.3, 0.6 + row * 0.8, 0.1);
        libGroup.add(book);
      }
    }
    this.group.add(libGroup);
  }

  private buildConveyor() {
    // Packaging Conveyor Belt (-10, 0, 5)
    const convGroup = new THREE.Group();
    convGroup.position.set(-10, 0, 5);
    convGroup.rotation.y = Math.PI * 0.75;

    const frameGeo = new THREE.BoxGeometry(4.5, 0.6, 0.7);
    const frameMat = new THREE.MeshToonMaterial({ color: 0x475569 });
    const frame = new THREE.Mesh(frameGeo, frameMat);
    frame.position.y = 0.3;
    convGroup.add(frame);

    // Belt
    const beltGeo = new THREE.BoxGeometry(4.3, 0.05, 0.6);
    const beltMat = new THREE.MeshBasicMaterial({ color: 0x1E293B });
    const belt = new THREE.Mesh(beltGeo, beltMat);
    belt.position.y = 0.63;
    convGroup.add(belt);

    // Cardboard boxes on belt
    const boxGeo = new THREE.BoxGeometry(0.4, 0.35, 0.4);
    const boxMat = new THREE.MeshToonMaterial({ color: 0xD97706 });
    for (let i = 0; i < 3; i++) {
      const box = new THREE.Mesh(boxGeo, boxMat);
      box.position.set(-1.2 + i * 1.2, 0.83, 0);
      convGroup.add(box);
    }
    this.group.add(convGroup);
  }

  private buildTestBench() {
    // Tester Bench with 10 LED Lights (7, 0, 8)
    const benchGroup = new THREE.Group();
    benchGroup.position.set(7, 0, 8);

    const benchGeo = new THREE.BoxGeometry(2.0, 0.75, 0.8);
    const benchMat = new THREE.MeshToonMaterial({ color: 0x334155 });
    const bench = new THREE.Mesh(benchGeo, benchMat);
    bench.position.y = 0.375;
    benchGroup.add(bench);

    // 10 LEDs array
    const ledGeo = new THREE.SphereGeometry(0.045, 8, 8);
    for (let i = 0; i < 10; i++) {
      const ledMat = new THREE.MeshBasicMaterial({ color: 0x10B981 });
      const led = new THREE.Mesh(ledGeo, ledMat);
      led.position.set(-0.7 + i * 0.155, 0.78, 0.15);
      benchGroup.add(led);
      this.testLeds.push(led);
    }
    this.group.add(benchGroup);
  }

  public updateTestLeds(passed: number, failed: number) {
    this.testLeds.forEach((led, idx) => {
      const mat = led.material as THREE.MeshBasicMaterial;
      if (idx < passed) {
        mat.color.setHex(0x10B981); // Green
      } else if (idx < passed + failed) {
        mat.color.setHex(0xEF4444); // Red
      } else {
        mat.color.setHex(0x475569); // Off
      }
    });
  }

  private buildPantry() {
    // Pantry Area (16, 0, 9.5)
    const pantryGroup = new THREE.Group();
    pantryGroup.position.set(16, 0, 9.5);

    // Coffee Counter
    const counterGeo = new THREE.BoxGeometry(2.4, 0.85, 0.8);
    const counterMat = new THREE.MeshToonMaterial({ color: 0x1E293B });
    const counter = new THREE.Mesh(counterGeo, counterMat);
    counter.position.y = 0.425;
    pantryGroup.add(counter);

    // Coffee Maker
    const coffeeGeo = new THREE.BoxGeometry(0.35, 0.45, 0.35);
    const coffeeMat = new THREE.MeshToonMaterial({ color: 0xEF4444 });
    const coffee = new THREE.Mesh(coffeeGeo, coffeeMat);
    coffee.position.set(-0.5, 1.05, 0);
    pantryGroup.add(coffee);

    // Sofa
    const sofaGeo = new THREE.BoxGeometry(1.8, 0.5, 0.7);
    const sofaMat = new THREE.MeshToonMaterial({ color: 0x1D5FD1 });
    const sofa = new THREE.Mesh(sofaGeo, sofaMat);
    sofa.position.set(0, 0.25, -1.4);
    pantryGroup.add(sofa);

    this.group.add(pantryGroup);
  }
}

