import * as THREE from 'three';

export type TimeOfDay = 'day' | 'sunset' | 'night';

export class OfficeLights {
  public dirLight: THREE.DirectionalLight;
  public ambientLight: THREE.AmbientLight;
  public deskLights: THREE.PointLight[] = [];

  constructor(scene: THREE.Scene) {
    // Ambient light
    this.ambientLight = new THREE.AmbientLight(0xE0F2FE, 0.85);
    scene.add(this.ambientLight);

    // Directional Sun Light
    this.dirLight = new THREE.DirectionalLight(0xFFFBEB, 1.4);
    this.dirLight.position.set(15, 25, 20);
    this.dirLight.castShadow = true;
    this.dirLight.shadow.mapSize.width = 2048;
    this.dirLight.shadow.mapSize.height = 2048;
    this.dirLight.shadow.camera.near = 0.5;
    this.dirLight.shadow.camera.far = 70;
    this.dirLight.shadow.camera.left = -22;
    this.dirLight.shadow.camera.right = 22;
    this.dirLight.shadow.camera.top = 18;
    this.dirLight.shadow.camera.bottom = -18;
    this.dirLight.shadow.bias = -0.0005;
    scene.add(this.dirLight);

    // Warm desk lights at key zones
    const deskPositions: [number, number, number][] = [
      [0, 3, -9],     // Orchestrator
      [7, 3, -6],     // Developer
      [11, 3, 4],     // Reviewer
      [-16, 3, -9],   // Library
      [0, 3, 0]       // Center model table
    ];

    for (const pos of deskPositions) {
      const pl = new THREE.PointLight(0xFDE68A, 0.6, 9);
      pl.position.set(...pos);
      scene.add(pl);
      this.deskLights.push(pl);
    }
  }

  public setTimeOfDay(tod: TimeOfDay, scene: THREE.Scene) {
    switch (tod) {
      case 'day':
        scene.background = new THREE.Color(0x0F2236);
        this.ambientLight.color.setHex(0xE0F2FE);
        this.ambientLight.intensity = 0.85;
        this.dirLight.color.setHex(0xFFFBEB);
        this.dirLight.intensity = 1.4;
        this.deskLights.forEach(l => { l.intensity = 0.5; });
        break;

      case 'sunset':
        scene.background = new THREE.Color(0x2B1A28);
        this.ambientLight.color.setHex(0xFDBA74);
        this.ambientLight.intensity = 0.7;
        this.dirLight.color.setHex(0xFB923C);
        this.dirLight.intensity = 1.1;
        this.deskLights.forEach(l => { l.intensity = 0.9; });
        break;

      case 'night':
        scene.background = new THREE.Color(0x08101C);
        this.ambientLight.color.setHex(0x1E293B);
        this.ambientLight.intensity = 0.45;
        this.dirLight.color.setHex(0x38BDF8);
        this.dirLight.intensity = 0.4;
        this.deskLights.forEach(l => { l.intensity = 1.8; });
        break;
    }
  }
}

