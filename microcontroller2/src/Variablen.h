struct dt_newCommand{
    unsigned long int X_Position;
    unsigned long int Y_Position;
    unsigned long int Z_Position;
} newCommand;

#define EXECUTE_COMMUICATION true
#define NO_HARDWARE true
#define DEBUG_COMMUNICATION true

const bool simulation = true;
const bool analyzeInputs = false;
const bool calculateAccelerationViaInterrupt = false;
const bool allowCommadsWhenAxisIsAktiv = true;
unsigned long int GlobalAcceleration = 600;
const int enablePin = 51;
const int beeperPin= LED_BUILTIN;//53;
const bool mixAxis = false;
bool requestHome = false;
bool newCommandPositionAvailable = false;



const unsigned long stepsPerMillimeter = 40;
const unsigned long int ProcessorCyclesPerMicrosecond = 16;
const unsigned long int accelerationRecalculationPeriod = 4000 * ProcessorCyclesPerMicrosecond; //16 = 1 Microsekunde bei einem 16 Mhz Prozessor
unsigned long int millisLastCycle;
bool stoppingToChangeDirection;
bool killCalled;
unsigned int ErrorNumber;
enum movementType{movementFromZero, movementWithChangeOfDirection, movementExtension};

unsigned long int getSteps(unsigned long int PositionInMillimeter){
    return PositionInMillimeter * stepsPerMillimeter;
}

unsigned long int getMillimeter(unsigned long int PositionInSteps){
    return PositionInSteps / stepsPerMillimeter;
}

