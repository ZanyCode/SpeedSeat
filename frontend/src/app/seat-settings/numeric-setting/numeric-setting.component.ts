import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommandValue } from 'src/app/models/command';

@Component({
  selector: 'numeric-setting',
  templateUrl: './numeric-setting.component.html',
  styleUrls: ['./numeric-setting.component.scss']
})
export class NumericSettingComponent implements OnInit {
   @Input() value!: CommandValue;
   @Output() valueChanged = new EventEmitter();

  constructor() { }

  ngOnInit(): void {
  }

}
