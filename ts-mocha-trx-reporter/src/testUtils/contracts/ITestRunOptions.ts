export interface ITestRunOptions {
    name: string;
    runUser: string;
    settings: { name: string };
    times: {
        creation: string;
        queuing: string;
        start: string;
        finish: string;
    };
    screenshotsPath: string;
}
