import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { ZoneConfig, ZONES } from '../agents/roles.js';

export class CameraManager {
  public camera: THREE.PerspectiveCamera;
  public controls: OrbitControls;
  public isDirectorMode = false;
  public isWalkMode = false;

  private userInteractedTimer = 0;
  private isGlideActive = false;
  private targetPosition = new THREE.Vector3(0, 26, 30);
  private targetLookAt = new THREE.Vector3(0, 0, 1);

  private keysPressed: Set<string> = new Set();

  constructor(camera: THREE.PerspectiveCamera, domElement: HTMLElement) {
    this.camera = camera;
    this.camera.position.set(0, 26, 30);

    this.controls = new OrbitControls(this.camera, domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.maxPolarAngle = Math.PI / 2 - 0.04;
    this.controls.minDistance = 2;
    this.controls.maxDistance = 68;
    this.controls.target.set(0, 0, 1);

    this.controls.addEventListener('start', () => {
      this.userInteractedTimer = 20;
      this.isGlideActive = false;
    });

    window.addEventListener('keydown', (e) => {
      this.keysPressed.add(e.key.toLowerCase());
      if (!this.isWalkMode && ['1', '2', '3', '4', '5', '6', '7', '8'].includes(e.key)) {
        const idx = parseInt(e.key) - 1;
        if (ZONES[idx]) this.focusZone(ZONES[idx]);
      }
    });

    window.addEventListener('keyup', (e) => {
      this.keysPressed.delete(e.key.toLowerCase());
    });
  }

  public focusZone(zone: ZoneConfig) {
    if (this.isWalkMode) this.setWalkMode(false);
    this.targetPosition.set(...zone.cameraPos);
    this.targetLookAt.set(...zone.lookAt);
    this.isGlideActive = true;
    this.userInteractedTimer = 15;
  }

  public resetView() {
    this.focusZone(ZONES[0]);
  }

  public zoomIn() {
    this.isGlideActive = false;
    const dir = new THREE.Vector3().subVectors(this.controls.target, this.camera.position);
    const len = dir.length();
    if (len > 4) {
      this.camera.position.addScaledVector(dir.normalize(), Math.min(4.5, len * 0.25));
      this.controls.update();
    }
  }

  public zoomOut() {
    this.isGlideActive = false;
    const dir = new THREE.Vector3().subVectors(this.controls.target, this.camera.position);
    const len = dir.length();
    if (len < 62) {
      this.camera.position.addScaledVector(dir.normalize(), -Math.min(5.0, len * 0.25));
      this.controls.update();
    }
  }

  public setWalkMode(enabled: boolean) {
    this.isWalkMode = enabled;
    this.isGlideActive = false;
    if (enabled) {
      this.camera.position.set(11.0, 1.75, 11.5);
      this.controls.target.set(8.0, 1.55, 6.0);
      this.controls.maxPolarAngle = Math.PI * 0.85;
      this.controls.minDistance = 0.5;
      this.controls.maxDistance = 8;
    } else {
      this.controls.maxPolarAngle = Math.PI / 2 - 0.04;
      this.controls.minDistance = 2;
      this.controls.maxDistance = 68;
      this.focusZone(ZONES[0]);
    }
  }

  public flyTo(pos: THREE.Vector3, offset: THREE.Vector3 = new THREE.Vector3(0, 5.5, 7.5)) {
    if (this.isWalkMode) this.setWalkMode(false);
    this.userInteractedTimer = 15;
    this.targetPosition.copy(pos).add(offset);
    this.targetLookAt.copy(pos).add(new THREE.Vector3(0, 1.0, 0));
    this.isGlideActive = true;
  }

  public trackAgent(agentPos: THREE.Vector3) {
    if (!this.isDirectorMode || this.userInteractedTimer > 0 || this.isWalkMode) return;
    this.targetPosition.set(agentPos.x, agentPos.y + 8, agentPos.z + 11);
    this.targetLookAt.copy(agentPos).add(new THREE.Vector3(0, 0.8, 0));
    this.isGlideActive = true;
  }

  public update(delta: number) {
    if (this.userInteractedTimer > 0) {
      this.userInteractedTimer -= delta;
    }

    if (this.isWalkMode) {
      const forward = new THREE.Vector3();
      this.camera.getWorldDirection(forward);
      forward.y = 0;
      if (forward.lengthSq() > 0) forward.normalize();

      const right = new THREE.Vector3().crossVectors(forward, new THREE.Vector3(0, 1, 0)).normalize();
      const speed = (this.keysPressed.has('shift') ? 8.5 : 5.0) * delta;
      const move = new THREE.Vector3();

      if (this.keysPressed.has('w') || this.keysPressed.has('arrowup')) move.addScaledVector(forward, speed);
      if (this.keysPressed.has('s') || this.keysPressed.has('arrowdown')) move.addScaledVector(forward, -speed);
      if (this.keysPressed.has('a') || this.keysPressed.has('arrowleft')) move.addScaledVector(right, -speed);
      if (this.keysPressed.has('d') || this.keysPressed.has('arrowright')) move.addScaledVector(right, speed);

      if (move.lengthSq() > 0) {
        const nx = THREE.MathUtils.clamp(this.camera.position.x + move.x, -19.2, 19.2);
        const nz = THREE.MathUtils.clamp(this.camera.position.z + move.z, -12.5, 12.5);
        const dx = nx - this.camera.position.x;
        const dz = nz - this.camera.position.z;
        this.camera.position.x = nx;
        this.camera.position.z = nz;
        this.controls.target.x += dx;
        this.controls.target.z += dz;
      }
      this.camera.position.y = 1.75;
      this.controls.update();
      return;
    }

    if (this.isGlideActive) {
      this.camera.position.lerp(this.targetPosition, 0.08);
      this.controls.target.lerp(this.targetLookAt, 0.08);
      if (
        this.camera.position.distanceTo(this.targetPosition) < 0.08 &&
        this.controls.target.distanceTo(this.targetLookAt) < 0.08
      ) {
        this.isGlideActive = false;
      }
    }
    this.controls.update();
  }
}
