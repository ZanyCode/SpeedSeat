import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommandValue } from 'src/app/models/command';

@Component({
  selector: 'action-setting',
  templateUrl: './action-setting.component.html',
  styleUrls: ['./action-setting.component.scss']
})
export class ActionSettingComponent implements OnInit {
  @Input() value!: CommandValue;
  @Output() valueChanged = new EventEmitter();

  constructor() { }

  ngOnInit(): void {
  }

}
