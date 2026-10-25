using ProjectHub.Worker;

namespace ProjectHub.Worker.Tests;

public sealed class GitApprovalPersistenceTests
{
    [Fact]
    public void PendingApproval_SurvivesRoundTripAndCannotBeOverwritten()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "projecthub-git-approval-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var milestone = new MilestoneDefinition(
                "M1", "main", null, false, false, "", "[ACTION=WORK]",
                new Dictionary<string, MilestoneWorkDefinition>(),
                new Dictionary<string, MilestoneResourceDefinition>(),
                Array.Empty<string>())
            {
                SvInstructions = "조사 결과 통합",
                HighInstructions = "근거 검토"
            };
            var pending = new PendingGitApproval(
                "job", root, milestone, new[] { "docs/report.md" },
                new Dictionary<string, string> { ["docs/report.md"] = "FILE:1:123" });

            MainWindow.SavePendingGitApproval(root, pending);
            var recovered = MainWindow.LoadPendingGitApproval(root);

            Assert.NotNull(recovered);
            Assert.Equal("job", recovered.JobId);
            Assert.Equal("M1", recovered.Milestone.Id);
            Assert.Equal("근거 검토", recovered.Milestone.HighInstructions);
            Assert.Equal(new[] { "docs/report.md" }, recovered.Paths);
            Assert.Equal("FILE:1:123", recovered.Snapshot["docs/report.md"]);
            Assert.Throws<InvalidOperationException>(
                () => MainWindow.SavePendingGitApproval(root, pending));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
