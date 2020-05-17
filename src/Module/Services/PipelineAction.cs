﻿using Microsoft.Extensions.DependencyInjection;
using Module.Services.Contracts;
using System;
using Transformalize;
using Transformalize.Contracts;
using Action = Transformalize.Configuration.Action;

namespace Module.Services {
   public class PipelineAction : IAction {

      private readonly IContext _context;
      private readonly IServiceProvider _serviceProvider;
      private readonly Action _action;
      public PipelineAction(IContext context, Action action, IServiceProvider serviceProvider) {
         _context = context;
         _action = action;
         _serviceProvider = serviceProvider;
      }
      public ActionResponse Execute() {
         var response = new ActionResponse() { Action = _action };

         var taskService = _serviceProvider.GetRequiredService<ITaskService>();

         if (!string.IsNullOrEmpty(_action.Name)) {
            var contentItem = taskService.GetByIdOrAliasAsync(_action.Name);
            if (contentItem.Result != null) {
               var process = taskService.LoadForTask(contentItem.Result, _context.Logger);
               taskService.RunAsync(process, _context.Logger);
               response.Code = process.Status;
               response.Message = process.Message;
            } else {
               response.Code = 404;
               response.Message = $"Could not find content item {_action.Name}.";
            }
         } else {
            response.Code = 500;
            response.Message = "Please specify tfl action name.  The name is the alias or content item id.";
         }

         return response;
      }
   }
}
