﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using mrHelper.Common.Exceptions;
using mrHelper.Common.Interfaces;
using mrHelper.Common.Tools;

namespace mrHelper.GitClient
{
   public static class GitTools
   {
      public class SSLVerificationDisableException : ExceptionEx
      {
         internal SSLVerificationDisableException(Exception innerException)
            : base(String.Empty, innerException)
         {
         }
      }

      public static void DisableSSLVerification()
      {
         try
         {
            ExternalProcess.Start("git", "config --global http.sslVerify false", true, String.Empty);
         }
         catch (Exception ex)
         {
            if (ex is ExternalProcessFailureException || ex is ExternalProcessSystemException)
            {
               throw new SSLVerificationDisableException(ex);
            }
            throw;
         }
      }

      public static class GitVersionAccessor
      {
         public class UnknownVersionException : ExceptionEx
         {
            internal UnknownVersionException(Exception innerException)
               : base(String.Empty, innerException)
            {
            }
         }

         public static Version GetVersion()
         {
            if (_cachedVersion == null)
            {
               _cachedVersion = getVersion();
            }
            return _cachedVersion;
         }

         private static Version _cachedVersion;

         private static Version getVersion()
         {
            try
            {
               IEnumerable<string> stdOut =
                  ExternalProcess.Start("git", "--version", true, String.Empty).StdOut;
               if (!stdOut.Any())
               {
                  throw new UnknownVersionException(null);
               }

               Match m = gitVersion_re.Match(stdOut.First());
               if (!m.Success || m.Groups.Count < 3
                || !m.Groups["major"].Success || !m.Groups["minor"].Success || !m.Groups["build"].Success
                || !int.TryParse(m.Groups["major"].Value, out int major)
                || !int.TryParse(m.Groups["minor"].Value, out int minor)
                || !int.TryParse(m.Groups["build"].Value, out int build))
               {
                  throw new UnknownVersionException(null);
               }

               return new Version(major, minor, build);
            }
            catch (Exception ex)
            {
               if (ex is ExternalProcessFailureException || ex is ExternalProcessSystemException)
               {
                  throw new UnknownVersionException(ex);
               }
               throw;
            }
         }

         private static readonly Regex gitVersion_re =
            new Regex(@"git version (?'major'\d+).(?'minor'\d+).(?'build'\d+)");
      }

      public static bool SupportsFetchAutoGC()
      {
         try
         {
            Version version = GitVersionAccessor.GetVersion();
            return version.Major > 2 || (version.Major == 2 && version.Minor >= 23);
         }
         catch (GitVersionAccessor.UnknownVersionException ex)
         {
            ExceptionHandlers.Handle("Cannot detect git version", ex);
         }
         return false;
      }

      public static void TraceGitConfiguration()
      {
         try
         {
            foreach (string arguments in
               new string[]{ "--version", "config --global --list", "config --system --list" })
            {
               IEnumerable<string> stdOut = ExternalProcess.Start("git", arguments, true, String.Empty).StdOut;
               if (stdOut.Any())
               {
                  Trace.TraceInformation(String.Format("git {0} ==>\n{1}", arguments, String.Join("\n", stdOut)));
               }
            }
         }
         catch (Exception ex)
         {
            if (ex is ExternalProcessFailureException || ex is ExternalProcessSystemException)
            {
               ExceptionHandlers.Handle("Cannot trace git configuration", ex);
            }
         }
      }

      public static bool IsValidGitRepository(string path)
      {
         try
         {
            return Directory.Exists(path)
               && ExternalProcess.Start("git", "rev-parse --is-inside-work-tree", true, path).StdErr.Count() == 0;
         }
         catch (Exception ex)
         {
            if (ex is ExternalProcessFailureException || ex is ExternalProcessSystemException)
            {
               return false;
            }
            else
            {
               throw;
            }
         }
      }

      public static bool DoesEntityExistAtPath(string path, string entity)
      {
         try
         {
            return ExternalProcess.Start("git", String.Format("cat-file -t {0}", entity), true, path)
               .StdErr.Count() == 0;
         }
         catch (Exception ex)
         {
            if (ex is ExternalProcessFailureException || ex is ExternalProcessSystemException)
            {
               return false;
            }
            throw;
         }
      }

      public static ProjectKey? GetRepositoryProjectKey(string path)
      {
         string repositoryName = getRepositoryName(path);
         if (String.IsNullOrWhiteSpace(repositoryName))
         {
            return null;
         }

         Match m = gitRepo_re.Match(repositoryName);
         if (m.Success && m.Groups.Count == 5 && m.Groups[3].Success && m.Groups[4].Success)
         {
            string hostname = StringUtils.GetHostWithPrefix(m.Groups[3].Value);

            string gitSuffix = ".git";
            int startIndex = m.Groups[4].Value.StartsWith(":") ? 1 : 0;
            int endIndex = m.Groups[4].Value.EndsWith(gitSuffix)
               ? m.Groups[4].Value.Length - gitSuffix.Length : m.Groups[4].Value.Length;

            string project = m.Groups[4].Value.Substring(startIndex, endIndex - startIndex);
            return new ProjectKey
            {
               HostName = hostname,
               ProjectName = project
            };
         }

         return null;
      }

      public static bool IsSingleCommitFetchSupported(string path)
      {
         // TODO
         // Check if it is possible to run commands like `git fetch origin <sha>:refs/keep-around/sha`
         return true;
      }

      private static string getRepositoryName(string path)
      {
         try
         {
            IEnumerable<string> stdOut =
               ExternalProcess.Start("git", "config --get remote.origin.url", true, path).StdOut;
            return stdOut.Any() ? stdOut.First() : null;
         }
         catch (Exception ex)
         {
            if (ex is ExternalProcessFailureException || ex is ExternalProcessSystemException)
            {
               return null;
            }
            else
            {
               throw;
            }
         }
      }

      // from https://stackoverflow.com/a/2514986/9195131
      private static string GitRepositoryRegularExpression = @"(\w+://)?(.+@)*([\w\d\.]+)/*(.*)";
      private static readonly Regex gitRepo_re = new Regex(GitRepositoryRegularExpression,
         RegexOptions.Compiled | RegexOptions.IgnoreCase);
   }
}

