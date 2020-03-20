﻿using System;
using System.Collections.Generic;
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

      public static class GitVersion
      {
         public class UnknownVersionException : ExceptionEx
         {
            internal UnknownVersionException(Exception innerException)
               : base(String.Empty, innerException)
            {
            }
         }

         public static Version Get()
         {
            if (_version == null)
            {
               _version = getVersion();
            }
            return _version;
         }

         private static Version _version;

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
            Version version = GitVersion.Get();
            return version.Major > 2 || (version.Major == 2 && version.Minor >= 23);
         }
         catch (GitVersion.UnknownVersionException ex)
         {
            ExceptionHandlers.Handle("Cannot detect git version", ex);
         }
         return false;
      }
   }
}

