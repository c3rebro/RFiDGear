using Xunit;

// The two fixtures of this project share one reader and one COM port: their tests must not run at the same time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
