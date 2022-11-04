import { Component, OnDestroy, OnInit } from '@angular/core';
import { Observable, startWith } from 'rxjs';
import { AppEventsService } from '../app-events.service';
import { Command, ValueType } from '../models/command';
import { SeatSettingsDataService } from './seat-settings-data.service';

@Component({
  selector: 'app-seat-settings',
  templateUrl: './seat-settings.component.html',
  styleUrls: ['./seat-settings.component.scss']
})
export class SeatSettingsComponent implements OnInit, OnDestroy {
  commands?: Command[];
  ValueType = ValueType;
  commandObservables: Observable<Command>[] = [];
  connectionStateSubscription: any;
  isInitialized = false;

  constructor(public data: SeatSettingsDataService, private events: AppEventsService) {
  }

  ngOnInit(): void {
    this.connectionStateSubscription = this.events.ConnectionStateChanged.subscribe(connected => {
      if (connected) {
        this.updateValuesFromDataservice();
      }
      else if (this.isInitialized) {
        this.isInitialized = false;
        this.commands = [];
        this.commandObservables = [];
        setTimeout(() => this.data.destroy(), 0);
      }
    })
  }

  ngOnDestroy(): void {
    this.data.destroy();
  }

  updateValuesFromDataservice() {
    this.data.init().then(async () => {
      this.commands = await this.data.getCommands();
      this.commandObservables = this.commands.map(c => this.data.subscribeToConfigurableSetting(c).pipe(startWith(c)));
      this.isInitialized = true;
    });
  }

  onSettingChanged(command: Command) {
    this.data.updateSetting(command);
  }

  onFakeWriteRequest(command: Command) {
    this.data.fakeWriteRequest(command);
  }
}
