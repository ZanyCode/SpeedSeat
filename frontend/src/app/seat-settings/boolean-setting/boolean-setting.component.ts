import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommandValue } from 'src/app/models/command';

@Component({
  selector: 'boolean-setting',
  templateUrl: './boolean-setting.component.html',
  styleUrls: ['./boolean-setting.component.scss']
})
export class BooleanSettingComponent implements OnInit {
  @Input() value!: CommandValue;
  @Output() valueChanged = new EventEmitter();

  constructor() { }

  ngOnInit(): void {
  }

  onValueChanged(checked: boolean) {
    this.value.value = checked ? 1 : 0;
    this.valueChanged.emit();
  }
}
