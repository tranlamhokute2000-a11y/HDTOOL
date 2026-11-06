import { OfficeEvent } from './bridge.js';

export class DemoRunner {
  private events: OfficeEvent[] = [];
  private currentIndex = 0;
  private isRunning = false;
  private timer: any = null;
  private onDispatch: (event: OfficeEvent) => void;

  constructor(onDispatch: (event: OfficeEvent) => void) {
    this.onDispatch = onDispatch;
  }

  public async loadEvents(): Promise<boolean> {
    try {
      // Try loading from /api/history?tool=demo-tool
      const res = await fetch('/api/history?tool=demo-tool');
      if (res.ok) {
        const data = await res.json();
        if (data.events && data.events.length > 0) {
          this.events = data.events;
          return true;
        }
      }
    } catch (e) { }

    return false;
  }

  public start(speedMultiplier: number = 1.0) {
    if (this.isRunning || this.events.length === 0) return;
    this.isRunning = true;
    this.step(speedMultiplier);
  }

  private step(speedMultiplier: number) {
    if (!this.isRunning) return;

    if (this.currentIndex >= this.events.length) {
      // Loop after 5s pause
      setTimeout(() => {
        this.currentIndex = 0;
        this.step(speedMultiplier);
      }, 5000);
      return;
    }

    const currentEv = this.events[this.currentIndex];
    this.onDispatch(currentEv);

    // Compute interval to next event (approx 1.5s per event, total ~95s for 63 events)
    let delay = 1600 / speedMultiplier;
    if (currentEv.event === 'progress') delay = 900 / speedMultiplier;
    if (currentEv.event === 'handoff') delay = 2200 / speedMultiplier;

    this.currentIndex++;
    this.timer = setTimeout(() => this.step(speedMultiplier), delay);
  }

  public stop() {
    this.isRunning = false;
    if (this.timer) clearTimeout(this.timer);
  }

  public setSpeed(speed: number) {
    if (this.isRunning) {
      this.stop();
      this.start(speed);
    }
  }
}

