using System.IO;
using System.Text.Json;

namespace ProjectHub.Worker;

// HQ approval metadata is persisted locally across process restarts.
internal sealed record PendingGitApproval(
    string JobId,
    string WorkspaceRoot,
    MilestoneDefinition Milestone,
    string[] Paths,
    Dictionary<string, string> Snapshot);

public partial class MainWindow
{
    private static string PendingGitApprovalPath(string root) =>
        Path.Combine(root, ".projecthub", "pending-git-approval.json");

    internal static PendingGitApproval? LoadPendingGitApproval(string root)
    {
        var path = PendingGitApprovalPath(root);
        if (!File.Exists(path)) return null;
        var pending = JsonSerializer.Deserialize<PendingGitApproval>(
            File.ReadAllText(path, ProjectHubJson.Utf8NoBom),
            ProjectHubJson.CompactOptions);
        if (pending is null ||
            !string.Equals(Path.GetFullPath(pending.WorkspaceRoot),
                Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GIT_APPROVAL_WORKSPACE_MISMATCH");
        return pending;
    }

    internal static void SavePendingGitApproval(string root, PendingGitApproval pending)
    {
        var path = PendingGitApprovalPath(root);
        if (File.Exists(path))
            throw new InvalidOperationException("GIT_APPROVAL_ALREADY_PENDING");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, ProjectHubJson.Serialize(pending),
                ProjectHubJson.Utf8NoBom);
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private async Task<MilestoneGitResult> ApprovePendingGitAsync(
        string root, string jobId, string? repositoryUrl,
        CancellationToken cancellationToken)
    {
        var pending = LoadPendingGitApproval(root) ??
            throw new InvalidOperationException("GIT_APPROVAL_NOT_PENDING");
        if (!string.Equals(pending.JobId, jobId, StringComparison.Ordinal))
            throw new InvalidOperationException("GIT_APPROVAL_JOB_MISMATCH");

        // HQ approval applies only to the unchanged evidence it just received.
        var now = await MilestoneMechanicalExecutor.SnapshotChangeStateAsync(
            root, cancellationToken, requireSuccess: true,
            scopedPaths: pending.Paths).ConfigureAwait(false);
        if (now.Count != pending.Snapshot.Count ||
            now.Any(pair => !pending.Snapshot.TryGetValue(pair.Key, out var previous) ||
                !string.Equals(previous, pair.Value, StringComparison.Ordinal)))
            throw new InvalidOperationException("GIT_APPROVAL_EVIDENCE_CHANGED");

        var git = await MilestoneMechanicalExecutor.FinalizeGitAsync(
            root, pending.Milestone, pending.Paths, repositoryUrl,
            cancellationToken).ConfigureAwait(false);
        if (git.Success)
            File.Delete(PendingGitApprovalPath(root));
        return git;
    }
}
