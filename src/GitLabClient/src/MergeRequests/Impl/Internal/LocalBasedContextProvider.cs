﻿using System;
using System.Linq;
using System.Collections.Generic;
using mrHelper.Common.Interfaces;
using Version = GitLabSharp.Entities.Version;
using System.Diagnostics;

namespace mrHelper.Client.MergeRequests
{
   /// <summary>
   /// Detects the latest change among given versions
   /// </summary>
   internal class LocalBasedContextProvider : ICommitStorageUpdateContextProvider
   {
      internal LocalBasedContextProvider(IEnumerable<Version> versions)
      {
         _versions = versions;
      }

      public CommitStorageUpdateContext GetContext()
      {
         if (_versions == null)
         {
            Debug.Assert(false);
            return null;
         }

         if (!_versions.Any())
         {
            return null;
         }

         HashSet<string> shas = new HashSet<string>();
         foreach (Version version in _versions)
         {
            if (version.Base_Commit_SHA != null)
            {
               shas.Add(version.Base_Commit_SHA);
            }
            if (version.Head_Commit_SHA != null)
            {
               shas.Add(version.Head_Commit_SHA);
            }
         }

         return new FullUpdateContext(_versions.OrderBy(x => x.Created_At).LastOrDefault().Created_At, shas);
      }

      public override string ToString()
      {
         return String.Format("LocalBasedContextProvider. Version Count: {0}", _versions.Count());
      }

      private readonly IEnumerable<Version> _versions;
   }
}

