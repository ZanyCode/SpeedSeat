import { Injectable } from "@angular/core";
import { BehaviorSubject, Observable, Subject } from "rxjs";

@Injectable({
    providedIn: 'root'
})
export class AppEventsService {
    private connectionStateChangedSubject = new BehaviorSubject<boolean>(false);
    public ConnectionStateChanged: Observable<boolean>;

    constructor() {
        this.ConnectionStateChanged = this.connectionStateChangedSubject.asObservable();
    }

    public signalConnectionStateChanged(newState: boolean) {
        this.connectionStateChangedSubject.next(newState);
    }
}
