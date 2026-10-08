#ifndef AXIS_H
#define AXIS_H
#include "Arduino.h"
#include "configuration.h"
#include "smoothy.h"
#define MAX_AMOUNT_OF_AXIS 10
#define MAX_LENGTH_INTERRUPT (8388607 / 100)
#define MAX_STEP_FREQUENCY 30000
#define CYCLES_PER_INTERRUPT (F_CPU / MAX_STEP_FREQUENCY)
// #define MICROSECONDS_PER_INTERRUPT (1000000/MAX_STEP_FREQUENCY)

enum DIRECTION
{
    _POSITIVE,
    _NEGATIVE,
    _STANDSTILL
};

enum PARAMETER_MODE
{
    _MC_PERMANENT,
    _MC_TEMPORARY
};

class Axis
{
private:
    enum homingStep
    {
        waitForAxisToStop,
        clearEndstop,
        driveToEndstop,
        moveFromEndstopAfterHoming,
        waitingForEndOfMovement
    };
    enum MOVEMENT_TYPE
    {
        _POSITIONING,
        _VELOCITY,
        _STOPPING
    };
    unsigned long lastMoveSteps;
    const unsigned long jerk = 200 * STEPS_PER_MM;
    static bool SteppingIsEnabled;
    unsigned long maxPosition;
    unsigned long homingOffset;
    unsigned long defaultAcceleration;
    unsigned long defaultDeceleration;
    unsigned long defaulMaxSpeed;
    unsigned long speedWhileHoming;
    unsigned long accelerationWhileHoming;
    int EEPROMAdress;
    volatile bool active = false;
    static bool InterruptHasBeenSet;
    unsigned int AxisNumber;
    static unsigned AxisCounter;
    static bool toggle[MAX_AMOUNT_OF_AXIS];
    MOVEMENT_TYPE movementType = _POSITIONING;
    unsigned long targetVelocity;
    DIRECTION targetDirection;
    const int Pin_Step;
    const int Pin_Direction;
    const int Pin_Enable;
    const int Pin_Endstop;
    const int Pin_Trouble;
    volatile bool homingActive = false;
    bool AxisIsLocked = false;
    unsigned long stepsPerMillimeter = STEPS_PER_MM;
    bool isInitialized = false;
    volatile bool AxisHasError = false;
#ifdef HOMING_REQUIRED
    volatile bool AxisIsHomed = false;
#else
    volatile bool AxisIsHomed = true;
#endif
    unsigned short ErrorID = 0;
    static volatile unsigned interruptCounter;
    static unsigned interruptCounterLastTime;
    bool HardwareHasBeenInitialized = false;
    static int stepPins[MAX_AMOUNT_OF_AXIS];
    static unsigned short stepsThisInterrupt[MAX_AMOUNT_OF_AXIS];

    Smoothy* smoothy;
    void executeHoming();
    void loadDefaultValues();
    void writeEEPROM(unsigned long);
    void writeEEPROM(unsigned int);
    void readEEPROM(unsigned long &);
    void readEEPROM(unsigned int &);
    void initializeHardware();
    static bool globalInterrupt(void *);
    void init();
    void _move();
    void setTempAcceleration(unsigned long);
    void setTempDeceleration(unsigned long);
    void setTempACC_DCC(unsigned long);
    void resetAcceleration();
    void resetDeceleration();
    void setTempSpeed(unsigned long);
    void resetSpeed();
    void moveRelativeInternal(unsigned long, bool);
    void moveVelocityInternal(unsigned long, DIRECTION);
    void moveAbsoluteInternal(unsigned long);
    static void setInterrupt();
    void readData();
    void verifyData();
    void calculateValues();

public:
    Axis(int, int, int, int, int);
    void loadEEPROM();
    static void disableStepping();
    static void enableStepping();
    static bool interruptActive();
    void saveData();
    void moveAbsoluteSteps(unsigned long, bool);
    bool AxisIsReady();
    static bool digitalReadAverage(int, int averageingNumber = 10);
    bool getEndstopState();
    static void (*ExecutePointer[10])();

    void moveVelocity(unsigned long, DIRECTION);
    void stop();
    void execute();
    void lock();
    void home();
    void unlock();
    bool hasError();
    void resetAxis();
    unsigned getErrorID();
    static bool steppingIsEnabled();

    volatile unsigned long currentVelocity;
    volatile unsigned long acceleration = 1;
    volatile unsigned long deceleration = 1;
    volatile unsigned long currentPosition;
    volatile unsigned long targetPosition;
    volatile unsigned long cyclesTillNextStep = MAX_LENGTH_INTERRUPT;
    // volatile unsigned long TimerPeriod;
    volatile bool accelerating;
    volatile bool decelerating;
    volatile unsigned long speedOffsetThisCycle;
    volatile unsigned long processorCyclesSinceLaseSpeedUpdate;
    volatile unsigned long stepsPerInterrupt = 1;
    volatile unsigned long processorCylcesPerSpeedChangeAC = F_CPU / acceleration;
    volatile unsigned long processorCylcesPerSpeedChangeDC = F_CPU / acceleration;
    volatile unsigned long maxSpeed;
    volatile bool positionHasBeenChanged;
    volatile bool blockInterrupt = false;
    volatile DIRECTION currentDirection = _STANDSTILL;

    void setAcceleration(unsigned long, PARAMETER_MODE parameterMode = _MC_PERMANENT);
    void setSpeed(unsigned long);
    void setMaxPosition(unsigned long);
    void setHomingOffset(unsigned long);
    void setHomingSpeed(unsigned long);
    void setHomingAcceleration(unsigned long);
    void setStepsPerMillimeter(unsigned long);
    void setFilterConstant(int);

    bool isRunningMaxSpeed();
    bool isActive();
    bool isHomed();
    bool isDeccelerating();
    void moveAbsolute(unsigned long, unsigned long acceleration = 0, unsigned long deceleration = 0);
    void moveRelative(unsigned long, bool);
    void moveVelocity(unsigned long, bool);
    unsigned long getCurrentPosition();
    unsigned long getCommandPosition();
    unsigned long getSpeed();
    unsigned long getMaxPosition();
    unsigned long getHomingOffset();
    unsigned long getAcceleration();
    unsigned long getMaxSpeed();
    unsigned long getHomingSpeed();
    unsigned long getHomingAcceleration();
    static float getWorkload();
    unsigned int getFilterConstant();
    bool getDriveState();
    volatile homingStep homingStep = waitForAxisToStop;

    void setDeceleration(unsigned long, PARAMETER_MODE parameterMode = _MC_PERMANENT);
    unsigned long getDeceleration();
};

#endif