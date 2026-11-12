import { OfficeEvent } from './bridge.js';

export class DemoRunner {
  private events: OfficeEvent[] = [];
  private currentIndex = 0;
  private isRunning = false;
  private timer: any = null;
  private onDispatch: (event: OfficeEvent) => void;
  public onComplete?: () => void;

  constructor(onDispatch: (event: OfficeEvent) => void) {
    this.onDispatch = onDispatch;
  }

  public setEvents(events: OfficeEvent[]) {
    this.events = events || [];
    this.currentIndex = 0;
  }

  public async loadEvents(toolId: string = 'demo:office'): Promise<boolean> {
    try {
      const res = await fetch(`/api/history?tool=${encodeURIComponent(toolId)}`);
      if (res.ok) {
        const data = await res.json();
        if (data.events && data.events.length > 0) {
          this.events = data.events;
          this.currentIndex = 0;
          return true;
        }
      }
    } catch { }
    return false;
  }

  public start(speedMultiplier: number = 1.0, loop = true) {
    if (this.isRunning || this.events.length === 0) return;
    this.isRunning = true;
    this.step(speedMultiplier, loop);
  }

  private step(speedMultiplier: number, loop: boolean) {
    if (!this.isRunning) return;

    if (this.currentIndex >= this.events.length) {
      this.isRunning = false;
      if (this.onComplete) this.onComplete();
      if (loop) {
        this.timer = setTimeout(() => {
          this.currentIndex = 0;
          this.isRunning = true;
          this.step(speedMultiplier, true);
        }, 4500);
      }
      return;
    }

    const currentEv = this.events[this.currentIndex];
    this.onDispatch(currentEv);

    let delay = 1400 / speedMultiplier;
    if (currentEv.event === 'progress') delay = 850 / speedMultiplier;
    if (currentEv.event === 'handoff') delay = 2200 / speedMultiplier;

    this.currentIndex++;
    this.timer = setTimeout(() => this.step(speedMultiplier, loop), delay);
  }

  public stop() {
    this.isRunning = false;
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  public setSpeed(speed: number) {
    if (this.isRunning) {
      this.stop();
      this.start(speed);
    }
  }
}
