﻿using System;
using System.Diagnostics;
using mrHelper.Common.Tools;
using mrHelper.Common.Interfaces;
using mrHelper.Common.Exceptions;

namespace mrHelper.DiffTool
{
   /// <summary>
   /// Performs integration of the application into the specific DiffTool. Registers special difftool in git.
   /// </summary>
   public class DiffToolIntegration
   {
      public const string GitDiffToolName = "mrhelperdiff";

      /// <summary>
      /// Throws DiffToolNotInstalledException if diff tool is not installed
      /// Throws DiffToolIntegrationException if integration failed
      /// </summary>
      public void Integrate(IIntegratedDiffTool diffTool)
      {
         AppFinder.AppInfo appInfo = AppFinder.GetApplicationInfo(diffTool.GetToolRegistryNames());
         if (appInfo == null || !isInstalled(appInfo.InstallPath))
         {
            throw new DiffToolNotInstalledException("Diff tool not installed");
         }

         string toolpath = appInfo.InstallPath;
         Trace.TraceInformation(String.Format("Diff Tool installed at: {0}", toolpath));

         registerInGit(diffTool, GitDiffToolName, toolpath);

         try
         {
            registerInTool(diffTool);
         }
         catch (DiffToolIntegrationException)
         {
            Trace.TraceError(String.Format("Cannot register the application in \"{0}\"", GitDiffToolName));

            try
            {
               ExternalProcess.Start("git",
                  "config --global --remove-section difftool." + GitDiffToolName, true, String.Empty);
            }
            catch (Exception ex)
            {
               if (ex is ExternalProcessFailureException || ex is ExternalProcessSystemException)
               {
                  Trace.TraceError(String.Format("Cannot remove \"{0}\" from git config", GitDiffToolName));
               }
            }

            throw;
         }
      }

      /// <summary>
      /// Throws DiffToolIntegrationException if integration failed
      /// </summary>
      private void registerInTool(IIntegratedDiffTool diffTool)
      {
         try
         {
            diffTool.PatchToolConfig(Process.GetCurrentProcess().MainModule.FileName + " diff");
         }
         catch (DiffToolIntegrationException)
         {
            throw;
         }
         catch (Exception ex) // whatever XML exception
         {
            throw new DiffToolIntegrationException("Unknown error", ex);
         }
      }

      /// <summary>
      /// Throws ExternalProcessFailureException/ExternalProcessSystemException if registration failed
      /// </summary>
      private void registerInGit(IIntegratedDiffTool diffTool, string name, string toolpath)
      {
         ExternalProcess.Start("git",
            "config --global difftool." + name + ".cmd " + getGitCommand(diffTool, toolpath), true, String.Empty);
      }

      public bool isInstalled(string toolpath)
      {
         return !String.IsNullOrEmpty(toolpath);
      }

      private string getGitCommand(IIntegratedDiffTool diffTool, string toolPath)
      {
         var path = System.IO.Path.Combine(toolPath, diffTool.GetToolCommand());
         path = path.Replace(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
         return "\"\\\"" + path + "\\\"" + diffTool.GetToolCommandArguments() + "\"";
      }
   }
}

