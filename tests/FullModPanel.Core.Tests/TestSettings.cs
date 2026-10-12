// PanelCopy.Language is process-wide state. Run tests one at a time so an English
// test cannot change the strings another test expects in Chinese.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
