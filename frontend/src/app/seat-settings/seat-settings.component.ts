import { Component, OnInit } from '@angular/core';
import { Command } from '../models/command';
import { SeatSettingsDataService } from './seat-settings-data.service';

@Component({
  selector: 'app-seat-settings',
  templateUrl: './seat-settings.component.html',
  styleUrls: ['./seat-settings.component.scss']
})
export class SeatSettingsComponent implements OnInit {
  data: SeatSettingsDataService;
  commands?: Command[];

  constructor(data: SeatSettingsDataService) { 
    this.data = data;
  }

  ngOnInit(): void {
    this.data.init().then(async () => {
      this.commands = await this.data.getCommands();
      console.log(this.commands);
    });  
  }
}
