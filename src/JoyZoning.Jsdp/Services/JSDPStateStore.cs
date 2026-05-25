using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JSDPStateStore
{
    private readonly string _workspaceRoot;

    public JSDPStateStore(string workspaceRoot) => _workspaceRoot = workspaceRoot;

    public string WorkspaceRoot => _workspaceRoot;

    public bool IsInitialized => File.Exists(JsdpPaths.Run(_workspaceRoot));

    public void EnsureLayout()
    {
        Directory.CreateDirectory(JsdpPaths.Root(_workspaceRoot));
        Directory.CreateDirectory(JsdpPaths.Prompts(_workspaceRoot));
        Directory.CreateDirectory(JsdpPaths.Reports(_workspaceRoot));
        Directory.CreateDirectory(JsdpPaths.State(_workspaceRoot));
    }

    public JsdpRun LoadRun()
    {
        var path = JsdpPaths.Run(_workspaceRoot);
        if (!File.Exists(path))
            throw new JsdpException("No JSDP run found. Run: joyzoning jsdp init \"<goal>\"");
        return JsdpJson.ReadFile<JsdpRun>(path);
    }

    public void SaveRun(JsdpRun run)
    {
        EnsureLayout();
        JsdpJson.WriteFile(JsdpPaths.Run(_workspaceRoot), run);
        SaveTree(run);
    }

    public ProjectSpecDocument LoadProjectSpec()
    {
        var path = JsdpPaths.ProjectSpec(_workspaceRoot);
        if (!File.Exists(path))
            throw new JsdpException("No project spec found. Run: joyzoning jsdp init or analyze");
        return JsdpJson.ReadFile<ProjectSpecDocument>(path);
    }

    public void SaveProjectSpec(ProjectSpecDocument spec)
    {
        EnsureLayout();
        JsdpJson.WriteFile(JsdpPaths.ProjectSpec(_workspaceRoot), spec);
    }

    public JsdpConfig LoadConfig()
    {
        var path = JsdpPaths.Config(_workspaceRoot);
        if (!File.Exists(path))
            return new JsdpConfig();
        return JsdpJson.ReadFile<JsdpConfig>(path);
    }

    public void SaveConfig(JsdpConfig config)
    {
        EnsureLayout();
        JsdpJson.WriteFile(JsdpPaths.Config(_workspaceRoot), config);
    }

    public void SaveTree(JsdpRun run)
    {
        var tree = PromptDAGBuilder.BuildTreeDocument(run);
        JsdpJson.WriteFile(JsdpPaths.Tree(_workspaceRoot), tree);
    }

    public JsdpTreeDocument LoadTree()
    {
        var path = JsdpPaths.Tree(_workspaceRoot);
        if (!File.Exists(path))
            throw new JsdpException("No DAG tree found. Run: joyzoning jsdp plan");
        return JsdpJson.ReadFile<JsdpTreeDocument>(path);
    }
}

public sealed class JsdpException : Exception
{
    public JsdpException(string message) : base(message) { }
}
