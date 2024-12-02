using System;
using System.Collections.Generic;
using System.Text;

namespace Jgrass.MSBuild.GitTask.Helper;

public class WindowsGitBashExecutor
{
    /// <summary>
    /// 获取本机安装的 git 所对应的 bash.exe 执行文件
    /// </summary>
    /// <returns></returns>
    public string GetGitBashFile()
    {
        var gitFile = FindGitExecutable();
        var bashFile = FindGitBashFile(gitFile);

        return "";
    }

    private static string FindGitExecutable()
    {
        // 获取 PATH 环境变量
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            throw new InvalidOperationException("Cannot get PATH environment variable of system");
        }

        // 将 PATH 按照分号分割成各个路径
        string[] paths = pathEnv.Split(';');

        // 遍历每个路径，查找 git.exe
        foreach (string path in paths)
        {
            if (!path.ToLower().Contains("git"))
            {
                continue;
            }
            string gitExecutable = Path.Combine(path, "git.exe");

            if (File.Exists(gitExecutable))
            {
                return gitExecutable; // 返回找到的 git.exe 路径
            }
        }

        throw new InvalidOperationException(
            "Cannot get git.exe in environment variable of system, please install git or add to environment variable"
        );
    }

    private static string FindGitBashFile(string gitFile)
    {
        var folder = Path.GetDirectoryName(gitFile);
        if (folder == null)
        {
            throw new InvalidOperationException($"Cannot get git.exe parent dir. {gitFile}");
        }

        var bashFile = Path.Combine(folder, "../bin/bash.exe");

        if (!File.Exists(bashFile))
        {
            throw new InvalidOperationException(
                $"Cannot find git bash.exe in {bashFile}, try update git client software version"
            );
        }

        return Path.GetFullPath(bashFile);
    }
}
