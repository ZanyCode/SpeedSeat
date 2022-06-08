import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CurveEditorComponent } from './curve-editor.component';
import { PlotlyModule } from 'angular-plotly.js';
import { MatButtonModule } from '@angular/material/button';



@NgModule({
  declarations: [
    CurveEditorComponent
  ],
  imports: [
    CommonModule,
    PlotlyModule,
    MatButtonModule
  ],
  exports: [
    CurveEditorComponent
  ]
})
export class CurveEditorModule { }
