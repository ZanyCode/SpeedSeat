void kill(){
  stopAxis(0);
  stopAxis(1);
  stopAxis(2);
  killCalled = true;
}

void printKill(){

  Serial.print("kill called! Errornumber: ");
  Serial.println(ErrorNumber);

  Serial.print("Parameter of Axis ");
  Serial.print(Axis->AxisNomber);
  Serial.println(":");

  Serial.print("Drehrichtung: ");
  Serial.println(Axis->currentDirection);

  Serial.print("stepPeriodInProcessorCycles: ");
  Serial.println(Axis->stepPeriodInProcessorCycles);

  Serial.println("Unit -> Steps ---------------------------------");

  Serial.print("sollPosition: ");
  Serial.println(Axis->sollPosition);

  Serial.print("istPosition: ");
  Serial.println(Axis->istPosition);

  Serial.print("currentSpeed: ");
  Serial.println(Axis->currentSpeed);

  Serial.println("Unit -> Millimeter ----------------------------");

  Serial.print("sollPosition: ");
  Serial.println(Axis->sollPosition/stepsPerMillimeter);

  Serial.print("istPosition: ");
  Serial.println(Axis->istPosition/stepsPerMillimeter);

  Serial.print("currentSpeed: ");
  Serial.println(Axis->currentSpeed/stepsPerMillimeter);
}

const unsigned int timeTillTrouble = 100;
void checkForKill(){
    unsigned int counter = 0;
    if(X_Axis.homingAbgeschlossen & !digitalRead(X_Axis.Pin.Endstop)){
        while (!digitalRead(X_Axis.Pin.Endstop)){
            digitalWrite(beeperPin,HIGH);
            delay(5);
            digitalWrite(beeperPin,LOW);
            delay(5);
            counter = counter + 10;
            if (counter >= timeTillTrouble){
                break;
            }
        }
        if (counter >= timeTillTrouble){
            ErrorNumber = 3;
            kill();
        }
    }
    
    counter = 0;
    if(Y_Axis.homingAbgeschlossen & !digitalRead(Y_Axis.Pin.Endstop)){
        while (!digitalRead(Y_Axis.Pin.Endstop)){
            digitalWrite(beeperPin,HIGH);
            delay(5);
            digitalWrite(beeperPin,LOW);
            delay(5);
            counter = counter + 10;
            if (counter >= timeTillTrouble){
                break;
            }
        }
        if (counter >= timeTillTrouble){
            ErrorNumber = 13;
            kill();
        }
    }

    counter = 0;
    if(Z_Axis.homingAbgeschlossen & !digitalRead(Z_Axis.Pin.Endstop)){
        while (!digitalRead(Z_Axis.Pin.Endstop)){
            digitalWrite(beeperPin,HIGH);
            delay(5);
            digitalWrite(beeperPin,LOW);
            delay(5);
            counter = counter + 10;
            if (counter >= timeTillTrouble){
                break;
            }
        }
        if (counter >= timeTillTrouble){
            ErrorNumber = 23;
            kill();
        }
    }




    //Störung Treiber----------------------
    counter = 0;
    if(!digitalRead(X_Axis.Pin.Trouble)){
        while (!digitalRead(X_Axis.Pin.Trouble)){
            digitalWrite(beeperPin,HIGH);
            delay(5);
            digitalWrite(beeperPin,LOW);
            delay(5);
            counter = counter + 10;
            if (counter >= timeTillTrouble){
                break;
            }
        }
        if (counter >= timeTillTrouble){
            ErrorNumber = 4;
            kill();
        }
    }

    counter = 0;
    if(!digitalRead(Y_Axis.Pin.Trouble)){
        while (!digitalRead(Y_Axis.Pin.Trouble)){
            digitalWrite(beeperPin,HIGH);
            delay(5);
            digitalWrite(beeperPin,LOW);
            delay(5);
            counter = counter + 10;
            if (counter >= timeTillTrouble){
                break;
            }
        }
        if (counter >= timeTillTrouble){
            ErrorNumber = 14;
            kill();
        }
    }

    counter = 0;
    if(!digitalRead(Z_Axis.Pin.Trouble)){
        while (!digitalRead(Z_Axis.Pin.Trouble)){
            digitalWrite(beeperPin,HIGH);
            delay(5);
            digitalWrite(beeperPin,LOW);
            delay(5);
            counter = counter + 10;
            if (counter >= timeTillTrouble){
                break;
            }
        }
        if (counter >= timeTillTrouble){
            ErrorNumber = 24;
            kill();
        }
    }

    if (killCalled){
        Serial.write(ErrorNumber);
        X_Axis.aktiv = false;
        Y_Axis.aktiv = false;
        Z_Axis.aktiv = false;
        X_Axis.currentSpeed = 0;
        Y_Axis.currentSpeed = 0;
        Z_Axis.currentSpeed = 0;
        digitalWrite(X_Axis.Pin.Enable,HIGH);
        digitalWrite(Y_Axis.Pin.Enable,HIGH);
        digitalWrite(Z_Axis.Pin.Enable,HIGH);
        if (simulation){
            printKill();
        }
        
        
        while(!digitalRead(enablePin)){
            int x = ErrorNumber;
            while ((x > 0) & !digitalRead(enablePin)){
            digitalWrite(beeperPin,HIGH);
            delay(200);
            digitalWrite(beeperPin,LOW);
            delay(200);
            x--;
            }
            if(!digitalRead(enablePin)){delay(1000);}
        delay(100);
        }
        while(digitalRead(enablePin)){
            delay(100);
        }
        digitalWrite(X_Axis.Pin.Enable,LOW);
        digitalWrite(Y_Axis.Pin.Enable,LOW);
        digitalWrite(Z_Axis.Pin.Enable,LOW);
        requestHome = true;
        killCalled = false;
    }
}