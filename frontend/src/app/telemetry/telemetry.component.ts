import { Component, OnInit } from '@angular/core';

@Component({
  selector: 'app-telemetry',
  templateUrl: './telemetry.component.html',
  styleUrls: ['./telemetry.component.scss']
})
export class TelemetryComponent implements OnInit {
  private _frontLeftMotorPosition: number | null = 0.5;
  public get frontLeftMotorPosition(): number | null {
    return this._frontLeftMotorPosition;
  }
  public set frontLeftMotorPosition(value: number | null) {
    this._frontLeftMotorPosition = value;
    // this.data.setFrontLeftMotorPosition(value ?? 0.5);
  }


  constructor() { }

  ngOnInit(): void {
  }

}
