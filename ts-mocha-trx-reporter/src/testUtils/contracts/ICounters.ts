export interface ICounters {
    total: number;
    executed: number;
    passed: number;
    error: number;
    failed: number;
    timeout: number;
    aborted: number;
    inconclusive: number;
    passedButRunAborted: number;
    notRunnable: number;
    notExecuted: number;
    disconnected: number;
    warning: number;
    completed: number;
    inProgress: number;
    pending: number;
}
