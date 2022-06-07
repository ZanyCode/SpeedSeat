import { Component, ElementRef, EventEmitter, Input, OnInit, Output, ViewChild } from '@angular/core';
import { PlotMouseEvent } from 'plotly.js-dist-min';

export interface Point {
  x: number;
  y: number;
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

  _curve: Point[] = [];
  @Input() set curve(value: Point[]) {
   this._curve = value;
   this.internalCurve = this._curve.map(x => ({...x}));
  }

  get curve(): Point[] {
    return this._curve;
  }

  private _internalCurve: Point[] = [];
  public get internalCurve(): Point[] {
    return this._internalCurve;
  }
  public set internalCurve(value: Point[]) {
    this._internalCurve = value;
    this.updateGraph(this._internalCurve);
  }
  
  @Output() curveChange: EventEmitter<Point[]> = new EventEmitter();
  

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
      xaxis: { fixedrange: true, range: [0, 1], title: 'Output' },
      yaxis: {fixedrange: true, range: [0, 1], title: 'Input'},
    },
  };

  constructor() { }

  ngOnInit(): void {
  }

  onPlotClick(event: PlotMouseEvent) {
    this.previousTouchY = undefined;
    this.selectedPointIndex = event.points[0].pointIndex;
    var pixelPoint = this.convertCurvePointToPixelPoint(event.points[0] as any);
    this.handleTop = pixelPoint.y - this.config.handleSize / 2;
    this.handleLeft = pixelPoint.x - this.config.handleSize / 2;
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

      const newCurvePoint = this.convertPixelPointToCurvePoint({ x: this.handleLeft + this.config.handleSize / 2, y: this.handleTop + this.config.handleSize / 2 });
      this.internalCurve = this.internalCurve = this.internalCurve.map((p, i) => {
        if (i < this.selectedPointIndex) {
          return { ...p, y: Math.min(newCurvePoint.y, p.y) };
        }
        else if(i > this.selectedPointIndex) {
          return {...p, y: Math.max(newCurvePoint.y, p.y)};
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
    this.internalCurve = new Array(11).fill(0).map((_, i) => ({ x: i / 10, y: i / 10}));
    this.hasUnsavedChanges = true;
    this.selectedPointIndex = -1;
  }

  private updateGraph(curve: Point[]) {
    this.graph = {
      ...this.graph,
      data: [
        { x: curve.map(p => p.x), y: curve.map(p => p.y), type: 'scattergl', mode: 'lines', marker: {color: 'red'}, name: 'curve' },
      ]
    }
  }

  private convertCurvePointToPixelPoint(curvePoint: Point) {
    const { x, y } = curvePoint;
    const { offsetWidth, offsetHeight } = this.dragOverlay.nativeElement;
    const xPx = x * offsetWidth;
    const yPx = y * offsetHeight;
    return { x: xPx, y: yPx };
  }

  private convertPixelPointToCurvePoint(pixelPoint: Point) {
    const { x, y } = pixelPoint;
    const { offsetWidth, offsetHeight } = this.dragOverlay.nativeElement;
    const xC = x / offsetWidth;
    const yC = y / offsetHeight;
    return { x: xC, y: yC };
  }
}
