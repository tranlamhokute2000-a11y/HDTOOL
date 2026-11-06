import * as THREE from 'three';

export interface Waypoint {
  x: number;
  z: number;
}

export class PathNavigator {
  // Center model table obstacle: circle at (0,0) r=3.6
  private static OBSTACLE_CENTER = new THREE.Vector2(0, 0);
  private static OBSTACLE_RADIUS = 3.6;

  // Plan path between start and target avoiding central table
  public static planPath(start: THREE.Vector3, target: THREE.Vector3): Waypoint[] {
    const p1 = new THREE.Vector2(start.x, start.z);
    const p2 = new THREE.Vector2(target.x, target.z);

    const waypoints: Waypoint[] = [];
    waypoints.push({ x: p1.x, z: p1.y });

    // Check if line segment intersects circle at (0,0) r = OBSTACLE_RADIUS
    if (this.intersectsCircle(p1, p2, this.OBSTACLE_CENTER, this.OBSTACLE_RADIUS)) {
      // Route around the circle: pick north or south detour point
      const midZ = (p1.y + p2.y) / 2;
      const detourZ = midZ < 0 ? -4.5 : 4.5;
      const detourX = ((p1.x + p2.x) / 2) > 0 ? 4.5 : -4.5;

      waypoints.push({ x: detourX, z: detourZ });
    }

    waypoints.push({ x: p2.x, z: p2.y });
    return waypoints;
  }

  private static intersectsCircle(
    p1: THREE.Vector2,
    p2: THREE.Vector2,
    center: THREE.Vector2,
    radius: number
  ): boolean {
    const d = new THREE.Vector2().subVectors(p2, p1);
    const f = new THREE.Vector2().subVectors(p1, center);

    const a = d.dot(d);
    const b = 2 * f.dot(d);
    const c = f.dot(f) - radius * radius;

    let discriminant = b * b - 4 * a * c;
    if (discriminant < 0) return false;

    discriminant = Math.sqrt(discriminant);
    const t1 = (-b - discriminant) / (2 * a);
    const t2 = (-b + discriminant) / (2 * a);

    return (t1 >= 0 && t1 <= 1) || (t2 >= 0 && t2 <= 1);
  }
}

