export interface ITestDefinition {
    test: IUnitTest;
    computerName: string;
    outcome: string;
    duration: string;
    startTime: string;
    endTime: string;
    errorMessage: string;
    errorStacktrace: string;
}

export interface IUnitTest {
    id: string;
    name: string;
    type: string;
    methodName: string;
    methodCodeBase: string;
    methodClassName: string;
    storage: string;
    owners?: string;
    description?: string;
    executionId?: string;
}

export interface ITestEntry {
    testId: string;
    executionId: string;
    testListId: string;
}

export interface IUnitTestResult {
    testName: string;
    testType: string;
    testId: string;
    testListId: string;
    computerName: string;
    outcome: string;
    startTime: string;
    endTime: string;
    duration: string;
    executionId: string;
    errorMessage: string;
    errorStacktrace: string;
    relativeResultsDirectory: string;
    storage: string;
    resultFiles: IResultFile[];
    output?: string;
}

export interface IResultFile {
    path: string;
}
