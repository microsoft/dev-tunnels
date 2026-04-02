// Disable test parallelization in release builds to avoid potential issues with shared resources
// such as port conflicts during test runs.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]