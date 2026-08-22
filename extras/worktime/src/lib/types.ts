import type { RecordModel } from 'pocketbase';

export type Ministry = RecordModel & {
  name: string;
  account: string;
};

export type Salary = RecordModel & {
  name: string;
  amount: number;
  maxHours: number;
};

export type Employee = RecordModel & {
  name: string;
  account: string;
  ministry: string;
  salary: string;
  fired: boolean;
  expand?: {
    ministry?: Ministry;
    salary?: Salary;
  };
};

export type WorkSession = RecordModel & {
  start: string;
  end?: string;
  finished: boolean;
  employee: string;
  expand?: {
    employee?: Employee;
  };
};

export type WorkLog = RecordModel & {
  employee: string;
  action: 'clocked_in' | 'clocked_out';
  timestamp: string;
  expand?: {
    employee?: Employee;
  };
};
