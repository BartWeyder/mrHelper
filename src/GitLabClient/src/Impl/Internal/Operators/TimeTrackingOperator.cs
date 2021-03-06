using System;
using System.Threading.Tasks;
using GitLabSharp.Accessors;
using mrHelper.Common.Interfaces;

namespace mrHelper.GitLabClient.Operators
{
   /// <summary>
   /// Implements Time-Tracking-related interaction with GitLab
   /// </summary>
   internal class TimeTrackingOperator : BaseOperator
   {
      internal TimeTrackingOperator(string hostname, IHostProperties settings)
         : base(hostname, settings)
      {
      }

      internal Task AddSpanAsync(bool add, TimeSpan span, MergeRequestKey mrk)
      {
         return callWithSharedClient(
            async (client) =>
               await OperatorCallWrapper.Call(
                  async () =>
                     await client.RunAsync(
                        async (gl) =>
                           await gl.Projects.Get(mrk.ProjectKey.ProjectName).MergeRequests.Get(mrk.IId).AddSpentTimeAsync(
                              new AddSpentTimeParameters(add, span)))));
      }
   }
}

