import { Component, OnDestroy, OnInit } from '@angular/core';
import { ManualControlDataService } from './manual-control-data.service';

@Component({
  selector: 'app-manual-control',
  templateUrl: './manual-control.component.html',
  styleUrls: ['./manual-control.component.scss']
})
export class ManualControlComponent implements OnInit, OnDestroy {

  constructor(public data: ManualControlDataService) { }

  ngOnInit(): void {
  }
 
  ngOnDestroy(): void {
    this.data.destroy();
  }  
}
