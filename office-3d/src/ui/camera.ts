import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

export class CameraManager {
  public camera: THREE.PerspectiveCamera;
  public controls: OrbitControls;
  public isDirectorMode = true;

  private userInteractedTimer = 0;
  private targetPosition = new THREE.Vector3(0, 24, 25);
  private targetLookAt = new THREE.Vector3(0, 0, 0);

  constructor(camera: THREE.PerspectiveCamera, domElement: HTMLElement) {
    this.camera = camera;
    this.camera.position.set(0, 24, 25);

    this.controls = new OrbitControls(this.camera, domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.06;
    this.controls.maxPolarAngle = Math.PI / 2 - 0.05; // Don't go below floor
    this.controls.minDistance = 4;
    this.controls.maxDistance = 60;
    this.controls.target.set(0, 0, 0);

    // Pause Director mode on user manual interaction
    this.controls.addEventListener('start', () => {
      this.userInteractedTimer = 30; // Pause director mode for 30s
    });

    // Keyboard shortcuts 1 - 5
    window.addEventListener('keydown', (e) => {
      if (['1', '2', '3', '4', '5'].includes(e.key)) {
        this.setPreset(parseInt(e.key));
      }
    });
  }

  public setPreset(index: number) {
    this.userInteractedTimer = 15;
    switch (index) {
      case 1: // Overview
        this.targetPosition.set(0, 24, 25);
        this.targetLookAt.set(0, 0, 0);
        break;
      case 2: // Top-down
        this.targetPosition.set(0, 36, 0.1);
        this.targetLookAt.set(0, 0, 0);
        break;
      case 3: // Design & Requirements
        this.targetPosition.set(-14, 11, 4);
        this.targetLookAt.set(-11, 1, -5);
        break;
      case 4: // Coding & UI
        this.targetPosition.set(12, 11, 4);
        this.targetLookAt.set(9, 1, -5);
        break;
      case 5: // Testing & Verification
        this.targetPosition.set(6, 10, 15);
        this.targetLookAt.set(5, 1, 7);
        break;
    }
  }

  public flyTo(pos: THREE.Vector3, offset: THREE.Vector3 = new THREE.Vector3(0, 4, 6)) {
    this.userInteractedTimer = 12;
    this.targetPosition.copy(pos).add(offset);
    this.targetLookAt.copy(pos);
  }

  public trackAgent(agentPos: THREE.Vector3) {
    if (!this.isDirectorMode || this.userInteractedTimer > 0) return;
    this.targetPosition.set(agentPos.x, agentPos.y + 7, agentPos.z + 10);
    this.targetLookAt.copy(agentPos);
  }

  public update(delta: number) {
    if (this.userInteractedTimer > 0) {
      this.userInteractedTimer -= delta;
    }

    // Smooth camera glide towards target
    this.camera.position.lerp(this.targetPosition, 0.04);
    this.controls.target.lerp(this.targetLookAt, 0.04);
    this.controls.update();
  }
}

