import { Component, OnDestroy, OnInit } from '@angular/core';
import { SettingsDataService } from './settings-data.service';

@Component({
  selector: 'app-settings',
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent implements OnInit, OnDestroy {
  private _frontLeftMotorIdx = 0;
  public get frontLeftMotorIdx() {
    return this._frontLeftMotorIdx;
  }
  public set frontLeftMotorIdx(value) {
    this._frontLeftMotorIdx = value;
    this.data.SetFrontLeftMotorIdx(value);
  }

  private _frontRightMotorIdx = 1;
  public get frontRightMotorIdx() {
    return this._frontRightMotorIdx;
  }
  public set frontRightMotorIdx(value) {
    this._frontRightMotorIdx = value;
    this.data.SetFrontRightMotorIdx(value);
  }

  private _backMotorIdx = 2;
  public get backMotorIdx() {
    return this._backMotorIdx;
  }
  public set backMotorIdx(value) {
    this._backMotorIdx = value;
    this.data.SetBackMotorIdx(value);
  }

  constructor(private data: SettingsDataService) { } 

  ngOnInit(): void {
    this.data.init().then(async () => {
      const settings = await this.data.GetSettings();
      this._frontLeftMotorIdx = settings.frontLeftMotorIdx;
      this._frontRightMotorIdx = settings.frontRightMotorIdx;
      this._backMotorIdx = settings.backMotorIdx;
    });
  }

  ngOnDestroy(): void {
    this.data.destroy();
  }

}
