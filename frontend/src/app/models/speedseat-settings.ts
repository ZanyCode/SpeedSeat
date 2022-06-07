import { Point } from "../curve-editor/curve-editor.component";

export interface SpeedseatSettings
{
  frontLeftMotorIdx: number;
  frontRightMotorIdx: number;
  backMotorIdx: number;
  frontTiltPriority: number;
  backMotorResponseCurve: Point[];

  frontTiltGforceMultiplier: number;
  frontTiltOutputCap: number;
  frontTiltSmoothing: number;
  frontTiltReverse: boolean;

  sideTiltGforceMultiplier: number;
  sideTiltOutputCap: number;
  sideTiltSmoothing: number;
  sideTiltReverse: boolean;
}