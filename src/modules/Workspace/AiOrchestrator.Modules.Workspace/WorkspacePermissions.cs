namespace AiOrchestrator.Modules.Workspace;

/// <summary>
/// What operations on a folder require. Declared beside the use cases that name them, and granted
/// to a bundle in <see cref="WorkspaceModule.Add"/> — so the declaration and the grant are read
/// together, and a permission nobody grants is held by nobody rather than by everybody.
/// </summary>
static class WorkspacePermissions
{
    /// <summary>See the folder, its worktrees and its configured commands.</summary>
    public const string Read = "workspace:read";

    /// <summary>Create or remove a worktree — the operations that change the repository.</summary>
    public const string ManageWorktrees = "workspace:manage-worktrees";

    /// <summary>
    /// Remove a folder from the workspace — forget a checkout, without touching it on disk.
    /// <para>
    /// Its own constant rather than a second use of <see cref="ManageWorktrees"/>, because the line
    /// above says what that one is for and this is not it: removing a folder changes nothing in the
    /// repository. It sits with Admin for the same reason its neighbour does — it is the only way a
    /// curated list loses an entry, and a list that only grows is the state this exists to leave.
    /// </para>
    /// <para>
    /// There is no matching <c>add</c>: adding a folder is the one operation with no folder to hold a
    /// permission on, so <c>AddFolder</c> declares <c>Access.AnyCaller</c> instead.
    /// </para>
    /// </summary>
    public const string ManageFolders = "workspace:manage-folders";

    /// <summary>Hand the folder to the editor, the file manager or a terminal.</summary>
    public const string OpenOnDesktop = "workspace:open-on-desktop";

    /// <summary>
    /// Change what a folder defaults to — today, which agent it starts with.
    /// <para>
    /// Granted to Member rather than held with Admin, unlike the connector credentials next door.
    /// The distinction is what a wrong answer costs: naming the wrong secret reaches a vendor with
    /// somebody's token, while picking the wrong agent opens a terminal you close again. Choosing
    /// between the CLIs already installed on the machine is the everyday work here, not
    /// administration of it.
    /// </para>
    /// </summary>
    public const string Configure = "workspace:configure";
}
