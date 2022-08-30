void beep(){
    digitalWrite(beeperPin,HIGH);
    delay(100);
    digitalWrite(beeperPin,LOW);
    delay(1000);
}
void doubleBeep(){
    digitalWrite(beeperPin,HIGH);
    delay(100);
    digitalWrite(beeperPin,LOW);
    delay(100);
    digitalWrite(beeperPin,HIGH);
    delay(100);
    digitalWrite(beeperPin,LOW);
    delay(1000);
}