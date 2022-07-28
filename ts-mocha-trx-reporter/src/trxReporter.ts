import * as os from 'os';
import * as fs from 'fs';
import { TestRun } from './testUtils/testRun';
import { testToTrx } from './generateTrx';

const { Runner, reporters } = require('mocha');
const { EVENT_RUN_END, EVENT_TEST_BEGIN, EVENT_TEST_END, EVENT_TEST_FAIL } = Runner.constants;

export function trxReporter(this: any, runner: any, options: any) {
    reporters.Base.call(this, runner, options);

    const self = this;
    const tests = new Set();

    runner.on(EVENT_TEST_BEGIN, (test: any) => {
        test.start = new Date();
    });

    runner.on(EVENT_TEST_END, (test: any) => {
        test.end = new Date();
        tests.add(test);
    });

    runner.on(EVENT_TEST_FAIL, (test: any) => {
        test.end = new Date();
        tests.add(test);
        if (options.reporterOptions.abortAfterFailure) {
            generateReport(self, tests, options, true);
            runner.abort();
        }
    });

    runner.on(EVENT_RUN_END, () => {
        generateReport(self, tests, options, false);
    });
}

function generateReport(
    self: any,
    tests: Set<any>,
    options: any,
    erroredOut: boolean,
) {
    const computerName = os.hostname();
    const userName = os.userInfo().username;
    const cwd = process.cwd();

    const testResults = {
        stats: self.stats,
        tests: [...tests.values()],
    };

    const now = new Date().toISOString();
    const testRunName = `${userName}@${computerName} ${now}`;
    const run = new TestRun({
        name: testRunName,
        runUser: userName,
        settings: {
            name: 'default',
        },
        times: {
            creation: now,
            queuing: now,
            start: testResults.stats.start.toISOString(),
            finish: erroredOut ? now : testResults.stats.end.toISOString(),
        },
        screenshotsPath: options.reporterOptions.screenshotsPath,
    });

    const reporterOptions = options.reporterOptions || {};

    testResults.tests.forEach((test) => {
        run.addResult(testToTrx(test, computerName, cwd, reporterOptions));
    });

    const filename = reporterOptions.output || process.env.MOCHA_REPORTER_FILE;
    if (filename) {
        fs.writeFileSync(filename, run.toXml());
    } else {
        process.stdout.write(run.toXml());
    }
}
