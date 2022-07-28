import { ITestRun } from './contracts/ITestRun';
const builderLib = require('xmlbuilder');

export function testFormatter(testRun: ITestRun) {
    let xml = builderLib.create('TestRun');
    let el;

    // tslint:disable-next-line: no-http-string
    xml.att('xmlns', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010');
    xml.att('id', testRun.id);
    xml.att('name', testRun.options.name);

    if (testRun.options.runUser) {
        xml.att('runUser', testRun.options.runUser);
    }

    if (testRun.options.times) {
        el = xml
            .ele('Times')
            .att('creation', testRun.options.times.creation)
            .att('queuing', testRun.options.times.queuing)
            .att('start', testRun.options.times.start)
            .att('finish', testRun.options.times.finish);
    }

    el = xml
        .ele('TestSettings')
        .att('name', testRun.testSettings.name)
        .att('id', testRun.testSettings.id);

    el = xml.ele('ResultSummary');
    el.att('outcome', testRun.counters.failed > 0 ? 'Failed' : 'Completed')
        .ele('Counters')
        .att('total', testRun.counters.total)
        .att('executed', testRun.counters.executed)
        .att('passed', testRun.counters.passed)
        .att('error', testRun.counters.error)
        .att('failed', testRun.counters.failed)
        .att('timeout', testRun.counters.timeout)
        .att('aborted', testRun.counters.aborted)
        .att('inconclusive', testRun.counters.inconclusive)
        .att('passedButRunAborted', testRun.counters.passedButRunAborted)
        .att('notRunnable', testRun.counters.notRunnable)
        .att('notExecuted', testRun.counters.notExecuted)
        .att('disconnected', testRun.counters.disconnected)
        .att('warning', testRun.counters.warning)
        .att('completed', testRun.counters.completed)
        .att('inProgress', testRun.counters.inProgress)
        .att('pending', testRun.counters.pending);

    // if (testRun.resultFiles && testRun.resultFiles.length > 0) {
    //     buildArray(testRun.resultFiles, el.ele('ResultFiles'), buildResultFileEntry);
    // }

    el = xml.ele('TestDefinitions');
    buildArray(testRun.testDefinitions, el, buildTestDefinition);

    el = xml.ele('TestLists');
    buildArray(testRun.testLists, el, buildTestList);

    el = xml.ele('TestEntries');
    buildArray(testRun.testEntries, el, buildTestEntry);

    el = xml.ele('Results');
    buildArray(testRun.testResults, el, buildTestResult);

    return xml.end({ pretty: true });
};

function buildArray(items: any, element: any, builder: any) {
    items.forEach(function(item: any) {
        builder(element, item);
    });
}

function buildTestDefinition(parent: any, testDefinition: any) {
    let xml = parent
        .ele('UnitTest')
        .att('id', testDefinition.id)
        .att('name', testDefinition.name)
        .att('storage', testDefinition.storage);

    if (testDefinition.description) {
        xml.ele('Description', testDefinition.description);
    }

    if (testDefinition.owners) {
        let owners = xml.ele('Owners');
        buildArray(testDefinition.owners, owners, buildTestOwners);
    }

    xml.ele('Execution', { id: testDefinition.executionId });
    xml.ele('TestMethod')
        .att('codeBase', testDefinition.methodCodeBase)
        .att('className', testDefinition.methodClassName)
        .att('name', testDefinition.methodName);
}

function buildTestList(parent: any, testList: any) {
    let xml = parent
        .ele('TestList')
        .att('id', testList.id)
        .att('name', testList.name);
}

function buildTestEntry(parent: any, testEntry: any) {
    let xml = parent
        .ele('TestEntry')
        .att('testId', testEntry.testId)
        .att('executionId', testEntry.executionId)
        .att('testListId', testEntry.testListId);
}

function buildTestOwners(parent: any, owner: any) {
    let xml = parent.ele('Owner', owner).att('name', owner.name);
}

function buildTestResult(parent: any, result: any) {
    let xml = parent
        .ele('UnitTestResult')
        .att('testId', result.testId)
        .att('testName', result.testName)
        .att('testType', result.testType)
        .att('testListId', result.testListId)
        .att('computerName', result.computerName);

    if (result.outcome) {
        xml.att('outcome', result.outcome);
    }

    if (result.startTime) {
        xml.att('startTime', result.startTime);
    }

    if (result.endTime) {
        xml.att('endTime', result.endTime);
    }

    if (result.duration) {
        xml.att('duration', result.duration);
    }

    if (result.executionId) {
        xml.att('executionId', result.executionId);
    }

    if (result.relativeResultsDirectory) {
        xml.att('relativeResultsDirectory', result.relativeResultsDirectory);
    }

    if (result.resultFiles && result.resultFiles.length > 0) {
        buildArray(result.resultFiles, xml.ele('ResultFiles'), buildResultFileEntry);
    }

    if (result.output || result.errorMessage || result.errorStacktrace) {
        let output = xml.ele('Output');
        output.ele('StdOut', result.output || '');

        if (result.errorMessage || result.errorStacktrace) {
            let error = output.ele('ErrorInfo');
            error.ele('Message', result.errorMessage || '');
            error.ele('StackTrace', result.errorStacktrace || '');
        }
    }
}

function buildResultFileEntry(parent: any, result: any) {
    let xml = parent.ele('ResultFile').att('path', result.path);
}
