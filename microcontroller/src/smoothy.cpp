#include "Arduino.h"
#include "smoothy.h"
Smoothy::Smoothy(int bufferSize)
{
    setBuffer(bufferSize);
}

Smoothy::~Smoothy()
{
    delete[] this->buffer;
}

float Smoothy::filter(float input)
{
    newestValue = (newestValue + 1) % bufferSize;
    buffer[newestValue] = input;
    float temp = 0;
    for (int i = 0; i < bufferSize; i++)
    {
        int z = newestValue - i;
        if (z < 0)
        {
            z += bufferSize;
        }
        float multiplicator = 1.0 / bufferSize;
        float weight = multiplicator / trunc(bufferSize / 2);
        temp += buffer[z] * (multiplicator + ((i - bufferSize / 2) * -weight));
    }
    return temp;
}

void Smoothy::setBuffer(unsigned int bufferSize)
{
    bufferSize = constrain(bufferSize, 3, 99);
    if (bufferSize % 2 == 0)
    {
        bufferSize += 1;
    }
    float lastValueOfBuffer = 0;
    if (this->buffer != NULL)
    {
        lastValueOfBuffer = buffer[newestValue];
        delete[] this->buffer;
    }
    this->bufferSize = bufferSize;
    this->buffer = new float[bufferSize];
    for (int i = 0; i < bufferSize-1; i++)
    {
        buffer[i] = lastValueOfBuffer;
    }
    newestValue = 0;
}

unsigned int Smoothy::getBuffer()
{
    return this->bufferSize;
}