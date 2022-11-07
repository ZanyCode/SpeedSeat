import { Byte } from "@angular/compiler/src/util";

export enum ValueType {
    Numeric,
    Boolean,
    Action
}

export interface CommandValue {
    label: string;
    type: ValueType;
    scaleToFullRange: boolean;
    min: number;
    max: number;
    value: number | null;
    isUnblocked: boolean;
}

export interface Command {
    groupLabel: string;
    id: Byte;    
    readonly: boolean;
    value1: CommandValue;
    value2: CommandValue;
    value3: CommandValue;    
}