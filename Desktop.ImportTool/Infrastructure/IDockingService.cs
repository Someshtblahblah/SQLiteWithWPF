namespace Desktop.ImportTool.Infrastructure
{
    /// <summary>
    /// VM-facing abstraction to request opening/activating panes.
    /// Implementations live in the Views layer and use RadDocking / RadPane.
    /// </summary>
    public interface IDockingService
    {
        /// <summary>
        /// Open or activate pane identified by key (e.g. "Tasks" or "History").
        /// Implementations should reuse view instances that the VM created and recreate panes if they were closed.
        /// </summary>
        /// <param name="paneKey">Key identifying the pane to open.</param>
        void OpenPane(string paneKey);
    }
}