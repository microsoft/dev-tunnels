import * as path from 'path';
import * as uuid from 'uuid';
import { ITestRunOptions } from './contracts/ITestRunOptions';
import { ICounters } from './contracts/ICounters';
import { ITestRun } from './contracts/ITestRun';
import { ITestDefinition, ITestEntry, IUnitTestResult, IUnitTest } from './contracts/ITests';
import { testFormatter } from './formatter';

const resultsNotInAList = {
    id: '8c84fa94-04c1-424b-9868-57a2d4851a1d',
    name: 'Results Not in a List',
};

const allLoadedResults = {
    id: '19431567-8539-422a-85d7-44ee4e166bda',
    name: 'All Loaded Results',
};

export class TestRun implements ITestRun {
    public id: string;
    public testSettings: {
        id: string;
        name: string;
    };
    public testDefinitions: IUnitTest[];
    public testLists = [resultsNotInAList, allLoadedResults];
    public testEntries: ITestEntry[];
    public testResults: IUnitTestResult[];
    public counters: ICounters;

    constructor(public options: ITestRunOptions) {
        this.id = uuid.v4();
        this.testSettings = {
            id: uuid.v4(),
            name: 'default',
        };
        this.counters = {
            total: 0,
            executed: 0,
            passed: 0,
            error: 0,
            failed: 0,
            timeout: 0,
            aborted: 0,
            inconclusive: 0,
            passedButRunAborted: 0,
            notRunnable: 0,
            notExecuted: 0,
            disconnected: 0,
            warning: 0,
            completed: 0,
            inProgress: 0,
            pending: 0,
        };
        this.testDefinitions = [];
        this.testEntries = [];
        this.testResults = [];
    }

    public toXml() {
        return testFormatter(this);
    }

    public addResult(params: ITestDefinition) {
        const executionId = uuid.v4();

        /* Adding test definition */
        params.test.executionId = executionId;
        this.testDefinitions.push(params.test);

        /* Adding test entry */
        let testEntry = {
            testId: params.test.id,
            executionId: executionId,
            testListId: resultsNotInAList.id,
        };
        this.testEntries.push(testEntry);

        /* Adding results */
        let testResult: IUnitTestResult = {
            testName: params.test.name,
            testType: params.test.type,
            testId: params.test.id,
            testListId: resultsNotInAList.id,
            computerName: params.computerName,
            outcome: params.outcome,
            startTime: params.startTime,
            endTime: params.endTime,
            duration: params.duration,
            errorMessage: params.errorMessage,
            errorStacktrace: params.errorStacktrace,
            relativeResultsDirectory: executionId,
            storage: params.test.storage,
            executionId,
            resultFiles: [],
        };

        if (this.options.screenshotsPath) {
            /* Adding screenshots as attachments */
            const screenshotsFolderPath = path.join('..', '..', '..', this.options.screenshotsPath);
            const testResultPath = path.join(
                screenshotsFolderPath,
                params.test.name.replace(' ', '.'),
                'screenshot-window0.png',
            );
            testResult.resultFiles = [{ path: testResultPath }];
        }

        this.testResults.push(testResult);
        this.incrementCounters(params.outcome);

        return this;
    }

    public incrementCounters(outcome: string) {
        this.counters.total += 1;
        switch (outcome) {
            case 'Passed':
                this.counters.executed += 1;
                this.counters.passed += 1;
                break;
            case 'Failed':
                this.counters.executed += 1;
                this.counters.failed += 1;
                break;
            case 'Inconclusive':
                this.counters.executed += 1;
                this.counters.inconclusive += 1;
                break;
            case 'Timeout':
                this.counters.executed += 1;
                this.counters.timeout += 1;
                break;
            case 'Pending':
                this.counters.pending += 1;
                break;
            case 'NotExecuted':
                this.counters.notExecuted += 1;
                break;
            default:
                break;
        }
    }
}
