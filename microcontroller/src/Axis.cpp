#include "Arduino.h"
#include "Axis.h"
#include "AxisEEPROM.h"
#include "AxisConstructors.h"
#include "AxisMove.h"
#include "configuration.h"
#include "AxisHome.h"
#ifdef AUTO_SAVE
#define SAVE_DATA saveData();
#else
#define SAVE_DATA // NOTHING
#endif


bool Axis::InterruptHasBeenSet = false;
bool Axis::SteppingIsEnabled = false;
unsigned Axis::AxisCounter = 0;
volatile unsigned Axis::interruptCounter = 0;
unsigned Axis::interruptCounterLastTime = 0;
void (*Axis::ExecutePointer[10])() = {0};
int Axis::stepPins[MAX_AMOUNT_OF_AXIS] = {0};
unsigned short Axis::stepsThisInterrupt[MAX_AMOUNT_OF_AXIS] = {0};
bool Axis::toggle[MAX_AMOUNT_OF_AXIS] = {0};

void Axis::lock()
{
    if (!HardwareHasBeenInitialized)
    {
        initializeHardware();
    }
    digitalWrite(Pin_Enable, LOW);
    AxisIsLocked = true;
}

void Axis::unlock()
{
    digitalWrite(Pin_Enable, HIGH);
    active = false;
    currentDirection = _STANDSTILL;
    currentVelocity = 0;
    AxisIsHomed = false;
    AxisIsLocked = false;
}

void Axis::calculateValues()
{
    processorCylcesPerSpeedChangeAC = F_CPU / acceleration;
    processorCylcesPerSpeedChangeDC = F_CPU / deceleration;
}
void Axis::setStepsPerMillimeter(unsigned long steps)
{
    stepsPerMillimeter = steps;
}

void Axis::setTempAcceleration(unsigned long acceleration)
{
    this->acceleration = acceleration;
    calculateValues();
}

void Axis::setTempDeceleration(unsigned long deceleration)
{
    this->deceleration = deceleration;
    calculateValues();
}

void Axis::setTempACC_DCC(unsigned long ACC_DCC)
{
    setTempAcceleration(ACC_DCC);
    setTempDeceleration(ACC_DCC);
}

void Axis::resetAcceleration()
{
    if (acceleration == defaultAcceleration)
    {
        return;
    }
    acceleration = defaultAcceleration;
    calculateValues();
}

void Axis::resetDeceleration()
{
    if (deceleration == defaultDeceleration)
    {
        return;
    }
    deceleration = defaultDeceleration;
    calculateValues();
}

void Axis::setAcceleration(unsigned long acceleration, PARAMETER_MODE parameterMode)
{
    if (parameterMode == _MC_PERMANENT)
    {
        this->acceleration = acceleration * stepsPerMillimeter;
        this->defaultAcceleration = this->acceleration;
        calculateValues();
        SAVE_DATA
    }
    else
    {
        setTempAcceleration(acceleration * stepsPerMillimeter);
    }
}

void Axis::setDeceleration(unsigned long deceleration, PARAMETER_MODE parameterMode)
{
    if (parameterMode == _MC_PERMANENT)
    {
        this->deceleration = deceleration * stepsPerMillimeter;
        this->defaultDeceleration = this->deceleration;
        calculateValues();
        SAVE_DATA
    }
    else
    {
        setTempDeceleration(deceleration * stepsPerMillimeter);
    }
}

void Axis::setHomingSpeed(unsigned long speed)
{
    this->speedWhileHoming = speed * stepsPerMillimeter;
    SAVE_DATA
}

void Axis::setHomingAcceleration(unsigned long acceleration)
{
    this->accelerationWhileHoming = acceleration * stepsPerMillimeter;
    SAVE_DATA
}

void Axis::setTempSpeed(unsigned long speed)
{
    this->maxSpeed = speed;
}

void Axis::resetSpeed()
{
    if (maxSpeed == defaulMaxSpeed)
    {
        return;
    }
    maxSpeed = defaulMaxSpeed;
}

void Axis::setSpeed(unsigned long maxSpeed)
{
    this->maxSpeed = maxSpeed * stepsPerMillimeter;
    this->defaulMaxSpeed = this->maxSpeed;
    SAVE_DATA
}

bool Axis::isHomed() SAVE_DATA
{
    return AxisIsHomed;
}

void Axis::setMaxPosition(unsigned long MaxPosition)
{
    unsigned long maxPosition = MaxPosition * stepsPerMillimeter;
    if (maxPosition == 0)
    {
        maxPosition = stepsPerMillimeter;
    }
    if (this->maxPosition != maxPosition)
    {
        unsigned long long newPosition = (unsigned long long)currentPosition * maxPosition / this->maxPosition;
        this->maxPosition = maxPosition;

        if (isInitialized)
        {
            SAVE_DATA
            if (AxisIsHomed)
            {
                moveAbsoluteInternal(newPosition);
            }
        }
    }
}

void Axis::setHomingOffset(unsigned long offset)
{
    offset = offset * stepsPerMillimeter;
    if (offset != homingOffset)
    {
        homingOffset = offset;
        SAVE_DATA
        home();
    }
}

bool Axis::hasError()
{
    if (!HardwareHasBeenInitialized)
    {
        return false;
    }
    if (AxisHasError)
    {
        return true;
    }
    unsigned long int timeStamp;
    const unsigned long int timeTillError = 50; // time delay in milliseconds to prevent errors due to noise
#ifndef IGNORE_ENDSTOP
    if (AxisIsHomed && !digitalRead(Pin_Endstop))
    {
        timeStamp = millis();
        while (!digitalRead(Pin_Endstop))
        {
            if (millis() - timeStamp >= timeTillError)
            {
                ErrorID = 3;
                AxisHasError = true;
                return true;
            }
        }
    }
#endif
#ifndef IGNORE_DRIVE_ERRORS
    if (!digitalRead(Pin_Trouble))
    {
        timeStamp = millis();
        while (!digitalRead(Pin_Trouble))
        {
            if (millis() - timeStamp >= timeTillError)
            {
                ErrorID = 4;
                AxisHasError = true;
                return true;
            }
        }
    }
#endif
    return false;
}

void Axis::resetAxis()
{
    AxisHasError = false;
}

unsigned Axis::getErrorID()
{
    return ErrorID;
}

bool Axis::steppingIsEnabled()
{
    return SteppingIsEnabled;
}

bool Axis::AxisIsReady()
{
    if (!HardwareHasBeenInitialized)
    {
        initializeHardware();
    }
    if (!SteppingIsEnabled)
    {
        return false;
    }
    if (!AxisIsLocked)
    {
        return false;
    }
    return true;
}

bool Axis::digitalReadAverage(int pin, int averageingNumber)
{
    // read a digital pin and return the average result to prevent action due to noise
    int y = 0;
    int x = 0;
    for (x = 0; x < averageingNumber; x++)
    {
        if (digitalRead(pin))
        {
            y++;
        }
    }
    if (y > averageingNumber / 2)
    {
        return true;
    }
    else
    {
        return false;
    }
}

bool Axis::getEndstopState()
{
    return !digitalRead(Pin_Endstop);
}

bool Axis::getDriveState()
{
    return digitalRead(Pin_Trouble);
}

unsigned long Axis::getCurrentPosition()
{
    return currentPosition / stepsPerMillimeter;
}

unsigned long Axis::getCommandPosition()
{
    return targetPosition / stepsPerMillimeter;
}

unsigned long Axis::getSpeed()
{
    return currentVelocity / stepsPerMillimeter;
}

unsigned long Axis::getMaxPosition()
{
    return maxPosition / stepsPerMillimeter;
}

unsigned long Axis::getHomingOffset()
{
    return homingOffset / stepsPerMillimeter;
}

unsigned long Axis::getAcceleration()
{
    return defaultAcceleration / stepsPerMillimeter;
}

unsigned long Axis::getDeceleration()
{
    return defaultDeceleration / stepsPerMillimeter;
}


unsigned long Axis::getMaxSpeed()
{
    return defaulMaxSpeed / stepsPerMillimeter;
}

unsigned long Axis::getHomingSpeed()
{
    return speedWhileHoming / stepsPerMillimeter;
}

unsigned long Axis::getHomingAcceleration()
{
    return accelerationWhileHoming / stepsPerMillimeter;
}

float Axis::getWorkload()
{
    return 0;
}

bool Axis::interruptActive()
{
    if (interruptCounter != interruptCounterLastTime)
    {
        interruptCounterLastTime = interruptCounter;
        return true;
    }
    else
    {
        return false;
    }
}

bool Axis::isActive()
{
    return active;
}
