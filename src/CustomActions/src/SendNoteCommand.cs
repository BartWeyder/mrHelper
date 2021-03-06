﻿using System;
using System.Threading.Tasks;
using GitLabSharp;
using GitLabSharp.Accessors;
using GitLabSharp.Entities;
using mrHelper.Common.Interfaces;
using mrHelper.Common.Exceptions;

namespace mrHelper.CustomActions
{
   public class SendNoteCommand : ICommand
   {
      public SendNoteCommand(ICommandCallback callback, string name, string body, string dependency,
         bool stopTimer, bool reload, string hint)
      {
         _callback = callback;
         _name = name;
         _body = body;
         _dependency = dependency;
         _stopTimer = stopTimer;
         _reload = reload;
         _hint = hint;
      }

      public string GetName()
      {
         return _name;
      }

      public string GetBody()
      {
         return _body;
      }

      public string GetDependency()
      {
         return _dependency;
      }

      public bool GetStopTimer()
      {
         return _stopTimer;
      }

      public bool GetReload()
      {
         return _reload;
      }

      public string GetHint()
      {
         return _hint;
      }

      async public Task Run()
      {
         string hostname = _callback.GetCurrentHostName();
         string accessToken = _callback.GetCurrentAccessToken();
         string projectName = _callback.GetCurrentProjectName();
         int iid = _callback.GetCurrentMergeRequestIId();

         GitLabTaskRunner client = new GitLabTaskRunner(hostname, accessToken);
         await client.RunAsync(async (gitlab) =>
            await gitlab.Projects.Get(projectName).MergeRequests.
               Get(iid).Notes.CreateNewTaskAsync(new CreateNewNoteParameters(_body)));
      }

      private readonly ICommandCallback _callback;
      private readonly string _name;
      private readonly string _body;
      private readonly string _dependency;
      private readonly bool _stopTimer;
      private readonly bool _reload;
      private readonly string _hint;
   }
}

