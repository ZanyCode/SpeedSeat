#include "Arduino.h"
#include "Axis.h"
#include "configuration.h"

Axis::Axis(const int Pin_Step, const int Pin_Direction, const int Pin_Enable, const int Pin_Endstop, const int Pin_Trouble)
    : Pin_Step(Pin_Step),
      Pin_Direction(Pin_Direction),
      Pin_Enable(Pin_Enable),
      Pin_Endstop(Pin_Endstop),
      Pin_Trouble(Pin_Trouble)
{
    smoothy = new Smoothy(19);
    init();
}

void Axis::init()
{
    AxisNumber = AxisCounter;
    AxisCounter++;
    loadDefaultValues();
    verifyData();
    isInitialized = true;
    stepPins[AxisNumber] = Pin_Step;
}

void Axis::initializeHardware()
{
    if (HardwareHasBeenInitialized)
    {
        return;
    }
    pinMode(Pin_Step, OUTPUT);
    pinMode(Pin_Direction, OUTPUT);
    pinMode(Pin_Enable, OUTPUT);
    pinMode(Pin_Endstop, INPUT_PULLUP);
    pinMode(Pin_Trouble, INPUT_PULLUP);
    // lock();
    if (!InterruptHasBeenSet)
    {
        setInterrupt();
    }
    HardwareHasBeenInitialized = true;
}

void Axis::loadDefaultValues()
{
    defaulMaxSpeed = 350ul * STEPS_PER_MM;
    defaultAcceleration = 1000ul * STEPS_PER_MM;
    defaultDeceleration = 1000ul * STEPS_PER_MM;
    speedWhileHoming = 25ul * STEPS_PER_MM;
    accelerationWhileHoming = 1000ul * STEPS_PER_MM;
    maxPosition = 299 * STEPS_PER_MM;
    homingOffset = 40 * STEPS_PER_MM;
}

void Axis::verifyData()
{
    maxPosition = constrain(maxPosition, 20* STEPS_PER_MM, 300 * STEPS_PER_MM);
    homingOffset = constrain(homingOffset, 1* STEPS_PER_MM, 100 * STEPS_PER_MM);
    defaultAcceleration = constrain(defaultAcceleration, 20* STEPS_PER_MM, 1500 * STEPS_PER_MM);
    defaultDeceleration = constrain(defaultDeceleration, 20* STEPS_PER_MM, 1500 * STEPS_PER_MM);
    defaulMaxSpeed = constrain(defaulMaxSpeed, 20* STEPS_PER_MM, 600 * STEPS_PER_MM);
    speedWhileHoming = constrain(speedWhileHoming, 5* STEPS_PER_MM, 50 * STEPS_PER_MM);
    accelerationWhileHoming = constrain(accelerationWhileHoming, 20* STEPS_PER_MM, 1500 * STEPS_PER_MM);
    
    acceleration = defaultAcceleration;
    deceleration = defaultDeceleration;
    maxSpeed = defaulMaxSpeed;
    calculateValues();
}
