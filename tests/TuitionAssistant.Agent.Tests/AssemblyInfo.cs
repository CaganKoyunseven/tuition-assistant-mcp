using Xunit;

// Every test starts its own in-memory host, and the JWT library checks token shapes with regular expressions that have
// a short match timeout. When many hosts start at the same moment on a cold run (first run, slow disk, busy machine),
// a token check can time out and the call is rejected with 401. Running the tests one after another removes that
// race; the whole suite still finishes in a few seconds.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
