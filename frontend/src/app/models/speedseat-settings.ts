export interface SpeedseatSettings
{
  frontLeftMotorIdx: number;
  frontRightMotorIdx: number;
  backMotorIdx: number;
  frontTiltPriority: number;

  frontTiltGforceMultiplier: number;
  frontTiltOutputCap: number;
  frontTiltSmoothing: number;
  frontTiltReverse: boolean;

  sideTiltGforceMultiplier: number;
  sideTiltOutputCap: number;
  sideTiltSmoothing: number;
  sideTiltReverse: boolean;
}