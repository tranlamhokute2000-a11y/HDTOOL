import * as THREE from 'three';

export type TimeOfDay = 'day' | 'sunset' | 'night';

export class OfficeLights {
  public dirLight: THREE.DirectionalLight;
  public hemiLight: THREE.HemisphereLight;
  public ambientLight: THREE.AmbientLight;
  public deskLights: THREE.PointLight[] = [];

  constructor(scene: THREE.Scene) {
    scene.background = new THREE.Color(0xDCE8F5);
    scene.fog = new THREE.Fog(0xDCE8F5, 65, 135);

    this.hemiLight = new THREE.HemisphereLight(0xFFFFFF, 0xCBD5E1, 0.9);
    this.hemiLight.position.set(0, 35, 0);
    scene.add(this.hemiLight);

    this.ambientLight = new THREE.AmbientLight(0xF8FAFC, 0.35);
    scene.add(this.ambientLight);

    // Bright Daylight Sun
    this.dirLight = new THREE.DirectionalLight(0xFFFDF5, 1.35);
    this.dirLight.position.set(24, 34, 24);
    this.dirLight.castShadow = true;
    this.dirLight.shadow.mapSize.width = 2048;
    this.dirLight.shadow.mapSize.height = 2048;
    this.dirLight.shadow.camera.near = 2;
    this.dirLight.shadow.camera.far = 95;
    this.dirLight.shadow.camera.left = -28;
    this.dirLight.shadow.camera.right = 28;
    this.dirLight.shadow.camera.top = 24;
    this.dirLight.shadow.camera.bottom = -24;
    this.dirLight.shadow.bias = -0.0005;
    scene.add(this.dirLight);

    const interiorHighlights: [number, number, number][] = [
      [-15, 3.2, -9],
      [-3, 3.2, -7],
      [12, 3.2, -7],
      [-1, 3.2, 1.5],
      [14, 3.2, 2.2]
    ];

    for (const pos of interiorHighlights) {
      const pl = new THREE.PointLight(0xFEF9C3, 0.25, 10);
      pl.position.set(...pos);
      scene.add(pl);
      this.deskLights.push(pl);
    }
  }

  public setTimeOfDay(tod: TimeOfDay, scene: THREE.Scene) {
    switch (tod) {
      case 'day':
        scene.background = new THREE.Color(0xDCE8F5);
        scene.fog = new THREE.Fog(0xDCE8F5, 65, 135);
        this.hemiLight.intensity = 0.9;
        this.ambientLight.intensity = 0.35;
        this.dirLight.color.setHex(0xFFFDF5);
        this.dirLight.intensity = 1.35;
        this.deskLights.forEach(l => { l.intensity = 0.25; });
        break;

      case 'sunset':
        scene.background = new THREE.Color(0xFAD6B8);
        scene.fog = new THREE.Fog(0xFAD6B8, 65, 135);
        this.hemiLight.intensity = 0.7;
        this.ambientLight.intensity = 0.35;
        this.dirLight.color.setHex(0xFB923C);
        this.dirLight.intensity = 1.15;
        this.deskLights.forEach(l => { l.intensity = 0.65; });
        break;

      case 'night':
        scene.background = new THREE.Color(0x0F172A);
        scene.fog = new THREE.Fog(0x0F172A, 65, 135);
        this.hemiLight.intensity = 0.3;
        this.ambientLight.intensity = 0.25;
        this.dirLight.color.setHex(0x60A5FA);
        this.dirLight.intensity = 0.45;
        this.deskLights.forEach(l => { l.intensity = 1.5; });
        break;
    }
  }
}
