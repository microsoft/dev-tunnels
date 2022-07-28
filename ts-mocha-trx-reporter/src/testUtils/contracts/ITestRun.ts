import { ITestRunOptions } from './ITestRunOptions';
import { ICounters } from './ICounters';
import { ITestEntry, IUnitTestResult, IUnitTest } from './ITests';

//TODO: change any
export interface ITestRun {
    id: string;
    testSettings: {
        id: string;
        name: string;
    };
    testDefinitions: IUnitTest[];
    testLists: any[];
    testEntries: ITestEntry[];
    testResults: IUnitTestResult[];
    counters: ICounters;
    options: ITestRunOptions;
}
