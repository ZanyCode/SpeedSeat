import { Component, ElementRef, EventEmitter, Input, OnInit, Output, ViewChild } from '@angular/core';
import { PlotMouseEvent } from 'plotly.js-dist-min';

export interface ResponseCurvePoint {
  output: number;
  input: number;
}

@Component({
  selector: 'app-curve-editor',
  templateUrl: './curve-editor.component.html',
  styleUrls: ['./curve-editor.component.scss']
})
export class CurveEditorComponent implements OnInit {
  isDragging = false;
  selectedPointIndex = -1;
  hasUnsavedChanges = false;
  previousTouchY: number | undefined = undefined;

  @Input() config = {
    handleSize: 15,
  }

  @ViewChild('handle') handle!: ElementRef<HTMLElement>;
  @ViewChild('dragOverlay') dragOverlay!: ElementRef<HTMLElement>;
  handleTop = 0;
  handleLeft = 0;

  _curve: ResponseCurvePoint[] = [];
  @Input() set curve(value: ResponseCurvePoint[]) {
   this._curve = value;
   this.internalCurve = this._curve.map(x => ({...x}));
  }

  get curve(): ResponseCurvePoint[] {
    return this._curve;
  }

  private _internalCurve: ResponseCurvePoint[] = [];
  public get internalCurve(): ResponseCurvePoint[] {
    return this._internalCurve;
  }
  public set internalCurve(value: ResponseCurvePoint[]) {
    this._internalCurve = value;
    this.updateGraph(this._internalCurve);
  }
  
  @Output() curveChange: EventEmitter<ResponseCurvePoint[]> = new EventEmitter();
  

  public graph = {
    data: [
        { x: [] as number[], y: [] as number[], type: 'scattergl', mode: 'lines', marker: {color: 'red'}, name: 'curve'},
    ],
    layout: { 
      margin: {
        l: 40,
        r: 40,
        b: 40,
        t: 40,
        pad: 4
      },
      hovermode: 'closest' ,
      xaxis: { fixedrange: true, range: [0, 1], title: 'Input' },
      yaxis: {fixedrange: true, range: [0, 1], title: 'Output'},
    },
  };

  constructor() { }

  ngOnInit(): void {
  }

  onPlotClick(event: PlotMouseEvent) {
    this.previousTouchY = undefined;
    this.selectedPointIndex = event.points[0].pointIndex;
    var pixelPoint = this.convertCurvePointToPixelPoint(event.points[0] as any);
    this.handleTop = pixelPoint.input - this.config.handleSize / 2;
    this.handleLeft = pixelPoint.output - this.config.handleSize / 2;
  }

  startDrag() {
    this.isDragging = true;
  }

  endDrag() {
    this.isDragging = false;
  }

  onPointDrag(event:MouseEvent | TouchEvent) {
    if (this.isDragging) {
      if(event instanceof MouseEvent) {
        this.handleTop -= event.movementY;
      }
      else {
        this.handleTop -= event.touches[0].pageY - (this.previousTouchY ?? event.touches[0].pageY);        
        this.previousTouchY = event.touches[0].pageY;
        event.preventDefault();
      }

      this.handleTop = Math.min(Math.max(0 - this.config.handleSize / 2, this.handleTop), this.dragOverlay.nativeElement.offsetHeight - this.config.handleSize / 2);

      const newCurvePoint = this.convertPixelPointToCurvePoint({ input: this.handleLeft + this.config.handleSize / 2, output: this.handleTop + this.config.handleSize / 2 });
      this.internalCurve = this.internalCurve = this.internalCurve.map((p, i) => {
        if (i < this.selectedPointIndex) {
          return { ...p, output: Math.min(newCurvePoint.output, p.output) };
        }
        else if(i > this.selectedPointIndex) {
          return {...p, output: Math.max(newCurvePoint.output, p.output) };
        }

        return newCurvePoint;
      });
      this.hasUnsavedChanges = true;
    }
  }

  onSave() {
    this.curve = this.internalCurve;
    this.curveChange.emit(this.curve);
    this.hasUnsavedChanges = false;
    this.selectedPointIndex = -1;
  }

  onCancel() {
    this.internalCurve = this.curve;
    this.hasUnsavedChanges = false;
    this.selectedPointIndex = -1;
  }

  onResetToLinear() {
    this.internalCurve = new Array(11).fill(0).map((_, i) => ({ output: i / 10, input: i / 10}));
    this.hasUnsavedChanges = true;
    this.selectedPointIndex = -1;
  }

  private updateGraph(curve: ResponseCurvePoint[]) {
    this.graph = {
      ...this.graph,
      data: [
        { x: curve.map(p => p.input), y: curve.map(p => p.output), type: 'scattergl', mode: 'lines', marker: {color: 'red'}, name: 'curve' },
      ]
    }
  }

  private convertCurvePointToPixelPoint(curvePoint: {x: number, y: number})  {
    const { x, y } = curvePoint;
    const { offsetWidth, offsetHeight } = this.dragOverlay.nativeElement;
    const xPx = x * offsetWidth;
    const yPx = y * offsetHeight;
    return { output: xPx, input: yPx };
  }

  private convertPixelPointToCurvePoint(pixelPoint: ResponseCurvePoint) {
    const { output, input } = pixelPoint;
    const { offsetWidth, offsetHeight } = this.dragOverlay.nativeElement;
    const outputC = output / offsetHeight;
    const inputC = input / offsetWidth;
    return { output: outputC, input: inputC };
  }
}
