#include "commandList.h"

#define PROTOCOL_LENGTH 8
#define TIMEOUT 300000
unsigned long int cyclesSinceBufferWasEmpty;
unsigned long int cyclesSinceBufferWasFull;
CMD request = IDLE;
byte buffer[PROTOCOL_LENGTH];
bool toggle;
bool waitingForOKAY = false;

void clearBuffer(bool);
unsigned long int TwoBytesToSteps(byte, byte, unsigned long int);
bool commandFound(byte);
bool verifyData();
void setPosition();
void readNewCommand();
void unsignedLongToTwoBytes(unsigned long int, unsigned long int, byte*, byte*);
void sendBuffer();
void sendAnswer();

void executeCommunication()
{   
    if (waitingForOKAY & (Serial.available() >= 1))
    {
        waitingForOKAY = false;
        if (Serial.read() == 255)
        {
            clearBuffer(true);
            request = IDLE;
        }
        else
        {
            while (Serial.available() > 0)
            {
                Serial.read();
            }
        }
    }

    if (!waitingForOKAY & (request != IDLE))
    {   
        sendAnswer();
    }

    if (!waitingForOKAY & (Serial.available() == PROTOCOL_LENGTH) & (request == IDLE))
    {
        readNewCommand();
    }

    if (Serial.available() != 0)
    {
        cyclesSinceBufferWasEmpty++;
        cyclesSinceBufferWasFull = 0;
    }
    else
    {
        cyclesSinceBufferWasFull++;
        cyclesSinceBufferWasEmpty = 0;
    }
    
    if (cyclesSinceBufferWasEmpty == TIMEOUT)
    {
        //Serial.write(Serial.available());
        if (Serial.available() > PROTOCOL_LENGTH)
        {
            digitalWrite(beeperPin, HIGH);
            delay(100);
            digitalWrite(beeperPin, LOW);
        }
        else
        {
            digitalWrite(beeperPin, HIGH);
            delay(100);
            digitalWrite(beeperPin, LOW);
            delay(100);
            digitalWrite(beeperPin, HIGH);
            delay(100);
            digitalWrite(beeperPin, LOW);
        }
        clearBuffer(false);
    }

    if (cyclesSinceBufferWasFull == TIMEOUT)
    {
        if (waitingForOKAY){
            // sendBuffer();
            cyclesSinceBufferWasFull = 0;
        }else{
            digitalWrite(beeperPin, HIGH);
            delay(100);
            digitalWrite(beeperPin, LOW);
            // clearBuffer(Serial.available() == 0);
        }
        
    }
}


void readNewCommand()
{
    int x = 0;
    while (x < PROTOCOL_LENGTH)
    {
        buffer[x] = Serial.read();
        x++;
    }

    if (!verifyData())
    {
        clearBuffer(false);
        return;
    }

    bool successfulExecuted = true;
    CMD command = (CMD)(buffer[0] / 2);
    bool reading = (bool)(buffer[0] % 2);
    switch (command)
    {
    case POSITION:
        if (reading)
        {
            request = command;
        }
        else
        {
            newCommand.X_Position = TwoBytesToSteps(buffer[1], buffer[2], X_Axis.MaxPosition);
            newCommand.Y_Position = TwoBytesToSteps(buffer[3], buffer[4], Y_Axis.MaxPosition);
            newCommand.Z_Position = TwoBytesToSteps(buffer[5], buffer[6], Z_Axis.MaxPosition);
            newCommandPositionAvailable = true;
        }
        break;
    
    case HOMING_OFFSET:
        if (reading)
        {
            request = command;
        }else{
            X_Axis.HomingOffset = TwoBytesToSteps(buffer[1], buffer[2], 0);
            Y_Axis.HomingOffset = TwoBytesToSteps(buffer[3], buffer[4], 0);
            Z_Axis.HomingOffset = TwoBytesToSteps(buffer[5], buffer[6], 0);
            requestHome = true;
        }
        break;

    case MAX_POSITION:
        if (reading)
        {
            request = command;
        }else{
            X_Axis.MaxPosition = TwoBytesToSteps(buffer[1], buffer[2], 0) * stepsPerMillimeter;
            Y_Axis.MaxPosition = TwoBytesToSteps(buffer[3], buffer[4], 0) * stepsPerMillimeter;
            Z_Axis.MaxPosition = TwoBytesToSteps(buffer[5], buffer[6], 0) * stepsPerMillimeter;
            requestHome = true;
        }
        break;

    default:
        successfulExecuted = false;
        break;
    }

    if (successfulExecuted){
        if (reading){
            sendAnswer();
        }else{
            clearBuffer(true);
        }
    }else{
        clearBuffer(false);
    }
}


void sendAnswer(){
    memset(buffer, 0, PROTOCOL_LENGTH);
    buffer[0] = int(request) * 2;
    switch (request)
    {
    case POSITION:
        unsignedLongToTwoBytes(X_Axis.istPosition, X_Axis.MaxPosition, &buffer[1], &buffer[2]);
        unsignedLongToTwoBytes(Y_Axis.istPosition, Y_Axis.MaxPosition, &buffer[3], &buffer[4]);
        unsignedLongToTwoBytes(Z_Axis.istPosition, Z_Axis.MaxPosition, &buffer[5], &buffer[6]);
        break;

    case HOMING_OFFSET:
        unsignedLongToTwoBytes(X_Axis.HomingOffset, 0, &buffer[1], &buffer[2]);
        unsignedLongToTwoBytes(Y_Axis.HomingOffset, 0, &buffer[3], &buffer[4]);
        unsignedLongToTwoBytes(Z_Axis.HomingOffset, 0, &buffer[5], &buffer[6]);
        break;

    case MAX_POSITION:
        unsignedLongToTwoBytes(X_Axis.MaxPosition / stepsPerMillimeter, 0, &buffer[1], &buffer[2]);
        unsignedLongToTwoBytes(Y_Axis.MaxPosition / stepsPerMillimeter, 0, &buffer[3], &buffer[4]);
        unsignedLongToTwoBytes(Z_Axis.MaxPosition / stepsPerMillimeter, 0, &buffer[5], &buffer[6]);
        break;

    case ACCELLERATION:
        unsignedLongToTwoBytes(X_Axis.acceleration, stepsPerMillimeter, &buffer[1], &buffer[2]);
        unsignedLongToTwoBytes(Y_Axis.acceleration, stepsPerMillimeter, &buffer[3], &buffer[4]);
        unsignedLongToTwoBytes(Z_Axis.acceleration, stepsPerMillimeter, &buffer[5], &buffer[6]);
        break;

    case MAX_SPEED:
        unsignedLongToTwoBytes(X_Axis.maxSpeed, stepsPerMillimeter, &buffer[1], &buffer[2]);
        unsignedLongToTwoBytes(Y_Axis.maxSpeed, stepsPerMillimeter, &buffer[3], &buffer[4]);
        unsignedLongToTwoBytes(Z_Axis.maxSpeed, stepsPerMillimeter, &buffer[5], &buffer[6]);
        break;

    case HOMING_STATUS:
        buffer[1] = requestHome;
        break;

    case STEPS_PER_MILLIMETER:
        unsignedLongToTwoBytes(stepsPerMillimeter, 0, &buffer[1], &buffer[2]);
        break;

    default:
        request = IDLE;
        break;
    }
    if (request != IDLE){
        sendBuffer();
        waitingForOKAY = true;
    }
}


void clearBuffer(bool okay)
{
    while (Serial.available() > 0)
    {
        Serial.read();
    }
    cyclesSinceBufferWasEmpty = 0;
    cyclesSinceBufferWasFull = 0;
    if (okay)
    {
        Serial.write(255);
        Serial.flush();
    }
    else
    {
        Serial.write(254);
        Serial.flush();
    }
}

unsigned long int TwoBytesToSteps(byte Byte1, byte Byte2, unsigned long int maxPosition)
{
    unsigned long int ValueTwoBytes = Byte1;
    ValueTwoBytes = ValueTwoBytes * 256;
    ValueTwoBytes = ValueTwoBytes + Byte2;
    if (maxPosition != 0){
        ValueTwoBytes = ValueTwoBytes * (maxPosition - 1);
        ValueTwoBytes = ValueTwoBytes / 65535;
        if (ValueTwoBytes > maxPosition - 1)
        {
            ValueTwoBytes = maxPosition - 1;
        }
    }
    return ValueTwoBytes;
}

void unsignedLongToTwoBytes(unsigned long int Value, unsigned long int MaxValue, byte* Byte1, byte* Byte2){
    double ValueScaled;
    if (MaxValue == 0){
        ValueScaled = Value;
    }else{
        ValueScaled = Value * 65535 / MaxValue;
    }
    unsigned int ValueScaledINT = (unsigned int) (ValueScaled);
    *Byte1 = ValueScaledINT / 256;
    *Byte2 = ValueScaledINT;
}


bool verifyData()
{
    byte veryfyingResult = 0;
    int x;
    for (x = 0; x != PROTOCOL_LENGTH -1; ++x)
    {
        veryfyingResult = veryfyingResult xor buffer[x];
    }
    if (veryfyingResult == buffer[PROTOCOL_LENGTH - 1])
    {
        return true;
    }
    else
    {
        return false;
    }
}


void sendBuffer(){
    buffer[PROTOCOL_LENGTH-1] = 0;
    int x;
    for (x = 0; x != PROTOCOL_LENGTH -1; ++x)
    {
        buffer[PROTOCOL_LENGTH-1] = buffer[PROTOCOL_LENGTH-1] xor buffer[x];
    }

    for (x = 0; x != PROTOCOL_LENGTH; ++x)
    {
        // Serial.write(buffer[x]);
        Serial.flush();
    }
}


