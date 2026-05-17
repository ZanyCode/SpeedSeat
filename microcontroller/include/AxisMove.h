#include "Arduino.h"
#include "Axis.h"
#define _TIMERINTERRUPT_LOGLEVEL_ 0
#include "ESP32TimerInterrupt.h"
ESP32Timer InterruptInstance(1);

void Axis::setInterrupt()
{
    InterruptInstance.attachInterrupt(MAX_STEP_FREQUENCY, Axis::globalInterrupt);
    InterruptHasBeenSet = true;
}

bool IRAM_ATTR Axis::globalInterrupt(void *timerNo)
{
    if (!steppingIsEnabled())
    {
        return true;
    }

    for (unsigned i = 0; i < Axis::AxisCounter; i++)
    {
        (*Axis::ExecutePointer[i])();
    }
    while (1)
    {
        bool exitFlag = true;
        for (unsigned i = 0; i < AxisCounter; i++)
        {
            if (stepsThisInterrupt[i] > 0)
            {
                digitalWrite(stepPins[i], toggle[i]);
                toggle[i] = !toggle[i];
                stepsThisInterrupt[i]--;
                if (stepsThisInterrupt > 0)
                {
                    exitFlag = false;
                }
            }
        }
        delayMicroseconds(2);
        if (exitFlag)
        {
            break;
        }
    }

    interruptCounter++;
    return true;
}

void Axis::execute()
{
    if (homingActive)
    {
        executeHoming();
    }
    if (blockInterrupt)
    {
        return;
    }

    _move();
}

void Axis::_move()
{
    active = false;
    unsigned long breakingDistance = ((unsigned long long)currentVelocity * currentVelocity) / (2 * deceleration);
    unsigned long stoppingPosition;
    unsigned long offsetCycleTillNextStep = 0;
    bool makeOneStep = false;
    bool cycleThimeHasBeenExceeded = false;

    if (movementType == _POSITIONING)
    {
        if (currentPosition < targetPosition)
        {
            targetDirection = _POSITIVE;
        }
        else if (currentPosition > targetPosition)
        {
            targetDirection = _NEGATIVE;
        }
        else
        {
            targetDirection = _STANDSTILL;
            if (currentDirection == _STANDSTILL)
            {
                return;
            }
        }
    }
    else if (movementType == _STOPPING)
    {
        targetDirection = _STANDSTILL;
        if (currentDirection == _STANDSTILL)
        {
            return;
        }
    }
    active = true;
    if (currentDirection == _STANDSTILL && targetDirection != _STANDSTILL)
    {
        cyclesTillNextStep = 0;
    }

    // decide if drive will make one step
    if (cyclesTillNextStep <= CYCLES_PER_INTERRUPT)
    {
        cycleThimeHasBeenExceeded = true;
        offsetCycleTillNextStep = CYCLES_PER_INTERRUPT - cyclesTillNextStep;
    }
    else
    {
        cyclesTillNextStep -= CYCLES_PER_INTERRUPT;
    }
    if (cycleThimeHasBeenExceeded && (targetDirection != _STANDSTILL || currentDirection != _STANDSTILL))
    {
        makeOneStep = true;
    }

    if (makeOneStep)
    {
        accelerating = false;
        decelerating = false;
        if (currentDirection == _STANDSTILL)
        {
            accelerating = true;
            currentVelocity = 1;
            speedOffsetThisCycle = 0;
            processorCyclesSinceLaseSpeedUpdate = 0;
            currentDirection = targetDirection;
        }
        else
        {
            if (movementType == _POSITIONING)
            {
                if (currentDirection == _POSITIVE)
                {
                    stoppingPosition = currentPosition + breakingDistance;
                    if (targetPosition > stoppingPosition)
                    {
                        accelerating = true;
                    }
                    else
                    {
                        decelerating = true;
                    }
                }
                else
                {

                    if (currentPosition > breakingDistance)
                    {
                        stoppingPosition = currentPosition - breakingDistance;
                    }
                    else
                    {
                        stoppingPosition = 0;
                    }
                    if (targetPosition < stoppingPosition)
                    {
                        accelerating = true;
                    }
                    else
                    {
                        decelerating = true;
                    }
                }
            }
            else if (movementType == _VELOCITY)
            {
                if (currentDirection != targetDirection)
                {
                    decelerating = true;
                }
                else
                {
                    if (currentVelocity < targetVelocity)
                    {
                        accelerating = true;
                    }
                    else
                    {
                        decelerating = true;
                    }
                }
            }
        }
        if (movementType == _STOPPING)
        {
            decelerating = true;
            accelerating = false;
        }

        // reset bit to show that next time the targetposition is reached the move is finished
        // also change decremental deceleration to speed calculation via position difference. -> no speed jumps when arriving at target position
        if (accelerating)
        {
            positionHasBeenChanged = false;
        }

        // set the direction pin
        digitalWrite(Pin_Direction, (currentDirection == _POSITIVE));

        // to improve performance the step pins are set in the global Interrupt
        stepsThisInterrupt[AxisNumber] = stepsPerInterrupt;

        // increment or decrement the current position
        if (currentDirection == _POSITIVE)
        {
            currentPosition += stepsPerInterrupt;
        }
        else
        {
            if (movementType == _POSITIONING || movementType == _STOPPING)
            {
                if (currentPosition < stepsPerInterrupt)
                {
                    currentDirection = _STANDSTILL;
                    currentVelocity = 0;
                } //------------------------------------
                else
                {
                    currentPosition -= stepsPerInterrupt;
                }
            }
            else if (movementType == _VELOCITY)
            {
                if (currentPosition >= stepsPerInterrupt)
                {
                    currentPosition -= stepsPerInterrupt;
                }
                else
                {
                    currentPosition = 0;
                }
            }
        }

        // finish moving
        if (!positionHasBeenChanged && currentPosition == targetPosition && movementType == _POSITIONING)
        {
            currentDirection = _STANDSTILL;
            currentVelocity = 0;
        } //---------------

        if (currentVelocity > 0)
        {
            cyclesTillNextStep = F_CPU / currentVelocity;
            stepsPerInterrupt = (F_CPU / cyclesTillNextStep) / MAX_STEP_FREQUENCY + 1;
            cyclesTillNextStep = cyclesTillNextStep * stepsPerInterrupt;
            cyclesTillNextStep -= offsetCycleTillNextStep;
        }
        else
        {
            if (movementType == _STOPPING)
            {
                currentDirection = _STANDSTILL;
            }
            cyclesTillNextStep = MAX_LENGTH_INTERRUPT;
            stepsPerInterrupt = 1;
        }
    }

    unsigned long processorCylcesPerSpeedChange;
    if (accelerating)
    {
        processorCylcesPerSpeedChange = processorCylcesPerSpeedChangeAC;
    }
    else
    {
        processorCylcesPerSpeedChange = processorCylcesPerSpeedChangeDC;
    }
    // processorCylcesPerSpeedChange = processorCylcesPerSpeedChangeAC;
    processorCyclesSinceLaseSpeedUpdate += CYCLES_PER_INTERRUPT;
    speedOffsetThisCycle = processorCyclesSinceLaseSpeedUpdate / processorCylcesPerSpeedChange;
    processorCyclesSinceLaseSpeedUpdate -= speedOffsetThisCycle * processorCylcesPerSpeedChange;
    if (speedOffsetThisCycle != 0 && currentVelocity != 0)
    {
        unsigned long oldVelocity = currentVelocity;
        // calculate new speed
        if (accelerating)
        {
            bool speedIsNotMaxSpeed = currentVelocity < targetVelocity;
            currentVelocity = currentVelocity + speedOffsetThisCycle;
            if (currentVelocity > maxSpeed && movementType == _POSITIONING)
            {
                currentVelocity = maxSpeed;
            }
            if (speedIsNotMaxSpeed && currentVelocity > targetVelocity && movementType == _VELOCITY)
            {
                currentVelocity = targetVelocity;
            }
        }
        else if (decelerating)
        {
            if (currentVelocity > speedOffsetThisCycle)
            {
                currentVelocity = currentVelocity - speedOffsetThisCycle;
            }
            else
            {
                if (currentDirection != targetDirection || movementType == _STOPPING)
                {
                    currentVelocity = 0;
                    currentDirection = _STANDSTILL;
                }
                else
                {
                    currentVelocity = 1;
                }
            }
        }
        if (currentVelocity != 0)
        {
            cyclesTillNextStep = cyclesTillNextStep * oldVelocity / currentVelocity;
        }
    }
}

void Axis::disableStepping()
{
    if (SteppingIsEnabled)
    {
        SteppingIsEnabled = false;
    }
}

void Axis::enableStepping()
{
    if (!InterruptHasBeenSet)
    {
        setInterrupt();
    }
    if (!SteppingIsEnabled)
    {
        SteppingIsEnabled = true;
    }
}

void Axis::moveAbsoluteSteps(unsigned long newPosition, bool filter)
{
    newPosition = filter?smoothy->filter(newPosition):newPosition;
    newPosition = constrain(newPosition, 0 , maxPosition);
    float secondsSinceLastMoveSteps;
    if (acceleration != defaultAcceleration || deceleration != defaultDeceleration)
    {
        secondsSinceLastMoveSteps = (float)(millis() - lastMoveSteps) / 1000;
        lastMoveSteps = millis();
        if (secondsSinceLastMoveSteps > 1)
        {
            secondsSinceLastMoveSteps = 1;
        }
        unsigned long newAcceleration = acceleration + (jerk * secondsSinceLastMoveSteps);
        newAcceleration = constrain(newAcceleration, 0, defaultAcceleration);
        setTempAcceleration(newAcceleration);
        unsigned long newDeceleration = deceleration + (jerk * secondsSinceLastMoveSteps);
        newDeceleration = constrain(newDeceleration, 0, defaultDeceleration);
        setTempDeceleration(newDeceleration);
    }
    if (AxisIsHomed)
    {
        moveAbsoluteInternal(newPosition);
    }
}

void Axis::moveAbsolute(unsigned long newPosition, unsigned long acceleration, unsigned long deceleration)
{
    if (AxisIsHomed)
    {
        if (acceleration == 0)
        {
            resetAcceleration();
            resetDeceleration();
        }
        else if (deceleration == 0)
        {
            setAcceleration(acceleration, _MC_TEMPORARY);
            setDeceleration(acceleration, _MC_TEMPORARY);
        }
        else
        {
            setAcceleration(acceleration, _MC_TEMPORARY);
            setDeceleration(deceleration, _MC_TEMPORARY);
        }
        moveAbsoluteInternal(newPosition * stepsPerMillimeter);
    }
}

void Axis::moveRelative(unsigned long newPosition, bool direction)
{
    if (AxisIsHomed)
    {

        moveRelativeInternal(newPosition * stepsPerMillimeter, direction);
    }
}

void Axis::moveVelocity(unsigned long speed, bool direction)
{
    if (AxisIsHomed)
    {
        DIRECTION d;
        if (direction)
        {
            d = _POSITIVE;
        }
        else
        {
            d = _NEGATIVE;
        }
        moveVelocityInternal(speed * stepsPerMillimeter, d);
    }
}

void Axis::moveVelocityInternal(unsigned long speed, DIRECTION direction)
{
    if (!AxisIsReady())
    {
        return;
    }
    blockInterrupt = true;
    active = true;
    targetVelocity = speed;
    targetDirection = direction;
    movementType = _VELOCITY;
    blockInterrupt = false;
}

void Axis::moveAbsoluteInternal(unsigned long newPosition)
{
    if (!AxisIsReady())
    {
        return;
    }
    movementType = _POSITIONING;
    blockInterrupt = true;
    active = true;
    if (currentDirection == _STANDSTILL)
    {
        cyclesTillNextStep = 0;
        processorCyclesSinceLaseSpeedUpdate = 0;
    }
    targetPosition = newPosition;
    positionHasBeenChanged = true;
    blockInterrupt = false;
}

void Axis::moveVelocity(unsigned long targetSpeed, DIRECTION direction)
{
    movementType = _VELOCITY;
    targetVelocity = targetSpeed;
    targetDirection = direction;
}

void Axis::stop()
{
    movementType = _STOPPING;
}

void Axis::setFilterConstant(int bufferSize)
{
    smoothy->setBuffer(bufferSize);
}

unsigned int Axis::getFilterConstant()
{
    return smoothy->getBuffer();
}

