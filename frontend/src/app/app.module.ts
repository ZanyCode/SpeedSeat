import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { LayoutModule } from '@angular/cdk/layout';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatSidenavModule } from '@angular/material/sidenav';
import {MatInputModule} from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { ManualControlComponent } from './manual-control/manual-control.component';
import { ProgramSettingsComponent } from './program-settings/program-settings.component';
import {MatCardModule} from '@angular/material/card';
import {MatSliderModule} from '@angular/material/slider';
import {MatSelectModule} from '@angular/material/select';
import {MatCheckboxModule} from '@angular/material/checkbox';
import { QRCodeModule } from 'angularx-qrcode';
import { TelemetryComponent } from './telemetry/telemetry.component';
import * as PlotlyJS from 'plotly.js-dist-min';
import { PlotlyModule } from 'angular-plotly.js';
import { CurveEditorModule } from './program-settings/curve-editor/curve-editor.module';
import { FormsModule } from '@angular/forms';
import { SeatSettingsComponent } from './seat-settings/seat-settings.component';
import { NumericSettingComponent } from './seat-settings/numeric-setting/numeric-setting.component';
import { BooleanSettingComponent } from './seat-settings/boolean-setting/boolean-setting.component';
import { ActionSettingComponent } from './seat-settings/action-setting/action-setting.component';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {ScrollingModule as ExperimentalScrollingModule} from '@angular/cdk-experimental/scrolling';
import {ScrollingModule } from '@angular/cdk/scrolling';

PlotlyModule.plotlyjs = PlotlyJS;

@NgModule({
  declarations: [
    AppComponent,
    ManualControlComponent,
    ProgramSettingsComponent,
    TelemetryComponent,
    SeatSettingsComponent,
    NumericSettingComponent,
    BooleanSettingComponent,
    ActionSettingComponent,
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    LayoutModule,
    MatToolbarModule,
    MatButtonModule,
    MatSidenavModule,
    MatIconModule,
    MatListModule,
    MatCardModule,
    MatSliderModule,
    MatSelectModule,
    MatCheckboxModule,
    MatInputModule,
    QRCodeModule,
    PlotlyModule,
    FormsModule,
    CurveEditorModule,
    MatProgressSpinnerModule,
    ScrollingModule,
    ExperimentalScrollingModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
