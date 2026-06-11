import { ResponseCurvePoint } from "../program-settings/curve-editor/curve-editor.component";

export interface SpeedseatSettings
{
  frontLeftMotorIdx: number;
  frontRightMotorIdx: number;
  backMotorIdx: number;
  frontTiltPriority: number;
  backMotorResponseCurve: ResponseCurvePoint[];
  sideMotorResponseCurve: ResponseCurvePoint[];

  frontTiltGforceMultiplier: number;
  frontTiltOutputCap: number;
  frontTiltSmoothing: number;
  frontTiltReverse: boolean;

  sideTiltGforceMultiplier: number;
  sideTiltOutputCap: number;
  sideTiltSmoothing: number;
  sideTiltReverse: boolean;
}