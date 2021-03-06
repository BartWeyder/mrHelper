﻿using mrHelper.Common.Interfaces;

namespace mrHelper.GitLabClient
{
   public class GitLabInstance
   {
      public GitLabInstance(string hostname, IHostProperties hostProperties)
      {
         HostProperties = hostProperties;
         HostName = hostname;
      }

      internal IHostProperties HostProperties { get; }
      internal string HostName { get; }
   }
}

