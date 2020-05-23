﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using mrHelper.Common.Interfaces;
using Version = GitLabSharp.Entities.Version;
using System.Diagnostics;

namespace mrHelper.Client.MergeRequests
{
   /// <summary>
   /// Detects the latest change among given versions
   /// </summary>
   internal class LocalBasedContextProvider : IProjectUpdateContextProvider
   {
      internal LocalBasedContextProvider(IEnumerable<Version> versions)
      {
         _versions = versions;
      }

      public Task<IProjectUpdateContext> GetContext()
      {
         if (_versions == null)
         {
            Debug.Assert(false);
            return Task.FromResult<IProjectUpdateContext>(null);
         }

         if (!_versions.Any())
         {
            return Task.FromResult<IProjectUpdateContext>(null);
         }

         List<string> shas = new List<string>();
         foreach (Version version in _versions)
         {
            shas.Add(version.Base_Commit_SHA);
            shas.Add(version.Head_Commit_SHA);
         }

         FullUpdateContext update = new FullUpdateContext(
            _versions.OrderBy(x => x.Created_At).LastOrDefault().Created_At, shas);
         return Task.FromResult(update as IProjectUpdateContext);
      }

      public override string ToString()
      {
         return String.Format("LocalBasedContextProvider. Version Count: {0}", _versions.Count());
      }

      private readonly IEnumerable<Version> _versions;
   }
}
