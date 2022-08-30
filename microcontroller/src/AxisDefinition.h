struct PinConfig{
    const int Direction;
    const int Enable;
    const int Trouble;
    const int InPosition;
    const int Endstop;
};

struct Achse{
    struct PinConfig Pin;
    unsigned long int MaxPosition;
    const unsigned long int acceleration;
    const unsigned long int accelerationPerAccelerationRecalculation;
    const unsigned long int decceleration;
    const unsigned long int maxSpeed;
    const unsigned long int DistanzAbbremsenVonMaxSpeed;
    unsigned int HomingOffset;

    volatile bool aktiv;
    bool accelerating;
    bool deccelerating;
    bool homingAbgeschlossen;
    bool changeOfDirection;
    volatile bool runningMinSpeed;
    unsigned long int currentSpeed;

    unsigned long int posStartDeccelerating;
    unsigned long int sollPositionNachRichtungswechsel;
    volatile unsigned long int istPosition;
    unsigned long int sollPosition;

    bool currentDirection;
    const unsigned int RundungsFehlerProAccellerationCalculation;
    unsigned int RundungsfehlerSumiert;
    unsigned long int CyclesSinceLastAccelerationCalculation;

    volatile uint16_t *TimerPeriod;
    volatile uint8_t *Port;
    volatile uint8_t *OutputRegister;
    const unsigned int StepPinNumber;

    bool toggle;
    unsigned int stepPeriodInProcessorCycles;
    const unsigned short int AxisNomber;
    
};

struct Achse X_Axis{    27,      //Direction
                        28,      //Enable
                        30,      //Trouble
                        29,      //InPosition
                        32,      //Endstop

                        210*stepsPerMillimeter,      //MaxPosition
                        GlobalAcceleration*stepsPerMillimeter,      //acceleration
                        GlobalAcceleration*stepsPerMillimeter/ProcessorCyclesPerMicrosecond*accelerationRecalculationPeriod/1000000,//accelerationPerAccelerationRecalculation
                        100*stepsPerMillimeter,      //decceleration
                        400*stepsPerMillimeter,      //maxSpeed
                        (X_Axis.maxSpeed * X_Axis.maxSpeed) / (2*X_Axis.acceleration),//DistanzAbbremsenVonMaxSpeed
                        .HomingOffset = 20,

                        0,      //aktiv
                        0,      //accelerating
                        0,      //deccelerating
                        0,      //homingAbgeschlossen
                        0,      //changeOfDirection
                        0,      //runningMinSpeed
                        0,      //currentSpeed

                        0,      //posStartDeccelerating
                        0,      //posStartDeccelerating_nextMove
                        105*stepsPerMillimeter,      //istPosition
                        0,      //sollPosition

                        1,      //currentDirection
                        1,      //RundungsFehlerProAccellerationCalculation
                        0,      //RundungsfehlerSumiert
                        0,      //microsSinceLastAccelerationCalculation

                        &OCR3A, //TimerPeriod
                        &PORTA, //Port
                        &DDRA,  //OutpuRegister
                        PA4,    //StepPinNumber

                        0,      //toggle
                        65535,      //stepPeriodInProcessorCycles
                        0       //AxisNomber
                        };

struct Achse Y_Axis{    15,      //Direction
                        16,      //Enable
                        18,      //Trouble
                        17,      //InPosition
                        19,      //Endstop

                        210*stepsPerMillimeter,      //MaxPosition
                        GlobalAcceleration*stepsPerMillimeter,      //acceleration
                        GlobalAcceleration*stepsPerMillimeter/ProcessorCyclesPerMicrosecond*accelerationRecalculationPeriod/1000000,//accelerationPerAccelerationRecalculation
                        100*stepsPerMillimeter,      //decceleration
                        400*stepsPerMillimeter,      //maxSpeed
                        (Y_Axis.maxSpeed * Y_Axis.maxSpeed) / (2*Y_Axis.acceleration),//DistanzAbbremsenVonMaxSpeed
                        .HomingOffset = 20,

                        0,      //aktiv
                        0,      //accelerating
                        0,      //deccelerating
                        0,      //homingAbgeschlossen
                        0,      //changeOfDirection
                        0,      //runningMinSpeed
                        0,      //currentSpeed

                        0,      //posStartDeccelerating
                        0,      //posStartDeccelerating_nextMove
                        0,      //istPosition
                        0,      //sollPosition

                        1,      //currentDirection
                        1,      //RundungsFehlerProAccellerationCalculation
                        0,      //RundungsfehlerSumiert
                        0,      //microsSinceLastAccelerationCalculation

                        &OCR4A, //TimerPeriod
                        &PORTJ, //Port
                        &DDRJ,  //OutpuRegister
                        PJ1,    //StepPinNumber

                        0,      //toggle
                        65535,      //stepPeriodInProcessorCycles
                        1       //AxisNomber
                        };

struct Achse Z_Axis{    3,      //Direction
                        4,      //Enable
                        6,      //Trouble
                        5,      //InPosition
                        7,      //Endstop

                        200*stepsPerMillimeter,      //MaxPosition
                        GlobalAcceleration*stepsPerMillimeter,      //acceleration
                        GlobalAcceleration*stepsPerMillimeter/ProcessorCyclesPerMicrosecond*accelerationRecalculationPeriod/1000000,//accelerationPerAccelerationRecalculation
                        100*stepsPerMillimeter,      //decceleration
                        400*stepsPerMillimeter,      //maxSpeed
                        (Z_Axis.maxSpeed * Z_Axis.maxSpeed) / (2*Z_Axis.acceleration),//DistanzAbbremsenVonMaxSpeed
                        .HomingOffset = 20,

                        0,      //aktiv
                        0,      //accelerating
                        0,      //deccelerating
                        0,      //homingAbgeschlossen
                        0,      //changeOfDirection
                        0,      //runningMinSpeed
                        0,      //currentSpeed

                        0,      //posStartDeccelerating
                        0,      //posStartDeccelerating_nextMove
                        0,      //istPosition
                        0,      //sollPosition

                        1,      //currentDirection
                        1,      //RundungsFehlerProAccellerationCalculation
                        0,      //RundungsfehlerSumiert
                        0,      //microsSinceLastAccelerationCalculation

                        &OCR5A, //TimerPeriod
                        &PORTE, //Port
                        &DDRE,  //OutpuRegister
                        PE4,    //StepPinNumber

                        0,      //toggle
                        65535,      //stepPeriodInProcessorCycles
                        2       //AxisNomber
                        };


struct Achse *Axis;

void initializeAxis(struct Achse Axis){
    pinMode(Axis.Pin.Direction,OUTPUT);
    pinMode(Axis.Pin.Enable,OUTPUT);
    pinMode(Axis.Pin.InPosition,INPUT_PULLUP);
    pinMode(Axis.Pin.Trouble,INPUT_PULLUP);
    pinMode(Axis.Pin.Endstop,INPUT_PULLUP);

    digitalWrite(Axis.Pin.Direction,LOW);
    digitalWrite(Axis.Pin.Enable,LOW);
    uint8_t AktuellerWertPort = *Axis.OutputRegister;
    *Axis.OutputRegister = AktuellerWertPort |(1<<Axis.StepPinNumber);
}

struct Achse *getAxis(unsigned short int AxisNomber){
    if (AxisNomber == 0){
        return &X_Axis;
    }else if(AxisNomber == 1){
        return &Y_Axis;
    }else{
        return &Z_Axis;
    }
}