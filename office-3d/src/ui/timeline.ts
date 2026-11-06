export class TimelineUI {
  public isPaused = false;
  public speed = 1;
  public onPlayPause?: (paused: boolean) => void;
  public onSpeedChange?: (speed: number) => void;
  public onSeek?: (percent: number) => void;
  public onGoLive?: () => void;

  private playPauseBtn: HTMLButtonElement;
  private speedBtn: HTMLButtonElement;
  private slider: HTMLInputElement;
  private liveBtn: HTMLButtonElement;

  constructor() {
    this.playPauseBtn = document.getElementById('btn-play-pause') as HTMLButtonElement;
    this.speedBtn = document.getElementById('btn-speed') as HTMLButtonElement;
    this.slider = document.getElementById('timeline-slider') as HTMLInputElement;
    this.liveBtn = document.getElementById('btn-live') as HTMLButtonElement;

    this.playPauseBtn.addEventListener('click', () => {
      this.isPaused = !this.isPaused;
      this.playPauseBtn.textContent = this.isPaused ? '▶ Tiếp tục' : '⏸ Tạm dừng';
      if (this.onPlayPause) this.onPlayPause(this.isPaused);
    });

    this.speedBtn.addEventListener('click', () => {
      if (this.speed === 1) this.speed = 4;
      else if (this.speed === 4) this.speed = 16;
      else this.speed = 1;

      this.speedBtn.textContent = `x${this.speed}`;
      if (this.onSpeedChange) this.onSpeedChange(this.speed);
    });

    this.slider.addEventListener('input', () => {
      const val = parseFloat(this.slider.value);
      if (this.onSeek) this.onSeek(val);
    });

    this.liveBtn.addEventListener('click', () => {
      this.slider.value = '100';
      if (this.onGoLive) this.onGoLive();
    });
  }

  public setProgress(percent: number) {
    this.slider.value = percent.toString();
  }
}

