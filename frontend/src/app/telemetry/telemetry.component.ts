import { Component, OnDestroy, OnInit } from '@angular/core';
import { DataPoint, TelemetryDataService } from './telemetry-data.service';

@Component({
  selector: 'app-telemetry',
  templateUrl: './telemetry.component.html',
  styleUrls: ['./telemetry.component.scss']
})
export class TelemetryComponent implements OnInit, OnDestroy {
  isStreaming = false;
  frontTiltTelemetry: DataPoint[] = [];
  sideTiltTelemetry: DataPoint[] = [];

  public graph = {
    data: [
        { x: [new Date(), new Date(), new Date()], y: [2, 6, 3], type: 'scattergl', mode: 'lines', marker: {color: 'red'}, name: 'test' },
    ],
    layout: { 
      margin: {
        l: 50,
        r: 50,
        b: 50,
        t: 50,
        pad: 4
      },
      xaxis: {range: [new Date(), new Date()]},
      yaxis: {range: [-1, 1]}
    },
  };

  private _frontTiltGForceMultiplier: number | null = 0.3;
  public get frontTiltGForceMultiplier(): number | null {
    return this._frontTiltGForceMultiplier;
  }
  public set frontTiltGForceMultiplier(value: number | null) {
    this._frontTiltGForceMultiplier = value;
    this.data.setFrontTiltGForceMultiplier(value ?? 0.3);
  }

  private _frontTiltOutputCap: number | null = 1.0;
  public get frontTiltOutputCap(): number | null {
    return this._frontTiltOutputCap;
  }
  public set frontTiltOutputCap(value: number | null) {
    this._frontTiltOutputCap = value;
    this.data.setFrontTiltOutputCap(value ?? 1.0);
  }

  private _frontTiltSmoothing: number | null = 0;
  public get frontTiltSmoothing(): number | null {
    return this._frontTiltSmoothing;
  }
  public set frontTiltSmoothing(value: number | null) {
    this._frontTiltSmoothing = value;
    this.data.setFrontTiltSmoothing(value ?? 0);
  }

  private _sideTiltGForceMultiplier: number | null = 0.3;
  public get sideTiltGForceMultiplier(): number | null {
    return this._sideTiltGForceMultiplier;
  }
  public set sideTiltGForceMultiplier(value: number | null) {
    this._sideTiltGForceMultiplier = value;
    this.data.setSideTiltGForceMultiplier(value ?? 0.3);
  }

  private _sideTiltOutputCap: number | null = 1.0;
  public get sideTiltOutputCap(): number | null {
    return this._sideTiltOutputCap;
  }
  public set sideTiltOutputCap(value: number | null) {
    this._sideTiltOutputCap = value;
    this.data.setSideTiltOutputCap(value ?? 1.0);
  }

  private _sideTiltSmoothing: number | null = 0;
  public get sideTiltSmoothing(): number | null {
    return this._sideTiltSmoothing;
  }
  public set sideTiltSmoothing(value: number | null) {
    this._sideTiltSmoothing = value;
    this.data.setSideTiltSmoothing(value ?? 0);
  }

  private _frontTiltReverse: boolean = false;
  public get frontTiltReverse(): boolean {
    return this._frontTiltReverse;
  }
  public set frontTiltReverse(value: boolean) {
    this._frontTiltReverse = value;
    this.data.setFrontTiltReverse(value);
  }

  private _sideTiltReverse: boolean = false;
  public get sideTiltReverse(): boolean {
    return this._sideTiltReverse;
  }
  public set sideTiltReverse(value: boolean) {
    this._sideTiltReverse = value;
    this.data.setSideTiltReverse(value);
  }

  constructor(private data: TelemetryDataService) { }


  ngOnInit(): void {
    this.data.init(this.onUpdateTelemetry).then(async () => {
      const settings = await this.data.getCurrentState();
      this.isStreaming = await this.data.getIsStreaming();
      this._frontTiltGForceMultiplier = settings.frontTiltGforceMultiplier;    
      this._frontTiltOutputCap = settings.frontTiltOutputCap;    
      this._frontTiltSmoothing = settings.frontTiltSmoothing;    
      this._sideTiltGForceMultiplier = settings.sideTiltGforceMultiplier;    
      this._sideTiltOutputCap = settings.sideTiltOutputCap;    
      this._sideTiltSmoothing = settings.sideTiltSmoothing;    
      this._frontTiltReverse = settings.frontTiltReverse;
      this._sideTiltReverse = settings.sideTiltReverse;
    });
  }

  async startStreaming() {
    await this.data.startStreaming();
    this.isStreaming = true;    
  }

  async stopStreaming() {
    await this.data.stopStreaming();
    this.isStreaming = false;
  }

  onUpdateTelemetry = (frontTiltTelemetry: DataPoint[], sideTiltTelemetry: DataPoint[]) => {
    const maxPoints = 1200;
    this.frontTiltTelemetry = [...this.frontTiltTelemetry, ...frontTiltTelemetry].slice(-maxPoints);
    this.sideTiltTelemetry = [...this.sideTiltTelemetry, ...sideTiltTelemetry].slice(-maxPoints);
    this.graph.data = [
      { x: this.frontTiltTelemetry.map(t => t.x), y: this.frontTiltTelemetry.map(t => t.y), type: 'scattergl', mode: 'lines', marker: {color: 'red'}, name: 'Front Tilt' },
      { x: this.sideTiltTelemetry.map(t => t.x), y: this.sideTiltTelemetry.map(t => t.y), type: 'scattergl', mode: 'lines', marker: {color: 'green'}, name: 'Side Tilt' }
    ]

    const now = new Date();
    const before30s = new Date();
    before30s.setSeconds(before30s.getSeconds() - 30);
    this.graph.layout = {
      ...this.graph.layout,
      xaxis: {range: [before30s, now]},
    }
  }

  ngOnDestroy(): void {
    this.data.destroy();
  }
}
