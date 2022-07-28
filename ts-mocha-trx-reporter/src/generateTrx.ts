import { ITestDefinition } from './testUtils/contracts/ITests';

const path = require('path');
const uuid = require('uuid');

export function testToTrx(test: any, computerName: any, cwd: any, options: any): ITestDefinition {
    const safeCwd = cwd || '';
    return {
        test: {
            name: test.fullTitle(),
            methodCodeBase: test.file ? path.relative(safeCwd, test.file) : 'none',
            methodName: test.fullTitle(),
            methodClassName: 'none',
            storage: 'none',
            type: '13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b',
            id: uuid.v4(),
        },
        computerName,
        outcome: getOutcome(test, options),
        duration: getDuration(test.duration || 0),
        startTime: test.start ? test.start.toISOString() : '',
        endTime: test.end ? test.end.toISOString() : '',
        errorMessage: test.err ? test.err.message : '',
        errorStacktrace: test.err ? test.err.stack : '',
    };
}

function getDuration(milliseconds: number) {
    const duration = new Date(milliseconds).toISOString();
    return duration.substring(duration.indexOf('T') + 1).replace('Z', '');
}

function getOutcome(test: any, opts: any) {
    const options = opts || {};
    if (test.timedOut === true) {
        return 'Timeout';
    }
    if (test.pending === true) {
        if (test.err) {
            return 'Failed';
        }

        return options.treatPendingAsNotExecuted === true ? 'NotExecuted' : 'Pending';
    }
    switch (test.state) {
        case 'passed':
        case 'failed':
            return test.state.charAt(0).toUpperCase() + test.state.slice(1);
        default:
            return 'Inconclusive';
    }
}
