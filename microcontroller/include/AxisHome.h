#include "Axis.h"

void Axis::home()
{
#ifdef NO_HARDWARE
    return;
#else

    initializeHardware();
    if (!SteppingIsEnabled)
    {
        return;
    }
    if (homingActive)
    {
        return;
    }
    AxisIsHomed = false;
    homingStep = waitForAxisToStop;
    homingActive = true;
#endif
}

void Axis::executeHoming()
{
    switch (homingStep)
    {
    case waitForAxisToStop:
        if (active)
        {
            stop();
        }
        else
        {
            setTempACC_DCC(accelerationWhileHoming);
            homingStep = clearEndstop;
        }
        break;

    case clearEndstop:
        if (!digitalReadAverage(Pin_Endstop))
        {
            if (!active)
            {
                moveVelocityInternal(speedWhileHoming, _POSITIVE);
            }
        }
        else
        {
            stop();
            homingStep = driveToEndstop;
        }
        break;

    case driveToEndstop:
        if (!digitalReadAverage(Pin_Endstop))
        {
            // set the current position to the breaking distance to prevent the axis from stopping instantly when reaching the endstop
            currentPosition = ((unsigned long long)currentVelocity * currentVelocity) / (2 * deceleration);
            stop();
            homingStep = moveFromEndstopAfterHoming;
        }
        else
        {
            if (!active)
            {
                setTempACC_DCC(accelerationWhileHoming);
                moveVelocityInternal(speedWhileHoming, _NEGATIVE);
            }
        }
        break;

    case moveFromEndstopAfterHoming:
        if (!active)
        {
            setTempACC_DCC(20 * stepsPerMillimeter);
            resetSpeed();
            currentPosition = 0;
            targetPosition = 0;
            moveAbsoluteInternal(homingOffset + (maxPosition / 2)); // move to center position before setting the homing done flag
            homingStep = waitingForEndOfMovement;
        }
        break;

    case waitingForEndOfMovement:
        if (!active)
        {
            resetAcceleration();
            resetDeceleration();
            resetSpeed();
            currentPosition = maxPosition / 2;
            targetPosition = currentPosition;
            AxisIsHomed = true;
            homingActive = false;
        }
        break;

    default:
        break;
    }
}