#include "Arduino.h"
#include "Variablen.h"
#include "Timer.h"
#include "AxisDefinition.h"
#include "kill.h"
#include "createMovement.h"
#include "Steps.h"
#include "ISRs.h"
#include "serialPrint.h"
#include "homing.h"
#include "communication.h"
#include "beep.h"

const unsigned int MaxPosTest = 260*stepsPerMillimeter;

void setup()
{
  Serial.begin(250000);
  while (DEBUG_COMMUNICATION){
    executeCommunication();
  }
  TimerInitialisieren();
  initializeAxis(X_Axis);
  initializeAxis(Y_Axis);
  initializeAxis(Z_Axis);
  pinMode(enablePin,INPUT_PULLUP);
  pinMode(beeperPin,OUTPUT);

  digitalWrite(beeperPin,HIGH);
  delay(500);
  digitalWrite(beeperPin,LOW);
  delay(100);
  while (!digitalRead(enablePin) & !NO_HARDWARE){
    doubleBeep();
  }

  if (analyzeInputs){
    while(true){
      printInputStatus();
      delay(1000);
    }
  }

  while(digitalRead(enablePin) & !NO_HARDWARE){
    delay(100);
  }
  digitalWrite(X_Axis.Pin.Enable,LOW);
  digitalWrite(Y_Axis.Pin.Enable,LOW);
  digitalWrite(Z_Axis.Pin.Enable,LOW);

  if (simulation){
    dumpAxisParameter();
    printInputStatus();
  }


  if(!NO_HARDWARE){
    home();
  }
 
}
const bool HenryMoechteSichBewegen = false;
int AxisInBearbeitung = 1;
int Step;
//---------------------------------------------------------------------------------------
void loop(){
  if (allowCommadsWhenAxisIsAktiv || (!X_Axis.aktiv & !Y_Axis.aktiv & !Z_Axis.aktiv)){
    //delay(2000);
    if (EXECUTE_COMMUICATION){
      executeCommunication();
      if (newCommandPositionAvailable){
        newCommandPositionAvailable = false;
        move(0,newCommand.X_Position);
        move(1,newCommand.Y_Position);
        move(2,newCommand.Z_Position);
      }
    }
  }
  //if (!X_Axis.aktiv){
   // makeCustomMove();
  //}
  if(simulation){
    simulationPrint();
    if (Serial.available()!=0){
      int t = Serial.read();
      Serial.println(t);
      if (t == 49){AxisInBearbeitung = 0;}//1
      if (t == 50){AxisInBearbeitung = 1;}//2
      if (t == 51){AxisInBearbeitung = 2;}//3
      if (t == 52){move(1,Y_Axis.MaxPosition);}//4
      if (t == 53){move(1,0);}//5
      if (t == 54){makeCustomMove();}//6
      if (t == 43){
        if(AxisInBearbeitung == 0){
          move(AxisInBearbeitung,X_Axis.istPosition + getSteps(10));
        }
        if(AxisInBearbeitung == 1){
          move(AxisInBearbeitung,Y_Axis.istPosition + getSteps(10));
        }
        if(AxisInBearbeitung == 2){
          move(AxisInBearbeitung,Z_Axis.istPosition + getSteps(10));
        }
        
      }
      if (t == 45){
        if(AxisInBearbeitung == 0){
          move(AxisInBearbeitung,X_Axis.istPosition - getSteps(10));
        }
        if(AxisInBearbeitung == 1){
          move(AxisInBearbeitung,Y_Axis.istPosition - getSteps(10));
        }
        if(AxisInBearbeitung == 2){
          move(AxisInBearbeitung,Z_Axis.istPosition - getSteps(10));
        }
      }
    }
  }
  if(HenryMoechteSichBewegen){
      if (!X_Axis.aktiv & !Y_Axis.aktiv & !Z_Axis.aktiv){
        switch (Step)
        {
        case 0:
          move(0,X_Axis.MaxPosition-1);
          Step++;
          break;

        case 1:
          move(1,Y_Axis.MaxPosition-1);
          Step++;
          break;

        case 2:
          move(2,Z_Axis.MaxPosition-1);
          Step++;
          break;
        
        case 3:
          move(0,0);
          Step++;
          break;

        case 4:
          move(1,0);
          Step++;
          break;

        case 5:
          move(2,0);
          Step++;
          break;
        default:
       // HenryMoechteSichBewegen = false;
          Step = 0;
          break;
        }
      }
    }

  if(digitalRead(enablePin) & !NO_HARDWARE){
    stopAxis(0);
    stopAxis(1);
    stopAxis(2);
    X_Axis.aktiv = false;
    Y_Axis.aktiv = false;
    Z_Axis.aktiv = false;
    while(digitalRead(enablePin)){
      delay(100);
    }
    home();
  }
  checkForKill();
  if(requestHome){
    home();
    requestHome = false;
  }
}