﻿using System;
using System.Collections.Generic;
using System.Text;
using CliWrap;

namespace Jgrass.MSBuild.GitTask.Helper;

internal class GitCommandExecutor
{
    public static void Run(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentNullException(nameof(command), "Not set any git command.");
        }

        var args = command.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

        if (args.Length < 1)
        {
            throw new InvalidOperationException("Invalid git command.");
        }

        if (args[0].ToLower() == "git")
        {
            args = args.Skip(1).ToArray();
        }

        if (args.Length < 1)
        {
            throw new InvalidOperationException("Invalid git command.");
        }

        var cmd = Cli.Wrap("git").WithArguments(args);
        cmd.ExecuteAsync().Task.Wait();
    }
}
