unsigned long int ZykluszeitInMicrosekunden;
unsigned long int microsPreviousCycle;
unsigned long int ZeitSeitLetztemSerialPrint;
bool blockSerialForOneCycleToMeasureCylcetime;

void simulationPrint(){
    unsigned long int myMicros = micros();
    ZykluszeitInMicrosekunden = myMicros - microsPreviousCycle;
    microsPreviousCycle = myMicros;
    ZeitSeitLetztemSerialPrint = ZeitSeitLetztemSerialPrint + ZykluszeitInMicrosekunden;
    if (!blockSerialForOneCycleToMeasureCylcetime){
        if (ZeitSeitLetztemSerialPrint > 1000000){
            ZeitSeitLetztemSerialPrint = ZeitSeitLetztemSerialPrint - 1000000;
            Serial.print("Zykluszeit: ");



            Serial.print(ZykluszeitInMicrosekunden); 
            Serial.print(" Sollposition: ");
            Serial.print(X_Axis.sollPosition/stepsPerMillimeter);
            Serial.print(" | ");
            Serial.print(Y_Axis.sollPosition/stepsPerMillimeter);
            Serial.print(" | ");
            Serial.print(Z_Axis.sollPosition/stepsPerMillimeter);

            Serial.print(" Istposition: ");
            Serial.print(X_Axis.istPosition/stepsPerMillimeter);
            Serial.print(" | ");
            Serial.print(Y_Axis.istPosition/stepsPerMillimeter);
            Serial.print(" | ");
            Serial.print(Z_Axis.istPosition/stepsPerMillimeter);


            Serial.print(" Speed: ");
            Serial.print(X_Axis.currentSpeed/stepsPerMillimeter);
            Serial.print(" | ");
            Serial.print(Y_Axis.currentSpeed/stepsPerMillimeter);
            Serial.print(" | ");
            Serial.print(Z_Axis.currentSpeed/stepsPerMillimeter);


            Serial.println();
            blockSerialForOneCycleToMeasureCylcetime = true;
    }
  }else{
    blockSerialForOneCycleToMeasureCylcetime = false;
  }

}


void printHomingParameter(struct Achse AxVariable){
  if (AxVariable.AxisNomber == 0){
    Serial.println("-----Homingparameter X Achse-----");
  }
  if (AxVariable.AxisNomber == 1){
    Serial.println("-----Homingparameter Y Achse-----");
  }
  if (AxVariable.AxisNomber == 2){
    Serial.println("-----Homingparameter Z Achse-----");
  }
  Serial.print("Endstop Pin: ");Serial.print(AxVariable.Pin.Endstop);Serial.print(" Aktuell: ");Serial.println(digitalRead(AxVariable.Pin.Endstop));
}


void dumpAxisParameter(){
  Serial.println();
  Serial.println();
  int x = 0;

  while (x < 3){
    struct Achse *TempAchse;
    if (x==0){
      TempAchse = &X_Axis;
      Serial.println("Parameter of X Axis:");
    }
    if (x==1){
      TempAchse = &Y_Axis;
      Serial.println("Parameter of Y Axis:");
    }
    if (x==2){
      TempAchse = &Z_Axis;
      Serial.println("Parameter of Z Axis:");
    }

    Serial.println("Unit -> Steps ---------------------------------");

    Serial.print("Accelleration: ");
    Serial.println(TempAchse->acceleration); 

    Serial.print("accelerationPerAccelerationRecalculation: ");
    Serial.println(TempAchse->accelerationPerAccelerationRecalculation); 

    Serial.print("MaxPosition: ");
    Serial.println(TempAchse->MaxPosition); 

    Serial.println("Unit -> Millimeter ----------------------------");

    Serial.print("Accelleration: ");
    Serial.println(TempAchse->acceleration/stepsPerMillimeter); 

    Serial.print("MaxPosition: ");
    Serial.println(TempAchse->MaxPosition/stepsPerMillimeter);

    Serial.println();
    Serial.println();
    x++;
  }
}


void printInputStatus(){
  Serial.println();
  Serial.println();
  Serial.println("--------------------INPUTSTATUS-------------------------");
  int x = 0;
  while (x < 3){
    Axis = getAxis(x);
    if(x == 0){
      Serial.println("X Achse-----");
    }
    if(x == 1){
      Serial.println("Y Achse-----");
    }
    if(x == 2){
      Serial.println("Z Achse-----");
    }

    Serial.print("Endstop:");
    Serial.println(digitalRead(Axis->Pin.Endstop));

    Serial.print("InPosition:");
    Serial.println(digitalRead(Axis->Pin.InPosition));

    Serial.print("Trouble:");
    Serial.println(digitalRead(Axis->Pin.Trouble));


    x++;
  }
  Serial.println();
  Serial.print("Enable: ");
  Serial.println(digitalRead(enablePin));

  Serial.println();
  Serial.println();
}