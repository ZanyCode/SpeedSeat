#include "Axis.h"
#include <EEPROM.h>

void Axis::loadEEPROM()
{
    readData();
    verifyData();
    saveData();
}
void Axis::saveData()
{
    EEPROMAdress = AxisNumber * 100;

    writeEEPROM(maxPosition / STEPS_PER_MM);
    writeEEPROM(homingOffset / STEPS_PER_MM);
    writeEEPROM(defaultAcceleration / STEPS_PER_MM);
    writeEEPROM(defaultDeceleration / STEPS_PER_MM);
    writeEEPROM(defaulMaxSpeed / STEPS_PER_MM);
    writeEEPROM(speedWhileHoming / STEPS_PER_MM);
    writeEEPROM(accelerationWhileHoming / STEPS_PER_MM);
    writeEEPROM(smoothy->getBuffer());
    EEPROM.commit();
}

void Axis::readData()
{
    EEPROMAdress = AxisNumber * 100;

    readEEPROM(maxPosition);
    readEEPROM(homingOffset);
    readEEPROM(defaultAcceleration);
    readEEPROM(defaultDeceleration);
    readEEPROM(defaulMaxSpeed);
    readEEPROM(speedWhileHoming);
    readEEPROM(accelerationWhileHoming);
    unsigned int bufferSize;
    readEEPROM(bufferSize);
    smoothy->setBuffer(bufferSize);

    calculateValues();
}

void Axis::writeEEPROM(unsigned long data)
{
    EEPROM.put(EEPROMAdress, data);
    EEPROMAdress += sizeof(data);
}

void Axis::writeEEPROM(unsigned int data)
{
    EEPROM.put(EEPROMAdress, data);
    EEPROMAdress += sizeof(data);
}

void Axis::readEEPROM(unsigned long &data)
{
    unsigned long d;
    EEPROM.get(EEPROMAdress, d);
    EEPROMAdress += sizeof(data);
    if (d == 0xFFFFFFFF)
    {
        return;
    }
    data = d * STEPS_PER_MM;
}

void Axis::readEEPROM(unsigned int &data)
{
    unsigned int d;
    EEPROM.get(EEPROMAdress, d);
    EEPROMAdress += sizeof(data);
    if (d == 0xFFFF)
    {
        return;
    }
    data = d;// * STEPS_PER_MM;
}