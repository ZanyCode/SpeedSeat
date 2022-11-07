import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { AppEventsService } from '../app-events.service';
import { Command } from '../models/command';
import { SeatSettingsDataService } from '../seat-settings/seat-settings-data.service';
import { ManualControlDataService } from './manual-control-data.service';

@Component({
  selector: 'app-manual-control',
  templateUrl: './manual-control.component.html',
  styleUrls: ['./manual-control.component.scss']
})
export class ManualControlComponent implements OnInit, OnDestroy {
  public isInitialized = false;
  public isConnected = false;
  public isMotorPositionValid = true;
  public isTiltPositionValid = true;
  private connectionStateSubscription: Subscription | undefined;
  public command: Command | undefined;

  private _frontLeftMotorPosition: number | null = 0.5;
  public get frontLeftMotorPosition(): number | null {
    return this._frontLeftMotorPosition;
  }
  public set frontLeftMotorPosition(value: number | null) {
    this._frontLeftMotorPosition = value;
    this.data.setFrontLeftMotorPosition(value ?? 0.5);
  }

  private _frontRightMotorPosition: number | null = 0.5;
  public get frontRightMotorPosition(): number | null {
    return this._frontRightMotorPosition;
  }
  public set frontRightMotorPosition(value: number | null) {
    this._frontRightMotorPosition = value;
    this.data.setFrontRightMotorPosition(value ?? 0.5);
  }

  private _backMotorPosition: number | null = 0.5;
  public get backMotorPosition(): number | null {
    return this._backMotorPosition;
  }
  public set backMotorPosition(value: number | null) {
    this._backMotorPosition = value;
    this.data.setBackMotorPosition(value ?? 0.5);
  }

  private _frontTilt: number | null = 0;
  public get frontTilt(): number | null {
    return this._frontTilt;
  }
  public set frontTilt(value: number | null) {
    this._frontTilt = value;
    this.data.setTilt(value ?? 0, this.sideTilt ?? 0);
  }

  private _sideTilt: number | null = 0;
  public get sideTilt(): number | null {
    return this._sideTilt;
  }
  public set sideTilt(value: number | null) {
    this._sideTilt = value;
    this.data.setTilt(this.frontTilt ?? 0, value ?? 0);
  }

  constructor(public data: ManualControlDataService, private events: AppEventsService, private settings: SeatSettingsDataService) { }

  ngOnInit(): void {
    this.connectionStateSubscription = this.events.ConnectionStateChanged.subscribe(connected => {
      if (connected) {
        this.updateValuesFromDataservice();
      }
      else {
        this.isMotorPositionValid = false;
        this.isTiltPositionValid = false;
        this.isInitialized = false;
        this.data.destroy();
        this.settings.destroy();
      }
    })
  }

  updateValuesFromDataservice() {
    this.data.init().then(async () => {
      const seat = await this.data.getCurrentState();    
      this.isConnected = seat.isConnected;
      this.isMotorPositionValid = true;
      this.isTiltPositionValid = true;
      this.isInitialized = true;
    });

    this.settings.init().then(async () => {
      this.command = await (await this.settings.getCommands()).filter(x => x.id == 0)[0];
      this._frontRightMotorPosition = this.command.value1.value;
      this._frontLeftMotorPosition = this.command.value2.value;
      this._backMotorPosition = this.command.value3.value;
    });
  }

  onPositionsChanged(command: Command) {
    this.settings.updateSetting(command);
  }

  ngOnDestroy(): void {
    this.connectionStateSubscription?.unsubscribe();
    this.data.destroy();
  }
}
