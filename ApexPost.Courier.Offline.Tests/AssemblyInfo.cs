// Every test opens its own database in its own temporary folder. The tests run one after another so the native
// Couchbase Lite library is only started once at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
