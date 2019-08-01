using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace mrCore
{
   public class GitUtils
   {
      /// <summary>
      /// Launches 'git' with arguments passed and waits for process completion if needed.
      /// Returns StdOutput content if process exited with exit code 0, otherwise throws.
      /// </summary>
      static internal List<string> git(string arguments, bool wait)
      {
         List<string> output = new List<string>();
         List<string> errors = new List<string>();

         var process = new Process
         {
            StartInfo = new ProcessStartInfo
            {
               FileName = "git",
               Arguments = arguments,
               UseShellExecute = false,
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               CreateNoWindow = true
            }
         };

         process.Start();

         process.OutputDataReceived += (sender, args) => { if (args.Data != null) output.Add(args.Data); };
         process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errors.Add(args.Data); };

         process.BeginOutputReadLine();
         process.BeginErrorReadLine();

         int exitcode = 0;
         if (wait)
         {
            process.WaitForExit();
            exitcode = process.ExitCode;
         }
         else
         {
            System.Threading.Thread.Sleep(500); // ms
            if (process.HasExited)
            {
               exitcode = process.ExitCode;
            }
         }

         if (exitcode != 0)
         {
            throw new GitOperationException(arguments, exitcode, errors);
         }
         else if (errors.Count > 0)
         {
            Trace.TraceWarning(String.Format("\"git {0}\" returned exit code 0, but stderr is not empty:\n{1}",
               arguments, String.Join("\n", errors)));
         }
         return output;
      }

      /// <summary>
      /// Adds a difftool with the given name and command to the global git configuration.
      /// Throws GitOperationException in case of problems with git.
      /// </summary>
      static public void SetGlobalDiffTool(string name, string command)
      {
         // No need to change current directory because we're changing a global setting
         string arguments = "config --global difftool." + name + ".cmd " + command;
         git(arguments, true);
      }
      
      /// <summary>
      /// Removes a section for the difftool with the passed name from the global git configuration.
      /// Throws GitOperationException in case of problems with git.
      /// </summary>
      static public void RemoveGlobalDiffTool(string name)
      {
         // No need to change current directory because we're changing a global setting
         string arguments = "config --global --remove-section difftool." + name;
         git(arguments, true);
      }
   }
}

