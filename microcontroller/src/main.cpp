#include "Arduino.h"
#include "configuration.h"
#include "Axis.h"
#include "communication.h"
#include "EEPROM.h"
#include "Beeping.h"
#include "compileChecker.h"

#ifdef DEBUG
void printPosition(int i)
{
  Serial.print("POSITION: ");
  Serial.println(i);
  Serial.flush();
  delay(100);
}
#endif

const unsigned long intervall = 20;
const unsigned long intervall1 = 100;
unsigned long timeStamp;
unsigned long timeStamp1;
bool gamingActive = false;

communication com;
Axis X_Axis(PIN_X_STEP, PIN_X_DIRECTION, PIN_X_ENABLE, PIN_X_ENDSTOP, PIN_X_TROUBLE);
Axis Y_Axis(PIN_Y_STEP, PIN_Y_DIRECTION, PIN_Y_ENABLE, PIN_Y_ENDSTOP, PIN_Y_TROUBLE);
Axis Z_Axis(PIN_Z_STEP, PIN_Z_DIRECTION, PIN_Z_ENABLE, PIN_Z_ENDSTOP, PIN_Z_TROUBLE);
Beeping beep(PIN_BEEPER, 400, 1000);

void analyzeMotionCernel();
void writeRequestedValue();
void readNewCommand();
void checkReturnToZero();

void setup()
{
  Serial.begin(38400);
  while (!Serial)
    ;
  delay(500);
#ifdef USE_EEPROM
  EEPROM.begin(512);
  X_Axis.loadEEPROM();
  Y_Axis.loadEEPROM();
  Z_Axis.loadEEPROM();
#endif
  // Achse initialisieren
  Axis::ExecutePointer[0] = []()
  { X_Axis.execute(); };
  Axis::ExecutePointer[1] = []()
  { Y_Axis.execute(); };
  Axis::ExecutePointer[2] = []()
  { Z_Axis.execute(); };

  pinMode(LED_BUILTIN, OUTPUT);
  pinMode(PIN_ENABLE, INPUT_PULLUP);
  pinMode(PIN_BEEPER, OUTPUT);

  digitalWrite(PIN_BEEPER, HIGH);
  delay(500);
  digitalWrite(PIN_BEEPER, LOW);
  delay(100);

#ifdef ANALYZE_MOTION_CERNEL
  timeStamp = millis();
  timeStamp1 = millis();
#endif
}

//--------------------------------------LOOP-------------------------------------------------
void loop()
{
#ifdef ANALYZE_MOTION_CERNEL
  analyzeMotionCernel();
#endif
#ifdef AUTO_RETURN_TO_ZERO
  checkReturnToZero();
#endif
  com.execute();
  if (com.getRequestedValue() != IDLE)
  {
    writeRequestedValue();
  }

  if (com.recived_value.is_available)
  {
    readNewCommand();
  }

#ifndef ALLOW_MOVEMENT_AFTER_BOOTUP
  static bool movementWasDisallowedOnce = false;
  if (MOVEMENT_NOT_ALLOWED)
  {
    movementWasDisallowedOnce = true;
  }
  if (!movementWasDisallowedOnce)
  {
    beep.beep(2);
    return;
  }
#endif

#ifdef NO_HARDWARE
  Axis::enableStepping();
  X_Axis.lock();
  Y_Axis.lock();
  Z_Axis.lock();
#else

  if (!X_Axis.isHomed())
  {
    X_Axis.home();
  }
  if (!Y_Axis.isHomed())
  {
    Y_Axis.home();
  }
  if (X_Axis.hasError() || Y_Axis.hasError())// || Z_Axis.hasError())
  {
    Axis::disableStepping();
    X_Axis.unlock();
    Y_Axis.unlock();
    Z_Axis.unlock();
    if (X_Axis.hasError())
    {
      beep.beep(X_Axis.getErrorID());
    }
    if (Y_Axis.hasError())
    {
      beep.beep(Y_Axis.getErrorID() + 10);
    }
    /*if (Z_Axis.hasError())
    {
      beep.beep(Z_Axis.getErrorID() + 20);
    }*/
    if (MOVEMENT_NOT_ALLOWED)
    {
      X_Axis.resetAxis();
      Y_Axis.resetAxis();
      Z_Axis.resetAxis();
    }
  }
  else
  {
    beep.kill();
    if (!Axis::steppingIsEnabled())
    {
      if MOVEMENT_ALLOWED
      {
        beep.doubleBeep();
        X_Axis.lock();
        Y_Axis.lock();
        Z_Axis.lock();
        Axis::enableStepping();
      }
    }
  }

  if (digitalRead(PIN_ENABLE) && Axis::steppingIsEnabled())
  {
    if MOVEMENT_NOT_ALLOWED
    {
      Axis::disableStepping();
      beep.doubleBeep();
    }
  }
#endif
}

// END OF LOOP--------------------------------------------

void writeRequestedValue()
{
  switch (com.getRequestedValue())
  {
  case POSITION:
    com.fillValueBuffer(
        X_Axis.getCurrentPosition() * 0xFFFF / X_Axis.getMaxPosition(),
        Y_Axis.getCurrentPosition() * 0xFFFF / Y_Axis.getMaxPosition(),
        Z_Axis.getCurrentPosition() * 0xFFFF / Z_Axis.getMaxPosition());
    break;

  case IST_POSITION:
    com.fillValueBuffer(
        constrain(X_Axis.getCurrentPosition(), 0, X_Axis.getMaxPosition()),
        constrain(Y_Axis.getCurrentPosition(), 0, Y_Axis.getMaxPosition()),
        constrain(Z_Axis.getCurrentPosition(), 0, Z_Axis.getMaxPosition()));
    break;

  case MAX_POSITION:
    com.fillValueBuffer(X_Axis.getMaxPosition(), Y_Axis.getMaxPosition(), Z_Axis.getMaxPosition());
    break;

  case HOMING_OFFSET:
    com.fillValueBuffer(X_Axis.getHomingOffset(), Y_Axis.getHomingOffset(), Z_Axis.getHomingOffset());
    break;

  case ACCELERATION:
    com.fillValueBuffer(X_Axis.getAcceleration(), Y_Axis.getAcceleration(), Z_Axis.getAcceleration());
    break;

  case DECELERATION:
    com.fillValueBuffer(X_Axis.getDeceleration(), Y_Axis.getDeceleration(), Z_Axis.getDeceleration());
    break;

  case MAX_SPEED:
    com.fillValueBuffer(X_Axis.getMaxSpeed(), Y_Axis.getMaxSpeed(), Z_Axis.getMaxSpeed());
    break;

  case HOMING_STATUS:
    com.fillValueBuffer(X_Axis.isHomed(), Y_Axis.isHomed(), Z_Axis.isHomed());
    break;

  case HOMING_SPEED:
    com.fillValueBuffer(X_Axis.getHomingSpeed(), Y_Axis.getHomingSpeed(), Z_Axis.getHomingSpeed());
    break;

  case HOMING_ACCELERATION:
    com.fillValueBuffer(X_Axis.getHomingAcceleration(), Y_Axis.getHomingAcceleration(), Z_Axis.getHomingAcceleration());
    break;

  case INFORMATION:
    com.fillValueBuffer(com.fps, Axis::getWorkload() * 100, com.failedCommands);
    break;

  case INIT_SUCCESSFUL:
    com.fillValueBuffer(0, 0, 0);
    break;

  case STATE_UPDATE_INTERVALL:
    com.fillValueBuffer(com.stateUpdateIntervall, com.sendState, com.onlySendMotorPosition);
    break;

  case ENDSTOP_STATUS:
    com.fillValueBuffer(X_Axis.getEndstopState(), Y_Axis.getEndstopState(), Z_Axis.getEndstopState());
    break;

  case ERROR_STATUS:
    com.fillValueBuffer(X_Axis.hasError(), Y_Axis.hasError(), Z_Axis.hasError());
    break;

  case OTHER_STATES:
    com.fillValueBuffer(MOVEMENT_ALLOWED, Axis::steppingIsEnabled(), Axis::interruptActive());
    break;

  case CURRENT_SPEED:
    com.fillValueBuffer(X_Axis.currentVelocity / STEPS_PER_MM, Y_Axis.currentVelocity / STEPS_PER_MM, Z_Axis.currentVelocity / STEPS_PER_MM);
    break;

  case HOMING_STEP:
    com.fillValueBuffer((int)X_Axis.homingStep, (int)Y_Axis.homingStep, (int)Z_Axis.homingStep);
    break;
  
  case ERROR_ID:
    com.fillValueBuffer((int)X_Axis.getErrorID(), (int)Y_Axis.getErrorID(), (int)Z_Axis.getErrorID());
    break;
  
  case DRIVE_STATE:
    com.fillValueBuffer((int)X_Axis.getDriveState(), (int)Y_Axis.getDriveState(), (int)Z_Axis.getDriveState());
    break;

  case FILTER_CONSTANT:
    com.fillValueBuffer(X_Axis.getFilterConstant(), 0, 0);
    break;

  default:
    break;
  }
}

void readNewCommand()
{
  switch (com.recived_value.command)
  {
  case POSITION:
    X_Axis.moveAbsoluteSteps(X_Axis.getMaxPosition() * STEPS_PER_MM * ((float)com.recived_value.as_int16[0] / 0xFFFFul), gamingActive);
    Y_Axis.moveAbsoluteSteps(Y_Axis.getMaxPosition() * STEPS_PER_MM * ((float)com.recived_value.as_int16[1] / 0xFFFFul), gamingActive);
    Z_Axis.moveAbsoluteSteps(Z_Axis.getMaxPosition() * STEPS_PER_MM * ((float)com.recived_value.as_int16[2] / 0xFFFFul), gamingActive);
    break;

  case MAX_POSITION:
    X_Axis.setMaxPosition(com.recived_value.as_int16[0]);
    Y_Axis.setMaxPosition(com.recived_value.as_int16[1]);
    Z_Axis.setMaxPosition(com.recived_value.as_int16[2]);
    break;

  case HOMING_OFFSET:
    X_Axis.setHomingOffset(com.recived_value.as_int16[0]);
    Y_Axis.setHomingOffset(com.recived_value.as_int16[1]);
    Z_Axis.setHomingOffset(com.recived_value.as_int16[2]);
    break;

  case ACCELERATION:
    X_Axis.setAcceleration(com.recived_value.as_int16[0]);
    Y_Axis.setAcceleration(com.recived_value.as_int16[1]);
    Z_Axis.setAcceleration(com.recived_value.as_int16[2]);
    break;

  case DECELERATION:
    X_Axis.setDeceleration(com.recived_value.as_int16[0]);
    Y_Axis.setDeceleration(com.recived_value.as_int16[1]);
    Z_Axis.setDeceleration(com.recived_value.as_int16[2]);
    break;

  case MAX_SPEED:
    X_Axis.setSpeed(com.recived_value.as_int16[0]);
    Y_Axis.setSpeed(com.recived_value.as_int16[1]);
    Z_Axis.setSpeed(com.recived_value.as_int16[2]);
    break;

  case HOMING_SPEED:
    X_Axis.setHomingSpeed(com.recived_value.as_int16[0]);
    Y_Axis.setHomingSpeed(com.recived_value.as_int16[1]);
    Z_Axis.setHomingSpeed(com.recived_value.as_int16[2]);
    break;

  case HOMING_ACCELERATION:
    X_Axis.setHomingAcceleration(com.recived_value.as_int16[0]);
    Y_Axis.setHomingAcceleration(com.recived_value.as_int16[1]);
    Z_Axis.setHomingAcceleration(com.recived_value.as_int16[2]);
    break;

  case NEW_HOMING:
    if (com.recived_value.as_bool[0])
    {
      X_Axis.home();
    }
    if (com.recived_value.as_bool[1])
    {
      Y_Axis.home();
    }
    if (com.recived_value.as_bool[2])
    {
      Z_Axis.home();
    }
    break;

  case SAVE_SETTINGS:
    if (com.recived_value.as_bool[0])
    {
      X_Axis.saveData();
      Y_Axis.saveData();
      Z_Axis.saveData();
    }
    if (com.recived_value.as_bool[1])
    {
      ESP.restart();
    }
    break;

  case RESET_EEPROM:
    for (unsigned i = 0; i < EEPROM.length(); i++)
    {
      EEPROM.write(i, 0xFF);
      EEPROM.commit();
    }
    ESP.restart();
    break;

  case FILTER_CONSTANT:
    X_Axis.setFilterConstant(com.recived_value.as_int16[0]);
    Y_Axis.setFilterConstant(com.recived_value.as_int16[0]);
    Z_Axis.setFilterConstant(com.recived_value.as_int16[0]);

  default:
    break;
  }
  com.recived_value.is_available = false;
  digitalWrite(LED_BUILTIN, !digitalRead(LED_BUILTIN));
}

#ifdef ANALYZE_MOTION_CERNEL
void analyzeMotionCernel()
{
  unsigned long myMillis = millis();

  /*if (myMillis - timeStamp > intervall)
  {
    Serial.println(X_Axis.getCurrentPosition());
    Serial.flush();
    timeStamp += intervall;
  }*/

  if (myMillis - timeStamp1 > intervall1)
  {
    int x = random(0, 3);
    switch (x)
    {
    case 0:
      X_Axis.moveAbsolute(random(0, X_Axis.getMaxPosition()));
      Y_Axis.moveAbsolute(random(0, Y_Axis.getMaxPosition()));
      break;

    case 1:
      X_Axis.moveAbsolute(X_Axis.getMaxPosition());
      Y_Axis.moveAbsolute(Y_Axis.getMaxPosition());
      break;

    case 2:
      X_Axis.moveAbsolute(0);
      Y_Axis.moveAbsolute(0);
      break;

    default:
      break;
    }
    timeStamp1 += intervall1;
  }

  /*if (Serial.available() != 0)
  {
    while (Serial.available() != 0)
    {
      Serial.read();
    }
    while (Serial.available() == 0)
    {
    }
    while (Serial.available() != 0)
    {
      Serial.read();
    }
  }*/
}
#endif
#ifdef AUTO_RETURN_TO_ZERO
void checkReturnToZero()
{
  static unsigned long millisFPSWasHigh;
  if (com.fps > 5 || (gamingActive && (X_Axis.isActive() || Y_Axis.isActive() || Z_Axis.isActive())))
  {
    millisFPSWasHigh = millis();
    if (!gamingActive)
    {
      gamingActive = true;
    }
  }
  else
  {
    if (gamingActive && millis() - millisFPSWasHigh > 200)
    {
      X_Axis.moveAbsolute(X_Axis.getMaxPosition() / 2, 40);
      Y_Axis.moveAbsolute(Y_Axis.getMaxPosition() / 2, 40);
      Z_Axis.moveAbsolute(Z_Axis.getMaxPosition() / 2, 40);
      gamingActive = false;
    }
  }
}
#endif