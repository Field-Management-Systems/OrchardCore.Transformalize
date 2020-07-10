﻿#region license
// Transformalize
// Configurable Extract, Transform, and Load
// Copyright 2013-2017 Dale Newman
//  
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   
//       http://www.apache.org/licenses/LICENSE-2.0
//   
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using Autofac;
using Cfg.Net.Contracts;
using Cfg.Net.Environment;
using Microsoft.AspNetCore.Http;
using TransformalizeModule.Services.Contracts;
using TransformalizeModule.Services.Modifiers;
using TransformalizeModule.Services.Modules;
using OrchardCore.Users.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Transformalize.Containers.Autofac.Modules;
using Process = Transformalize.Configuration.Process;

namespace TransformalizeModule.Services {

   /// <summary>
   /// This container deals with the arrangement and how it becomes a process.
   /// Transforms and Validators are registered here as well because their 
   /// short-hand is expanded in the arrangement by customizers before it becomes a process.
   /// </summary>
   public class OrchardConfigurationContainer : IConfigurationContainer {

      private readonly IUserService _userService;
      private readonly IHttpContextAccessor _httpContext;
      private readonly CombinedLogger<OrchardConfigurationContainer> _logger;
      private readonly ITransformalizeParametersModifier _transformalizeParameters;

      public ISerializer Serializer { get; set; }

      public OrchardConfigurationContainer(
         IHttpContextAccessor httpContext,
         CombinedLogger<OrchardConfigurationContainer> logger,
         IUserService userService,
         ITransformalizeParametersModifier transformalizeParameters
      ) {
         _userService = userService;
         _httpContext = httpContext;
         _logger = logger;
         _transformalizeParameters = transformalizeParameters;
      }

      public ILifetimeScope CreateScope(string arrangement, int contentItemId, IDictionary<string, string> parameters = null) {

         var placeHolderStyle = "@[]";

         if (parameters == null) {
            parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         }

         var builder = new ContainerBuilder();
         builder.RegisterModule(new ShorthandModule(_logger));

         builder.Register(ctx => {

            var response = _transformalizeParameters.Modify(arrangement);

            var cfg = response.Arrangement;
            foreach(var kv in response.Parameters) {
               parameters[kv.Key] = kv.Value;
            }

            var dependancies = new List<IDependency>();
            dependancies.Add(ctx.Resolve<IReader>());
            dependancies.Add(new ReportParameterModifier());
            if (Serializer != null) {
               dependancies.Add(Serializer);
            }
            dependancies.Add(new ParameterModifier(new PlaceHolderReplacer(placeHolderStyle[0], placeHolderStyle[1], placeHolderStyle[2]), "parameters", "name", "value"));
            dependancies.Add(ctx.ResolveNamed<IDependency>(TransformModule.FieldsName));
            dependancies.Add(ctx.ResolveNamed<IDependency>(TransformModule.ParametersName));
            dependancies.Add(ctx.ResolveNamed<IDependency>(ValidateModule.FieldsName));
            dependancies.Add(ctx.ResolveNamed<IDependency>(ValidateModule.ParametersName));

            var process = new Process(cfg, parameters, dependancies.ToArray());

            if (process.Errors().Any()) {
               _logger.Error(() => "The configuration has errors.");
               foreach (var error in process.Errors()) {
                  _logger.Error(() => error);
               }
            }

            process.Id = contentItemId;

            return process;
         }).As<Process>().InstancePerDependency();
         return builder.Build().BeginLifetimeScope();
      }

   }

}
