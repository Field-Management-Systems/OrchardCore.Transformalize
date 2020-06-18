#region license
// Transformalize
// Configurable Extract, Transform, and Load
// Copyright 2013-2020 Dale Newman
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
using Cfg.Net.Shorthand;
using Microsoft.AspNetCore.Http;
using Module.Services.Modules;
using Module.Transforms;
using System;
using System.Collections.Generic;
using System.Linq;
using Transformalize.Containers.Autofac;
using Transformalize.Containers.Autofac.Modules;
using Transformalize.Context;
using Transformalize.Contracts;
using Transformalize.Impl;
using Transformalize.Nulls;
using Transformalize.Transforms.Humanizer.Autofac;
using Transformalize.Transforms.Jint.Autofac;
using Transformalize.Transforms.Json.Autofac;
using Transformalize.Transforms.System;
using Transformalize.Validate.Jint.Autofac;
using Transformalize.Providers.Ado.Autofac;
using Transformalize.Providers.Bogus.Autofac;
using Transformalize.Providers.CsvHelper.Autofac;
using Transformalize.Providers.Elasticsearch.Autofac;
using Transformalize.Providers.Json.Autofac;
using Transformalize.Providers.MySql;
using Transformalize.Providers.MySql.Autofac;
using Transformalize.Providers.PostgreSql;
using Transformalize.Providers.PostgreSql.Autofac;
using Transformalize.Providers.Sqlite.Autofac;
using Transformalize.Providers.SQLite;
using Transformalize.Providers.SqlServer;
using Transformalize.Providers.SqlServer.Autofac;
using LogTransform = Transformalize.Transforms.System.LogTransform;
using Process = Transformalize.Configuration.Process;
using System.Data;
using OrchardCore.Users.Services;
using Transformalize.Providers.File.Autofac;
using Transformalize.Transforms.Ado.Autofac;
using Transformalize.Actions;

namespace Module.Services {

   public class OrchardContainer : IContainer {

      private readonly HashSet<string> _methods = new HashSet<string>();
      private readonly ShorthandRoot _shortHand = new ShorthandRoot();
      private readonly IHttpContextAccessor _httpContext;
      private readonly IUserService _userService;
      private readonly IServiceProvider _serviceProvider;
      private readonly HashSet<string> _adoProviders = new HashSet<string>() { "sqlserver", "postgresql", "sqlite", "mysql" };

      public OrchardContainer(
         IHttpContextAccessor httpContext,
         IUserService userService,
         IServiceProvider serviceProvider
      ) {
         _httpContext = httpContext;
         _userService = userService;
         _serviceProvider = serviceProvider;
      }

      public ILifetimeScope CreateScope(Process process, IPipelineLogger logger) {

         var builder = new ContainerBuilder();
         builder.Properties["Process"] = process;

         builder.Register(ctx => process).As<Process>();
         builder.RegisterInstance(logger).As<IPipelineLogger>().SingleInstance();

         // register short-hand for t attribute, allowing for additional transforms
         var tm = new TransformModule(process, _methods, _shortHand, logger);
         // adding additional transforms here
         tm.AddTransform(new TransformHolder((c) => new UsernameTransform(_httpContext, c), new UsernameTransform().GetSignatures()));
         tm.AddTransform(new TransformHolder((c) => new UserIdTransform(_httpContext, _userService, c), new UserIdTransform().GetSignatures()));
         tm.AddTransform(new TransformHolder((c) => new UserEmailTransform(_httpContext, _userService, c), new UserEmailTransform().GetSignatures()));
         builder.RegisterModule(tm);

         // register short-hand for v attribute, allowing for additional validators
         var vm = new ValidateModule(process, _methods, _shortHand, logger);
         // adding additional validators here
         builder.RegisterModule(vm);

         // using custom internal module that does not handle the nested transformalize actions
         builder.RegisterModule(new OrchardInternalModule(process));

         // handling nested transformalize actions here instead
         foreach (var action in process.Actions.Where(a => a.GetModes().Any(m => m == process.Mode || m == "*"))) {
            if (action.Type == "tfl") {
               builder.Register<IAction>(ctx => {
                  return new PipelineAction(action, _serviceProvider);
               }).Named<IAction>(action.Key);
            }
         }

         // register providers
         var providers = new HashSet<string>(process.Connections.Select(c => c.Provider));

         // ADO
         builder.RegisterModule(new AdoProviderModule());
         if (providers.Contains("sqlserver")) { builder.RegisterModule(new SqlServerModule() { ConnectionFactory = (c) => new ProfiledConnectionFactory(new SqlServerConnectionFactory(c)) }); }
         if (providers.Contains("postgresql")) { builder.RegisterModule(new PostgreSqlModule() { ConnectionFactory = (c) => new ProfiledConnectionFactory(new PostgreSqlConnectionFactory(c)) }); }
         if (providers.Contains("sqlite")) { builder.RegisterModule(new SqliteModule() { ConnectionFactory = (c) => new ProfiledConnectionFactory(new SqliteConnectionFactory(c)) }); }
         if (providers.Contains("mysql")) { builder.RegisterModule(new MySqlModule() { ConnectionFactory = (c) => new ProfiledConnectionFactory(new MySqlConnectionFactory(c)) }); }

         if (providers.Contains("bogus")) { builder.RegisterModule(new BogusModule()); }
         if (providers.Contains("file")) { builder.RegisterModule(new CsvHelperProviderModule(_httpContext.HttpContext.Response.Body)); }
         if (providers.Contains("json")) { builder.RegisterModule(new JsonProviderModule(_httpContext.HttpContext.Response.Body)); }
         if (providers.Contains("elasticsearch")) { builder.RegisterModule(new ElasticsearchModule()); }
         // solr
         // lucene

         // just in case other modules need to see these
         builder.Properties["ShortHand"] = _shortHand;
         builder.Properties["Methods"] = _methods;

         // register transform modules here
         builder.RegisterModule(new JintTransformModule());
         builder.RegisterModule(new JsonTransformModule());
         builder.RegisterModule(new HumanizeModule());
         builder.RegisterModule(new FileModule());
         builder.RegisterModule(new AdoTransformModule());

         // register validator modules here
         builder.RegisterModule(new JintValidateModule());

         // process context
         builder.Register<IContext>((ctx, p) => new PipelineContext(logger, process)).As<IContext>();

         // process output context
         builder.Register(ctx => {
            var context = ctx.Resolve<IContext>();
            return new OutputContext(context);
         }).As<OutputContext>();

         // connection and process level output context
         foreach (var connection in process.Connections) {

            builder.Register(ctx => new ConnectionContext(ctx.Resolve<IContext>(), connection)).Named<IConnectionContext>(connection.Key);

            if (connection.Name != "output")
               continue;

            // register output for connection
            builder.Register(ctx => {
               var context = ctx.ResolveNamed<IConnectionContext>(connection.Key);
               return new OutputContext(context);
            }).Named<OutputContext>(connection.Key);

         }

         // entity context and rowFactory
         foreach (var entity in process.Entities) {
            builder.Register<IContext>((ctx, p) => new PipelineContext(ctx.Resolve<IPipelineLogger>(), process, entity)).Named<IContext>(entity.Key);

            builder.Register(ctx => {
               var context = ctx.ResolveNamed<IContext>(entity.Key);
               return new InputContext(context);
            }).Named<InputContext>(entity.Key);

            builder.Register<IRowFactory>((ctx, p) => new RowFactory(p.Named<int>("capacity"), entity.IsMaster, false)).Named<IRowFactory>(entity.Key);

            builder.Register(ctx => {
               var context = ctx.ResolveNamed<IContext>(entity.Key);
               return new OutputContext(context);
            }).Named<OutputContext>(entity.Key);

            var connection = process.Connections.First(c => c.Name == entity.Input);
            builder.Register(ctx => new ConnectionContext(ctx.Resolve<IContext>(), connection)).Named<IConnectionContext>(entity.Key);

         }

         // entity pipelines
         foreach (var entity in process.Entities) {
            builder.Register(ctx => {

               var context = ctx.ResolveNamed<IContext>(entity.Key);
               var outputController = ctx.IsRegisteredWithName<IOutputController>(entity.Key) ? ctx.ResolveNamed<IOutputController>(entity.Key) : new NullOutputController();
               var pipeline = new DefaultPipeline(outputController, context);

               // TODO: rely on IInputProvider's Read method instead (after every provider has one)
               pipeline.Register(ctx.IsRegisteredWithName(entity.Key, typeof(IRead)) ? ctx.ResolveNamed<IRead>(entity.Key) : null);
               pipeline.Register(ctx.IsRegisteredWithName(entity.Key, typeof(IInputProvider)) ? ctx.ResolveNamed<IInputProvider>(entity.Key) : null);

               // transforms
               if (!process.ReadOnly) {
                  pipeline.Register(new SetSystemFields(new PipelineContext(ctx.Resolve<IPipelineLogger>(), process, entity)));
               }

               pipeline.Register(new IncrementTransform(context));
               pipeline.Register(new DefaultTransform(context, context.GetAllEntityFields().Where(f => !f.System)));
               pipeline.Register(TransformFactory.GetTransforms(ctx, context, entity.GetAllFields().Where(f => f.Transforms.Any())));
               pipeline.Register(ValidateFactory.GetValidators(ctx, context, entity.GetAllFields().Where(f => f.Validators.Any())));

               if (!process.ReadOnly) {
                  pipeline.Register(new StringTruncateTransfom(new PipelineContext(ctx.Resolve<IPipelineLogger>(), process, entity)));
               }

               pipeline.Register(new LogTransform(context));

               // writer, TODO: rely on IOutputProvider instead
               pipeline.Register(ctx.IsRegisteredWithName(entity.Key, typeof(IWrite)) ? ctx.ResolveNamed<IWrite>(entity.Key) : null);
               pipeline.Register(ctx.IsRegisteredWithName(entity.Key, typeof(IOutputProvider)) ? ctx.ResolveNamed<IOutputProvider>(entity.Key) : null);

               // updater
               pipeline.Register(process.ReadOnly || !ctx.IsRegisteredWithName(entity.Key, typeof(IUpdate)) ? new NullUpdater() : ctx.ResolveNamed<IUpdate>(entity.Key));

               return pipeline;

            }).Named<IPipeline>(entity.Key);
         }


         // process pipeline
         builder.Register(ctx => {

            var calc = process.ToCalculatedFieldsProcess();
            var entity = calc.Entities.First();

            var context = new PipelineContext(ctx.Resolve<IPipelineLogger>(), calc, entity);
            var outputContext = new OutputContext(context);

            context.Debug(() => $"Registering {process.Pipeline} pipeline.");
            var outputController = ctx.IsRegistered<IOutputController>() ? ctx.Resolve<IOutputController>() : new NullOutputController();
            var pipeline = new DefaultPipeline(outputController, context);

            // no updater necessary
            pipeline.Register(new NullUpdater(context, false));

            if (!process.CalculatedFields.Any()) {
               pipeline.Register(new NullReader(context, false));
               pipeline.Register(new NullWriter(context, false));
               return pipeline;
            }

            // register transforms
            pipeline.Register(new IncrementTransform(context));
            pipeline.Register(new LogTransform(context));
            pipeline.Register(new DefaultTransform(new PipelineContext(ctx.Resolve<IPipelineLogger>(), calc, entity), entity.CalculatedFields));

            pipeline.Register(TransformFactory.GetTransforms(ctx, context, entity.CalculatedFields));
            pipeline.Register(ValidateFactory.GetValidators(ctx, context, entity.GetAllFields().Where(f => f.Validators.Any())));

            pipeline.Register(new StringTruncateTransfom(new PipelineContext(ctx.Resolve<IPipelineLogger>(), calc, entity)));

            // register input and output
            pipeline.Register(ctx.IsRegistered<IRead>() ? ctx.Resolve<IRead>() : new NullReader(context));
            pipeline.Register(ctx.IsRegistered<IWrite>() ? ctx.Resolve<IWrite>() : new NullWriter(context));

            if (outputContext.Connection.Provider == "sqlserver") {
               pipeline.Register(new MinDateTransform(new PipelineContext(ctx.Resolve<IPipelineLogger>(), calc, entity), new DateTime(1753, 1, 1)));
            }

            return pipeline;
         }).As<IPipeline>();

         // process controller
         builder.Register<IProcessController>(ctx => {

            var pipelines = new List<IPipeline>();

            // entity-level pipelines
            foreach (var entity in process.Entities) {
               var pipeline = ctx.ResolveNamed<IPipeline>(entity.Key);

               pipelines.Add(pipeline);
               if (entity.Delete && process.Mode != "init") {
                  pipeline.Register(ctx.ResolveNamed<IEntityDeleteHandler>(entity.Key));
               }
            }

            // process-level pipeline for process level calculated fields
            if (ctx.IsRegistered<IPipeline>()) {
               pipelines.Add(ctx.Resolve<IPipeline>());
            }

            var context = ctx.Resolve<IContext>();
            var controller = new ProcessController(pipelines, context);

            // output initialization
            if (process.Mode == "init" && ctx.IsRegistered<IInitializer>()) {
               controller.PreActions.Add(ctx.Resolve<IInitializer>());
            }

            // flatten(ing) is first post-action
            var output = process.GetOutputConnection();
            var isAdo = _adoProviders.Contains(output.Provider);
            if (process.Flatten && isAdo) {
               if (ctx.IsRegisteredWithName<IAction>(output.Key)) {
                  controller.PostActions.Add(ctx.ResolveNamed<IAction>(process.GetOutputConnection().Key));
               } else {
                  context.Error($"Could not find ADO Flatten Action for provider {output.Provider}.");
               }
            }

            // actions
            foreach (var action in process.Actions.Where(a => a.GetModes().Any(m => m == process.Mode || m == "*"))) {
               if (action.Before) {
                  controller.PreActions.Add(ctx.ResolveNamed<IAction>(action.Key));
               }
               if (action.After) {
                  controller.PostActions.Add(ctx.ResolveNamed<IAction>(action.Key));
               }
            }

            foreach (var map in process.Maps.Where(m => !string.IsNullOrEmpty(m.Query))) {
               controller.PreActions.Add(new MapReaderAction(context, map, ctx.ResolveNamed<IMapReader>(map.Name)));
            }

            return controller;
         }).As<IProcessController>();

         var build = builder.Build();

         return build.BeginLifetimeScope();

      }

   }

}