using System;
using System.Diagnostics;
using System.Text;
using Jgrass.MSBuild.GitTask.Helper;
using Microsoft.Build.Framework;

namespace Jgrass.MSBuild.GitTask;

public class LargeFileInterceptTask : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// 使用此 Task 的项目的目录（用户的开发工程目录）
    /// </summary>
    public string MsBuildProjectDirectory { get; set; } = "";

    /// <summary>
    /// .targets 文件所在的目录，通常就是 nuget 缓存目录中的，此 nuget 包下面的 build 目录
    /// </summary>
    public string MsBuildThisFileDirectory { get; set; } = "";

    /// <summary>
    /// Git 提交大小硬限制，默认 10M 左右；会通过 pre-commit 拦截，造成提交失败。
    /// </summary>
    public string FileSizeHardLimit { get; set; } = "10000000";

    /// <summary>
    /// Git 提交大小软限制，默认 1M 左右；超过会进行提示。
    /// </summary>
    public string FileSizeSoftLimit { get; set; } = "1000000";

    /// <summary>
    /// 此 Task 否是开启
    /// </summary>
    public string Enable { get; set; } = "true";

    public override bool Execute()
    {
#if DEBUG
        Debugger.Launch();
#endif

        if (!string.IsNullOrWhiteSpace(Enable) && Enable.Trim().ToLower() != "true")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(MsBuildProjectDirectory))
        {
            Log.LogError("Cannot load msbuild project directory");
            return false;
        }

        if (string.IsNullOrWhiteSpace(MsBuildThisFileDirectory))
        {
            Log.LogError("Cannot load Jgrass.MSBuild.GitTask.targets file directory");
            return false;
        }

        Log.LogMessage(
            MessageImportance.High,
            $"[LargeFileInterceptTask] MsBuildProjectDirectory: {MsBuildProjectDirectory}"
        );
        Log.LogMessage(
            MessageImportance.High,
            $"[LargeFileInterceptTask] MsBuildThisFileDirectory: {MsBuildThisFileDirectory}"
        );

        Log.LogMessage(
            MessageImportance.High,
            $"[LargeFileInterceptTask] FileSizeHardLimit: {FileSizeHardLimit}; FileSizeSoftLimit: {FileSizeSoftLimit}"
        );

        try
        {
            return Run();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"[LargeFileInterceptTask] [Abort] [{ex.GetType().Name}] {ex.Message}");
        }

        return true;
    }

    private bool Run()
    {
        if (!uint.TryParse(FileSizeHardLimit, out var fileSizeHardLimitBytes))
        {
            Log.LogError(
                $"[LargeFileInterceptTask] Cannot parse GitLargeFileInterceptHardLimit. {FileSizeHardLimit}"
            );
            return false;
        }

        if (!uint.TryParse(FileSizeSoftLimit, out var fileSizeSoftLimitBytes))
        {
            Log.LogError(
                $"[LargeFileInterceptTask] Cannot parse GitLargeFileInterceptSoftLimit. {FileSizeSoftLimit}"
            );
            return false;
        }

        if (fileSizeHardLimitBytes < 1000)
        {
            Log.LogError(
                $"[LargeFileInterceptTask] GitLargeFileInterceptHardLimit must be greater than 1000 bytes. Your value is {FileSizeHardLimit}."
            );
            return false;
        }
        if (fileSizeSoftLimitBytes < 100)
        {
            Log.LogError(
                $"[LargeFileInterceptTask] GitLargeFileInterceptSoftLimit must be greater than 100 bytes. Your value is {FileSizeSoftLimit}."
            );
            return false;
        }

        if (fileSizeHardLimitBytes < fileSizeSoftLimitBytes)
        {
            Log.LogError(
                $"[LargeFileInterceptTask] GitLargeFileInterceptHardLimit must be greater than GitLargeFileInterceptSoftLimit. Your value is {FileSizeHardLimit} and {FileSizeSoftLimit}."
            );
            return false;
        }

        var gitDir = GetProjectGitDir(MsBuildProjectDirectory);
        if (string.IsNullOrEmpty(gitDir))
        {
            Log.LogError("[LargeFileInterceptTask] Cannot found .git dir of this project.");
            return false;
        }

        GitCommandExecutor.Run($"git config hooks.filesizehardlimit {fileSizeHardLimitBytes}");
        GitCommandExecutor.Run($"git config hooks.filesizesoftlimit {fileSizeSoftLimitBytes}");

        var preCommitContent = GetCurrentGitHookPreCommit(gitDir);
        var templatePreCommitContent = GetTemplatePreCommitContent(MsBuildThisFileDirectory);

        // 如果没有配置 pre-commit，则直接使用模板 pre-commit 创建
        if (string.IsNullOrWhiteSpace(preCommitContent))
        {
            var targetPreCommitFile = Path.Combine(gitDir, "hooks", "pre-commit");
            WriteFileContent(targetPreCommitFile, templatePreCommitContent);

            Log.LogMessage(
                MessageImportance.High,
                $"[LargeFileInterceptTask] Write git hook pre-commit file success."
            );
            return true;
        }
        // 如果有配置 pre-commit，则尝试合并已经存在的 pre-commit 和 pre-commit 模板
        else
        {
            if (MergePreCommit(templatePreCommitContent, preCommitContent, out var newContent))
            {
                var targetPreCommitFile = Path.Combine(gitDir, "hooks", "pre-commit");
                WriteFileContent(targetPreCommitFile, newContent);

                Log.LogMessage(
                    MessageImportance.High,
                    $"[LargeFileInterceptTask] Update git hook pre-commit file success."
                );
                return true;
            }
        }

        Log.LogMessage(MessageImportance.High, $"[LargeFileInterceptTask] Finish.");
        return true;
    }

    /// <summary>
    /// 获取项目的 .git 目录
    /// </summary>
    /// <param name="projectDir"></param>
    /// <returns></returns>
    /// <exception cref="DirectoryNotFoundException"></exception>
    private string GetProjectGitDir(string projectDir)
    {
        var directoryInfo = new DirectoryInfo(projectDir);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not exist. {projectDir}");
        }

        var directories = Directory.GetDirectories(projectDir);
        foreach (var directory in directories)
        {
            var name = new DirectoryInfo(directory).Name;
            if (name.ToLower() == ".git")
            {
                return directory;
            }
        }

        var parentDir = directoryInfo.Parent;
        if (parentDir == null || !parentDir.Exists)
        {
            return "";
        }

        return GetProjectGitDir(parentDir.FullName);
    }

    /// <summary>
    /// 获取当前 git 配置的 pre-commit hook 的内容，没有则返回空
    /// </summary>
    /// <param name="gitDir"></param>
    /// <returns></returns>
    private string GetCurrentGitHookPreCommit(string gitDir)
    {
        var file = Path.Combine(gitDir, "hooks", "pre-commit");
        if (File.Exists(file))
        {
            return File.ReadAllText(file);
        }
        else
        {
            return "";
        }
    }

    /// <summary>
    /// 获取模板 pre-commit 文件内容
    /// </summary>
    /// <param name="nugetBuildDir"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    private string GetTemplatePreCommitContent(string nugetBuildDir)
    {
        var templateFile = Path.Combine(nugetBuildDir, "..", "scripts", "pre-commit");
        if (!File.Exists(templateFile))
        {
            throw new FileNotFoundException($"Template pre-commit file not found. {templateFile}");
        }

        return File.ReadAllText(templateFile);
    }

    /// <summary>
    /// 合并当前 pre-commit 和模板 pre-commit 的内容
    /// </summary>
    /// <param name="templatePreCommitContent"></param>
    /// <param name="preCommitContent"></param>
    /// <param name="content"></param>
    /// <returns>false: 无内容更新，true: 有内容更新</returns>
    private bool MergePreCommit(
        string templatePreCommitContent,
        string preCommitContent,
        out string content
    )
    {
        if (templatePreCommitContent == preCommitContent)
        {
            content = "";
            return false;
        }

        if (preCommitContent.Contains("list_new_or_modified_files | check_file_size"))
        {
            content = "";
            return false;
        }

        var templatePreCommitContentWithoutShebang = templatePreCommitContent.Substring(
            "#!/bin/sh".Length
        );

        content = preCommitContent + Environment.NewLine + templatePreCommitContentWithoutShebang;

        return true;
    }

    private void WriteFileContent(string file, string content)
    {
        using var writer = new StreamWriter(file, false, new UTF8Encoding(false));
        writer.Write(content);
    }
}
