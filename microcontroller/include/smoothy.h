#include <Arduino.h>
#ifndef SMOOTHY_IS_DEFINED
#define SMOOTHY_IS_DEFINED
class Smoothy
{
private:
  float *buffer = NULL;
  int bufferSize;
  int newestValue;

public:
  float filter(float);
  Smoothy(int bufferSize);
  ~Smoothy();
  void setBuffer(unsigned int);
  unsigned int getBuffer();
};
#endif