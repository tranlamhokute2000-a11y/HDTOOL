import * as THREE from 'three';

export interface Waypoint {
  x: number;
  z: number;
}

export class PathNavigator {
  // Plan clean corridor-aware walking path between rooms
  public static planPath(start: THREE.Vector3, target: THREE.Vector3): Waypoint[] {
    const waypoints: Waypoint[] = [{ x: start.x, z: start.z }];

    // Corridor spines: horizontal North-mid aisle at Z = -2.0, South aisle at Z = 5.4
    const needsNorthSouthHop = (start.z < -2.4 && target.z > 5.0) || (target.z < -2.4 && start.z > 5.0);
    if (needsNorthSouthHop) {
      waypoints.push({ x: -12.0, z: -2.0 });
      waypoints.push({ x: -12.0, z: 5.4 });
    } else if ((start.z < -2.5 && target.z > -1.5) || (target.z < -2.5 && start.z > -1.5)) {
      const doorX = Math.abs(start.x - 7.5) < Math.abs(start.x + 12.0) ? 7.5 : -12.0;
      waypoints.push({ x: doorX, z: -2.2 });
    }

    waypoints.push({ x: target.x, z: target.z });
    return waypoints;
  }
}
